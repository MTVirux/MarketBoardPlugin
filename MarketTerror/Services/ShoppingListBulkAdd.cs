// <copyright file="ShoppingListBulkAdd.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using System.Net.Http;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Dalamud.Plugin.Services;
  using Lumina.Excel.Sheets;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// Adds a whole category of items to the buy list, one Universalis request per chunk of item ids.
  /// </summary>
  /// <remarks>
  /// Only one job runs at a time. A chunk arrives all at once but its rows are shown one at a time
  /// over the time the next chunk is expected to take, so the list fills gradually. When the scope
  /// covers more than one target, each chunk is asked for every target in turn and a row is only
  /// priced once all the answers are in, so it can show the cheapest of them.
  /// </remarks>
  public sealed class ShoppingListBulkAdd : IDisposable
  {
    /// <summary>
    /// The pause between two requests. Universalis documents no rate limit, so this is a courtesy margin.
    /// </summary>
    private const int ChunkDelayMilliseconds = 1000;

    /// <summary>
    /// How many listings a picked row is checked against. More than a market board can show in one go.
    /// </summary>
    private const int PickListingCount = 100;

    private readonly MarketTerrorPlugin plugin;

    /// <summary>
    /// One entry per queried item, in query order, null when the item has no row to show.
    /// </summary>
    private readonly Queue<SavedItem?> pendingReveal = new Queue<SavedItem?>();

    private double revealPerMillisecond;

    private DateTime lastRevealUtc;

    private CancellationTokenSource? cancellation;

    private Task? job;

    private bool refreshing;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingListBulkAdd"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public ShoppingListBulkAdd(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      this.plugin.Framework.Update += this.HandleFrameworkUpdateEvent;
    }

    /// <summary>
    /// Gets the name of the category being added.
    /// </summary>
    public string CategoryName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the number of items shown so far.
    /// </summary>
    public int Counted { get; private set; }

    /// <summary>
    /// Gets the number of items the running job started with.
    /// </summary>
    public int Total { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a job is still running or still has rows left to show.
    /// </summary>
    public bool IsRunning => this.job is { IsCompleted: false } || this.pendingReveal.Count > 0;

    /// <summary>
    /// Starts adding a category to the buy list, unless a job is already running.
    /// </summary>
    /// <param name="categoryName">The name of the category, shown while the job runs.</param>
    /// <param name="items">The items to add.</param>
    /// <param name="queryTargets">The worlds, data centres or regions to price the items against.</param>
    public void Start(string categoryName, IReadOnlyList<Item> items, IReadOnlyList<string> queryTargets)
    {
      this.StartJob(categoryName, items, queryTargets, false);
    }

    /// <summary>
    /// Starts pricing the items already on the buy list again, unless a job is already running.
    /// </summary>
    /// <param name="items">The items to price again.</param>
    /// <param name="queryTargets">The worlds, data centres or regions to price the items against.</param>
    /// <param name="label">What to call the job while it runs, or null for the whole list.</param>
    public void StartRefresh(IReadOnlyList<Item> items, IReadOnlyList<string> queryTargets, string? label = null)
    {
      this.StartJob(label ?? "the shopping list", items, queryTargets, true);
    }

    /// <summary>
    /// Stops the running job. The rows already added stay in the buy list.
    /// </summary>
    public void Cancel()
    {
      this.cancellation?.Cancel();
      this.pendingReveal.Clear();
      this.plugin.ShoppingList.ClearRefreshing();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.plugin.Framework.Update -= this.HandleFrameworkUpdateEvent;
      this.cancellation?.Cancel();
      this.cancellation?.Dispose();
      this.cancellation = null;
      this.isDisposed = true;
    }

    /// <summary>
    /// Splits the items into the qualities they have to be priced at.
    /// </summary>
    /// <param name="items">The items being priced.</param>
    /// <param name="quality">The quality each item's row is holding out for, when it has one.</param>
    /// <returns>One group an item can be asked for in a single request, with the quality to ask for.</returns>
    /// <remarks>
    /// A row keeps the quality it was priced at, so a cheaper listing of the other one cannot take its
    /// place. Rows of both qualities therefore cost a request each, since Universalis filters per request.
    /// </remarks>
    private static IEnumerable<(bool? Hq, Item[] Items)> QualityGroups(Item[] items, IReadOnlyDictionary<uint, bool> quality)
    {
      if (quality.Count == 0)
      {
        yield return (null, items);
        yield break;
      }

      foreach (var group in items.GroupBy(i => quality.TryGetValue(i.RowId, out var hq) ? (bool?)hq : null))
      {
        yield return (group.Key, group.ToArray());
      }
    }

    /// <summary>
    /// Drops the listings a scope handed back twice.
    /// </summary>
    /// <param name="listings">The listings a row was priced against.</param>
    /// <returns>One listing per Universalis listing id, and everything without an id.</returns>
    /// <remarks>A scope can ask a world and its data centre in the same breath, which returns both.</remarks>
    private static IEnumerable<PickedListing> Distinct(IEnumerable<PickedListing> listings)
    {
      var seen = new HashSet<string>(StringComparer.Ordinal);

      return listings.Where(l => l.ListingId.Length == 0 || seen.Add(l.ListingId));
    }

    /// <summary>
    /// Puts a row that has lost every listing it was buying back on the cheapest one still on sale.
    /// </summary>
    /// <param name="row">The row to price again.</param>
    /// <param name="fresh">Every listing of the item across the scope.</param>
    private static void RebaseOnCheapest(SavedItem row, IReadOnlyList<PickedListing> fresh)
    {
      var cheapest = fresh.Count == 0 ? null : fresh.MinBy(f => f.Price);

      if (cheapest == null)
      {
        row.Unlisted = true;
        return;
      }

      row.Price = cheapest.Price;
      row.Quantity = cheapest.Quantity;
      row.Hq = cheapest.Hq;
      row.World = cheapest.World;
      row.Unlisted = false;
    }

    private void StartJob(string categoryName, IReadOnlyList<Item> items, IReadOnlyList<string> queryTargets, bool refresh)
    {
      ArgumentNullException.ThrowIfNull(items);
      ArgumentNullException.ThrowIfNull(queryTargets);

      if (this.IsRunning || items.Count == 0 || queryTargets.Count == 0)
      {
        return;
      }

      this.cancellation?.Dispose();
      this.cancellation = new CancellationTokenSource();

      var token = this.cancellation.Token;
      var queued = items.ToArray();
      var targets = queryTargets.ToArray();
      var picked = Array.Empty<(SavedItem Row, string[] Targets)>();
      var quality = new Dictionary<uint, bool>();

      if (refresh)
      {
        // Only the rows being priced again, so a single row refresh leaves the rest of the list alone.
        var ids = queued.Select(i => i.RowId).ToHashSet();

        // A direct listing is left out of refreshes, so its colour is left alone too.
        this.plugin.ShoppingList.ClearOutcomes(row => !row.IsDirect && ids.Contains(row.SourceItem.RowId));
        this.plugin.ShoppingList.MarkRefreshing(ids);

        // The bulk query only ever comes back with one listing an item, which cannot say whether a
        // particular picked listing is still there, so those rows are checked one at a time after it.
        // A limited row joins them even with nothing picked yet, since its rule sweeps the same listings.
        // Its scope is read here, on the framework thread, rather than from the job.
        picked = this.plugin.ShoppingList
          .Where(row => (row.HasPicks || row.IsLimited) && ids.Contains(row.SourceItem.RowId))
          .Select(row => (Row: row, Targets: ListingLimitScope.TargetsFor(this.plugin, row, targets).ToArray()))
          .ToArray();

        foreach (var row in this.plugin.ShoppingList.Where(row => !row.HasPicks && !row.IsLimited && ids.Contains(row.SourceItem.RowId)))
        {
          quality[row.SourceItem.RowId] = row.Hq;
        }
      }

      this.CategoryName = categoryName;
      this.Counted = 0;
      this.Total = queued.Length + picked.Length;
      this.refreshing = refresh;
      this.lastRevealUtc = DateTime.UtcNow;

      this.job = Task.Run(() => this.Run(queued, targets, picked, quality, token), token);
    }

    private async Task Run(Item[] items, string[] targets, (SavedItem Row, string[] Targets)[] picked, IReadOnlyDictionary<uint, bool> quality, CancellationToken token)
    {
      var firstRequest = true;
      var queries = 0;
      var elapsed = Stopwatch.StartNew();

      try
      {
        foreach (var (hq, group) in QualityGroups(items, quality))
        {
          foreach (var chunk in group.Chunk(UniversalisClient.MaxItemsPerRequest))
          {
            token.ThrowIfCancellationRequested();

            var ids = chunk.Select(i => i.RowId).ToArray();
            var cooldowns = firstRequest ? targets.Length - 1 : targets.Length;
            var startedUtc = DateTime.UtcNow;
            var answers = new List<(string Target, IReadOnlyDictionary<uint, MarketDataResponse> Prices)>();

            // Every target is asked before anything is priced, so a row can show the cheapest of them.
            foreach (var target in targets)
            {
              if (!firstRequest)
              {
                await Task.Delay(ChunkDelayMilliseconds, token).ConfigureAwait(false);
              }

              firstRequest = false;
              queries++;

              answers.Add((target, await this.Fetch(ids, target, hq, token).ConfigureAwait(false)));
            }

            var entries = new List<SavedItem>();

            foreach (var item in chunk)
            {
              SavedItem? cheapest = null;

              foreach (var (target, prices) in answers)
              {
                var entry = SavedItem.FromCheapestListing(
                  item,
                  prices.GetValueOrDefault(item.RowId),
                  !this.plugin.Config.NoGilSalesTax,
                  target);

                if (entry != null && (cheapest == null || entry.Price < cheapest.Price))
                {
                  cheapest = entry;
                }
              }

              if (cheapest != null)
              {
                entries.Add(cheapest);
              }
            }

            token.ThrowIfCancellationRequested();

            // How long this chunk's requests really took, so the rows are shown over the time the next one needs.
            var requestGuess = Math.Max(200d, (DateTime.UtcNow - startedUtc).TotalMilliseconds - (cooldowns * ChunkDelayMilliseconds));

            // The buy list is read while the window draws, so it may only be touched on the framework thread.
            await this.plugin.Framework
              .RunOnFrameworkThread(() => this.Apply(entries, chunk, (targets.Length * ChunkDelayMilliseconds) + requestGuess))
              .ConfigureAwait(false);
          }
        }

        foreach (var (row, rowTargets) in picked)
        {
          token.ThrowIfCancellationRequested();

          var fresh = new List<PickedListing>();
          var complete = true;

          foreach (var target in rowTargets)
          {
            await Task.Delay(ChunkDelayMilliseconds, token).ConfigureAwait(false);
            queries++;

            var listings = await this.FetchListings(row.SourceItem.RowId, target, token).ConfigureAwait(false);

            if (listings == null)
            {
              complete = false;
              continue;
            }

            fresh.AddRange(listings);
          }

          await this.plugin.Framework.RunOnFrameworkThread(() => this.ApplyPicks(row, fresh, complete)).ConfigureAwait(false);
        }

        elapsed.Stop();

        // A cancelled job keeps the previous stats, since it only priced part of what it was given.
        var stats = new QueryStats
        {
          Items = items.Length + picked.Length,
          Queries = queries,
          Scope = string.Join(" and ", targets),
          Milliseconds = elapsed.ElapsedMilliseconds,
        };

        await this.plugin.Framework.RunOnFrameworkThread(() => this.RecordStats(stats)).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        this.plugin.Log.Debug($"Cancelled adding {this.CategoryName} to the buy list.");
      }
    }

    private void RecordStats(QueryStats stats)
    {
      this.plugin.Config.ShoppingListLastQuery = stats;
      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }

    private async Task<IReadOnlyDictionary<uint, MarketDataResponse>> Fetch(uint[] ids, string target, bool? hq, CancellationToken token)
    {
      try
      {
        return await this.plugin.UniversalisClient.GetCheapestListings(ids, target, token, hq).ConfigureAwait(false);
      }
      catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
      {
        // One bad request should not abandon the rest of the category, its items keep the price they had.
        this.plugin.Log.Warning(ex, $"Skipped {ids.Length} items on {target} while adding {this.CategoryName} to the buy list.");
        return new Dictionary<uint, MarketDataResponse>();
      }
    }

    /// <summary>
    /// Fetches every listing of one item on one target.
    /// </summary>
    /// <param name="itemId">The row id of the item to fetch.</param>
    /// <param name="target">The world, data centre or region to fetch from.</param>
    /// <param name="token">Cancels the fetch.</param>
    /// <returns>The listings, or null when the request could not be made.</returns>
    private async Task<IReadOnlyList<PickedListing>?> FetchListings(uint itemId, string target, CancellationToken token)
    {
      try
      {
        var data = await this.plugin.UniversalisClient
          .GetMarketData(itemId, target, PickListingCount, 0, token)
          .ConfigureAwait(false);

        var withTax = !this.plugin.Config.NoGilSalesTax;

        return data.Listings
          .Select(listing => PickedListing.FromListing(listing, withTax, data.WorldName ?? target))
          .ToArray();
      }
      catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
      {
        // The row keeps the picks it has rather than having them all called gone by a failed request.
        this.plugin.Log.Warning(ex, $"Skipped checking the picked listings of item {itemId} on {target}.");
        return null;
      }
    }

    /// <summary>
    /// Picks a limited row's listings again from scratch, taking whatever is under its rule right now.
    /// </summary>
    /// <param name="row">The row to sweep.</param>
    /// <param name="fresh">Every listing of the item across the row's scope.</param>
    /// <param name="complete">True when every target answered, so the scope can be taken at its word.</param>
    private void ApplyLimit(SavedItem row, IReadOnlyList<PickedListing> fresh, bool complete)
    {
      // A request that never landed cannot say what is on sale, so the row keeps what it already buys.
      if (complete)
      {
        row.SetSweptPicks(row.Limit!.Apply(Distinct(fresh)));
      }

      row.Outcome = BuyOutcome.None;
      row.Refreshing = false;
      this.Counted++;

      this.plugin.ShoppingList.Persist();
    }

    /// <summary>
    /// Marks a picked row's listings against what is on sale now, dropping the ones that have sold out.
    /// </summary>
    /// <param name="row">The row to check.</param>
    /// <param name="fresh">Every listing of the item across the scope.</param>
    /// <param name="complete">True when every target answered, so the scope can be taken at its word.</param>
    private void ApplyPicks(SavedItem row, IReadOnlyList<PickedListing> fresh, bool complete)
    {
      if (row.Limit != null)
      {
        this.ApplyLimit(row, fresh, complete);
        return;
      }

      // One retainer can have two stacks that look alike, so a listing only answers for one pick.
      var claimed = new HashSet<PickedListing>();
      var live = new List<PickedListing>();

      foreach (var pick in row.Picks)
      {
        var match = fresh.FirstOrDefault(f => !claimed.Contains(f) && pick.SameAs(f));

        if (match == null)
        {
          pick.Gone = true;
          continue;
        }

        claimed.Add(match);
        pick.Gone = false;
        pick.Price = match.Price;
        pick.Outcome = BuyOutcome.None;
        pick.Paid = null;
        live.Add(pick);
      }

      // A request that never landed cannot say a listing has sold out, so those picks are only marked
      // gone and the row keeps them until it is priced again.
      if (complete)
      {
        row.SetPicks(live);
      }

      if (live.Count > 0)
      {
        row.Unlisted = false;
      }
      else if (complete)
      {
        RebaseOnCheapest(row, fresh);
      }

      row.Outcome = BuyOutcome.None;
      row.Refreshing = false;
      this.Counted++;

      this.plugin.ShoppingList.Persist();
    }

    private void Apply(IReadOnlyList<SavedItem> entries, IReadOnlyList<Item> chunk, double windowMilliseconds)
    {
      if (this.refreshing)
      {
        this.plugin.ShoppingList.Replace(entries);

        // The items the scope had nothing for keep their row, but lose the price it no longer holds.
        var priced = entries.Select(e => e.SourceItem.RowId).ToHashSet();
        this.plugin.ShoppingList.MarkUnlisted(chunk.Where(i => !priced.Contains(i.RowId)).Select(i => i.RowId));
      }
      else
      {
        // A row added straight from a listing is not a priced row, so it does not stand in for one.
        var listed = this.plugin.ShoppingList.Where(s => !s.IsDirect).Select(s => s.SourceItem.RowId).ToHashSet();
        var added = entries.Where(e => listed.Add(e.SourceItem.RowId)).ToArray();

        foreach (var entry in added)
        {
          entry.Refreshing = true;
        }

        this.plugin.ShoppingList.AddRange(added);
      }

      // One step per queried item, so the count still reaches the total for items with no row.
      foreach (var item in chunk)
      {
        this.pendingReveal.Enqueue(this.plugin.ShoppingList.Find(item.RowId));
      }

      this.revealPerMillisecond = this.pendingReveal.Count / Math.Max(windowMilliseconds, 1d);
      this.lastRevealUtc = DateTime.UtcNow;
    }

    private void HandleFrameworkUpdateEvent(IFramework framework)
    {
      if (this.pendingReveal.Count == 0)
      {
        return;
      }

      var now = DateTime.UtcNow;
      var due = (int)((now - this.lastRevealUtc).TotalMilliseconds * this.revealPerMillisecond);

      if (due <= 0)
      {
        return;
      }

      for (var i = 0; i < due && this.pendingReveal.Count > 0; i++)
      {
        var entry = this.pendingReveal.Dequeue();

        if (entry != null)
        {
          entry.Refreshing = false;
        }

        this.Counted++;
      }

      this.lastRevealUtc = now;
    }
  }
}
