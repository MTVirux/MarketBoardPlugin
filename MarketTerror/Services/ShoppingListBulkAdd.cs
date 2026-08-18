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
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// Prices the buy list against Universalis, one market and item at a time, and adds whole categories
  /// to it.
  /// </summary>
  /// <remarks>
  /// Only one job runs at a time. Entries are grouped by the market they shop in, and one item of one
  /// market is the unit of work: it is asked for on every target that market reaches before any entry
  /// under it is resolved, so an entry chooses out of the whole scope rather than out of whichever part
  /// answered first. Each answer is revealed over the time the next one is expected to take, so the
  /// progress line climbs steadily instead of jumping.
  /// </remarks>
  public sealed class ShoppingListBulkAdd : IDisposable
  {
    /// <summary>
    /// The pause between two requests. Universalis documents no rate limit, so this is a courtesy margin.
    /// </summary>
    private const int RequestDelayMilliseconds = 1000;

    /// <summary>
    /// How many listings an entry is resolved against. More than a market board can show in one go.
    /// </summary>
    private const int ListingsPerRequest = 100;

    private readonly MarketTerrorPlugin plugin;

    /// <summary>
    /// One item per priced market and item pair, in query order, null when the pair has no entry to show.
    /// </summary>
    private readonly Queue<ListingEntry?> pendingReveal = new Queue<ListingEntry?>();

    private double revealPerMillisecond;

    private DateTime lastRevealUtc;

    private CancellationTokenSource? cancellation;

    private Task? job;

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
    /// Gets the number of market and item pairs priced so far.
    /// </summary>
    public int Counted { get; private set; }

    /// <summary>
    /// Gets the number of market and item pairs the running job started with.
    /// </summary>
    /// <remarks>An item on the list twice under two markets is two of them, since it costs two fetches.</remarks>
    public int Total { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a job is still running or still has answers left to show.
    /// </summary>
    public bool IsRunning => this.job is { IsCompleted: false } || this.pendingReveal.Count > 0;

    /// <summary>
    /// Starts adding a category to the buy list, unless a job is already running.
    /// </summary>
    /// <param name="categoryName">The name of the category, shown while the job runs.</param>
    /// <param name="items">The items to add.</param>
    /// <param name="scope">The market the new entries shop in.</param>
    public void Start(string categoryName, IReadOnlyList<Item> items, ListingScope scope)
    {
      ArgumentNullException.ThrowIfNull(items);
      ArgumentNullException.ThrowIfNull(scope);

      if (this.IsRunning || items.Count == 0)
      {
        return;
      }

      this.plugin.ShoppingList.AddLowestRange(items, scope);

      var ids = items.Select(i => i.RowId).ToHashSet();

      // An item that already had an entry in this scope was left alone by the add, and is priced
      // along with the new ones rather than being the one row the category leaves stale.
      this.StartJob(
        categoryName,
        this.plugin.ShoppingList.InScope(scope)
          .Where(e => e.Kind == ListingKind.Lowest && ids.Contains(e.SourceItem.RowId))
          .ToArray());
    }

    /// <summary>
    /// Starts pricing entries already on the buy list again, unless a job is already running.
    /// </summary>
    /// <param name="entries">The entries to price again.</param>
    /// <param name="label">What to call the job while it runs, or null for the whole list.</param>
    public void StartRefresh(IEnumerable<ListingEntry> entries, string? label = null)
    {
      ArgumentNullException.ThrowIfNull(entries);

      this.StartJob(label ?? "the shopping list", entries.ToArray());
    }

    /// <summary>
    /// Stops the running job. The entries already priced keep what they were priced at.
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
    /// Works out which qualities of a listing the request has to come back with.
    /// </summary>
    /// <param name="entries">Every entry buying one item in one market, at least one of them.</param>
    /// <returns>True for high quality only, false for normal quality only, null for both.</returns>
    /// <remarks>
    /// Universalis filters per request and the whole pair shares one, so a quality can only be left out
    /// when no entry priced off the answer would take it. An entry with no conditions on it buys
    /// whatever is cheapest, so it wants both.
    /// </remarks>
    private static bool? QualityAsked(ListingEntry[] entries)
    {
      if (entries.Length == 0)
      {
        return null;
      }

      if (entries.All(e => e.Conditions?.Quality == QualityFilter.HqOnly))
      {
        return true;
      }

      if (entries.All(e => e.Conditions?.Quality == QualityFilter.NqOnly))
      {
        return false;
      }

      return null;
    }

    private void StartJob(string label, ListingEntry[] entries)
    {
      if (this.IsRunning || entries.Length == 0)
      {
        return;
      }

      var catalogue = this.plugin.WorldCatalogue;

      // A scope whose anchor world is not in the catalogue has nowhere to ask, so its entries are left
      // out rather than being marked as waiting for an answer that never comes.
      var groups = entries
        .GroupBy(e => e.Scope)
        .Select(g => (
          Targets: g.Key.Targets(catalogue).ToArray(),
          Items: g.Select(e => e.SourceItem.RowId).Distinct().ToArray(),
          Entries: g.ToArray()))
        .Where(g => g.Targets.Length > 0)
        .ToArray();

      if (groups.Length == 0)
      {
        return;
      }

      this.cancellation?.Dispose();
      this.cancellation = new CancellationTokenSource();

      var token = this.cancellation.Token;
      var touched = new HashSet<ListingEntry>(groups.SelectMany(g => g.Entries));

      this.plugin.ShoppingList.ClearOutcomes(touched.Contains);
      this.plugin.ShoppingList.MarkRefreshing(touched);

      this.CategoryName = label;
      this.Counted = 0;
      this.Total = groups.Sum(g => g.Items.Length);
      this.lastRevealUtc = DateTime.UtcNow;

      this.job = Task.Run(() => this.Run(groups, token), token);
    }

    private async Task Run((string[] Targets, uint[] Items, ListingEntry[] Entries)[] groups, CancellationToken token)
    {
      var firstRequest = true;
      var queries = 0;
      var priced = 0;
      var elapsed = Stopwatch.StartNew();

      try
      {
        foreach (var group in groups)
        {
          foreach (var itemId in group.Items)
          {
            token.ThrowIfCancellationRequested();

            var forItem = group.Entries.Where(e => e.SourceItem.RowId == itemId).ToArray();
            var quality = QualityAsked(forItem);
            var cooldowns = firstRequest ? group.Targets.Length - 1 : group.Targets.Length;
            var startedUtc = DateTime.UtcNow;
            var onSale = new List<ResolvedListing>();

            foreach (var target in group.Targets)
            {
              if (!firstRequest)
              {
                await Task.Delay(RequestDelayMilliseconds, token).ConfigureAwait(false);
              }

              firstRequest = false;
              queries++;

              onSale.AddRange(await this.FetchListings(itemId, target, quality, token).ConfigureAwait(false));
            }

            token.ThrowIfCancellationRequested();
            priced++;

            // How long this pair's requests really took, so its answer is revealed over the time the
            // next one needs.
            var requestGuess = Math.Max(200d, (DateTime.UtcNow - startedUtc).TotalMilliseconds - (cooldowns * RequestDelayMilliseconds));
            var window = (group.Targets.Length * RequestDelayMilliseconds) + requestGuess;

            // The buy list is read while the window draws, so it may only be touched on the framework thread.
            await this.plugin.Framework
              .RunOnFrameworkThread(() => this.Apply(forItem, onSale, window))
              .ConfigureAwait(false);
          }
        }

        elapsed.Stop();

        // A cancelled job keeps the previous stats, since it only priced part of what it was given.
        var stats = new QueryStats
        {
          Items = priced,
          Queries = queries,
          Scope = string.Join(" and ", groups.SelectMany(g => g.Targets).Distinct(StringComparer.OrdinalIgnoreCase)),
          Milliseconds = elapsed.ElapsedMilliseconds,
        };

        await this.plugin.Framework.RunOnFrameworkThread(() => this.RecordStats(stats)).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        this.plugin.Log.Debug($"Cancelled pricing {this.CategoryName}.");
      }
    }

    private void RecordStats(QueryStats stats)
    {
      this.plugin.Config.ShoppingListLastQuery = stats;
      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }

    /// <summary>
    /// Fetches every listing of one item on one target.
    /// </summary>
    /// <param name="itemId">The row id of the item to fetch.</param>
    /// <param name="target">The world, data centre or region to fetch from.</param>
    /// <param name="hq">True for high quality listings only, false for normal quality only, null for both.</param>
    /// <param name="token">Cancels the fetch.</param>
    /// <returns>The listings, or none when the request could not be made.</returns>
    /// <remarks>
    /// A request that never landed comes back empty rather than as a failure of its own. An entry
    /// resolved against nothing goes unlisted, which is the honest answer: the plugin no longer knows
    /// what is on sale, and a price it can no longer stand behind is worse than none.
    /// </remarks>
    private async Task<IReadOnlyList<ResolvedListing>> FetchListings(uint itemId, string target, bool? hq, CancellationToken token)
    {
      try
      {
        var data = await this.plugin.UniversalisClient
          .GetMarketData(itemId, target, ListingsPerRequest, 0, token, hq)
          .ConfigureAwait(false);

        var withTax = !this.plugin.Config.NoGilSalesTax;

        return data.Listings
          .Select(listing => ResolvedListing.FromListing(listing, withTax, data.WorldName ?? target))
          .ToArray();
      }
      catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
      {
        // One bad request should not abandon the rest of the job, so only this pair is given up on.
        this.plugin.Log.Warning(ex, $"Skipped item {itemId} on {target} while pricing {this.CategoryName}.");
        return Array.Empty<ResolvedListing>();
      }
    }

    /// <summary>
    /// Resolves every entry buying one item in one market against what that market handed back.
    /// </summary>
    /// <param name="entries">The entries the answer is for.</param>
    /// <param name="onSale">Every listing of their item across their market, which can be none.</param>
    /// <param name="windowMilliseconds">How long the next answer is expected to take.</param>
    private void Apply(ListingEntry[] entries, IReadOnlyList<ResolvedListing> onSale, double windowMilliseconds)
    {
      // Only the resolver decides what an entry buys, so a direct entry stays on its own listing even
      // though it is priced off the same array as everything else.
      foreach (var entry in entries)
      {
        this.plugin.ShoppingList.ApplyMatches(entry, ListingResolver.Resolve(entry, onSale));
      }

      // One step per market and item pair, so the count reaches the total however many entries share one.
      this.pendingReveal.Enqueue(entries.Length > 0 ? entries[0] : null);

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
