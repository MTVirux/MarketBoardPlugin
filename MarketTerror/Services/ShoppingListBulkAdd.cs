// <copyright file="ShoppingListBulkAdd.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
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
  /// over the time the next chunk is expected to take, so the list fills gradually.
  /// </remarks>
  public sealed class ShoppingListBulkAdd : IDisposable
  {
    /// <summary>
    /// The pause between two chunk requests. Universalis documents no rate limit, so this is a courtesy margin.
    /// </summary>
    private const int ChunkDelayMilliseconds = 1000;

    /// <summary>
    /// How long the first request is assumed to take. Later chunks use the time the previous one took.
    /// </summary>
    private const double FirstRequestGuessMilliseconds = 700;

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
    /// <param name="queryTarget">The world, data centre or region to price the items against.</param>
    public void Start(string categoryName, IReadOnlyList<Item> items, string queryTarget)
    {
      this.StartJob(categoryName, items, queryTarget, false);
    }

    /// <summary>
    /// Starts pricing the items already on the buy list again, unless a job is already running.
    /// </summary>
    /// <param name="items">The items to price again.</param>
    /// <param name="queryTarget">The world, data centre or region to price the items against.</param>
    public void StartRefresh(IReadOnlyList<Item> items, string queryTarget)
    {
      this.StartJob("the shopping list", items, queryTarget, true);
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

    private void StartJob(string categoryName, IReadOnlyList<Item> items, string queryTarget, bool refresh)
    {
      ArgumentNullException.ThrowIfNull(items);

      if (this.IsRunning || items.Count == 0)
      {
        return;
      }

      this.cancellation?.Dispose();
      this.cancellation = new CancellationTokenSource();

      var token = this.cancellation.Token;
      var queued = items.ToArray();

      this.CategoryName = categoryName;
      this.Counted = 0;
      this.Total = queued.Length;
      this.refreshing = refresh;
      this.lastRevealUtc = DateTime.UtcNow;

      if (refresh)
      {
        this.plugin.ShoppingList.MarkRefreshing();
      }

      this.job = Task.Run(() => this.Run(queued, queryTarget, token), token);
    }

    private async Task Run(IReadOnlyList<Item> items, string queryTarget, CancellationToken token)
    {
      var firstChunk = true;
      var requestGuess = FirstRequestGuessMilliseconds;

      try
      {
        foreach (var chunk in items.Chunk(UniversalisClient.MaxItemsPerRequest))
        {
          token.ThrowIfCancellationRequested();

          var cooldown = firstChunk ? 0 : ChunkDelayMilliseconds;
          var startedUtc = DateTime.UtcNow;

          if (!firstChunk)
          {
            await Task.Delay(ChunkDelayMilliseconds, token).ConfigureAwait(false);
          }

          firstChunk = false;

          IReadOnlyDictionary<uint, MarketDataResponse> prices;

          try
          {
            prices = await this.plugin.UniversalisClient
              .GetCheapestListings(chunk.Select(i => i.RowId).ToArray(), queryTarget, token)
              .ConfigureAwait(false);
          }
          catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
          {
            // One bad chunk should not abandon the rest of the category, its items keep the price they had.
            this.plugin.Log.Warning(ex, $"Skipped {chunk.Length} items while adding {this.CategoryName} to the buy list.");
            prices = new Dictionary<uint, MarketDataResponse>();
          }

          var entries = new List<SavedItem>();

          foreach (var item in chunk)
          {
            var entry = SavedItem.FromCheapestListing(
              item,
              prices.GetValueOrDefault(item.RowId),
              !this.plugin.Config.NoGilSalesTax,
              queryTarget);

            if (entry != null)
            {
              entries.Add(entry);
            }
          }

          token.ThrowIfCancellationRequested();

          // How long this chunk really took, so the rows are shown over the time the next one needs.
          requestGuess = Math.Max(200d, (DateTime.UtcNow - startedUtc).TotalMilliseconds - cooldown);

          // The buy list is read while the window draws, so it may only be touched on the framework thread.
          await this.plugin.Framework
            .RunOnFrameworkThread(() => this.Apply(entries, chunk, ChunkDelayMilliseconds + requestGuess))
            .ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException)
      {
        this.plugin.Log.Debug($"Cancelled adding {this.CategoryName} to the buy list.");
      }
    }

    private void Apply(IReadOnlyList<SavedItem> entries, IReadOnlyList<Item> chunk, double windowMilliseconds)
    {
      if (this.refreshing)
      {
        this.plugin.ShoppingList.Replace(entries);
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
