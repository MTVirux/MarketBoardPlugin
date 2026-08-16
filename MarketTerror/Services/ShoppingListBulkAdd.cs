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
    public void StartRefresh(IReadOnlyList<Item> items, IReadOnlyList<string> queryTargets)
    {
      this.StartJob("the shopping list", items, queryTargets, true);
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

      this.CategoryName = categoryName;
      this.Counted = 0;
      this.Total = queued.Length;
      this.refreshing = refresh;
      this.lastRevealUtc = DateTime.UtcNow;

      if (refresh)
      {
        this.plugin.ShoppingList.ClearOutcomes();
        this.plugin.ShoppingList.MarkRefreshing();
      }

      this.job = Task.Run(() => this.Run(queued, targets, token), token);
    }

    private async Task Run(Item[] items, string[] targets, CancellationToken token)
    {
      var firstRequest = true;
      var queries = 0;
      var elapsed = Stopwatch.StartNew();

      try
      {
        foreach (var chunk in items.Chunk(UniversalisClient.MaxItemsPerRequest))
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

            answers.Add((target, await this.Fetch(ids, target, token).ConfigureAwait(false)));
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

        elapsed.Stop();

        // A cancelled job keeps the previous stats, since it only priced part of what it was given.
        var stats = new QueryStats
        {
          Items = items.Length,
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

    private async Task<IReadOnlyDictionary<uint, MarketDataResponse>> Fetch(uint[] ids, string target, CancellationToken token)
    {
      try
      {
        return await this.plugin.UniversalisClient.GetCheapestListings(ids, target, token).ConfigureAwait(false);
      }
      catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
      {
        // One bad request should not abandon the rest of the category, its items keep the price they had.
        this.plugin.Log.Warning(ex, $"Skipped {ids.Length} items on {target} while adding {this.CategoryName} to the buy list.");
        return new Dictionary<uint, MarketDataResponse>();
      }
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
        var listed = this.plugin.ShoppingList.Select(s => s.SourceItem.RowId).ToHashSet();
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
