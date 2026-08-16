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
  using Lumina.Excel.Sheets;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// Adds a whole category of items to the buy list, one Universalis request per chunk of item ids.
  /// </summary>
  /// <remarks>
  /// Only one job runs at a time. Rows appear as each chunk lands rather than all at the end, so a
  /// large category fills the buy list gradually.
  /// </remarks>
  public sealed class ShoppingListBulkAdd : IDisposable
  {
    /// <summary>
    /// The pause between two chunk requests. Universalis documents no rate limit, so this is a courtesy margin.
    /// </summary>
    private const int ChunkDelayMilliseconds = 3000;

    /// <summary>
    /// A rough guess at how long one chunk request takes, only used to pace the progress bar.
    /// </summary>
    private const int RequestGuessMilliseconds = 1000;

    private readonly MarketTerrorPlugin plugin;

    private DateTime pendingStartedUtc;

    private int pendingBaseline;

    private int pendingChunkSize;

    private int pendingDurationMilliseconds;

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
    }

    /// <summary>
    /// Gets the name of the category being added.
    /// </summary>
    public string CategoryName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the number of items whose price has been looked up so far.
    /// </summary>
    public int Processed { get; private set; }

    /// <summary>
    /// Gets the number of items the running job started with.
    /// </summary>
    public int Total { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a job is still running.
    /// </summary>
    public bool IsRunning => this.job is { IsCompleted: false };

    /// <summary>
    /// Gets how full the progress bar should be, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// The chunk being worked on is counted in gradually over its cooldown and request, so the bar
    /// creeps forward instead of standing still and then jumping a whole chunk at a time.
    /// </remarks>
    public float Progress
    {
      get
      {
        if (this.Total <= 0)
        {
          return 0f;
        }

        var done = (float)this.Processed;

        if (this.pendingChunkSize > 0)
        {
          var elapsed = (DateTime.UtcNow - this.pendingStartedUtc).TotalMilliseconds;
          var ramp = Math.Clamp(elapsed / this.pendingDurationMilliseconds, 0d, 1d);

          // Never below Processed, so the bar cannot fall back once the chunk lands.
          done = Math.Max(done, this.pendingBaseline + (float)(this.pendingChunkSize * ramp));
        }

        return Math.Clamp(done / this.Total, 0f, 1f);
      }
    }

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
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

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
      this.Processed = 0;
      this.Total = queued.Length;
      this.refreshing = refresh;
      this.pendingChunkSize = 0;

      this.job = Task.Run(() => this.Run(queued, queryTarget, token), token);
    }

    private async Task Run(IReadOnlyList<Item> items, string queryTarget, CancellationToken token)
    {
      var firstChunk = true;

      try
      {
        foreach (var chunk in items.Chunk(UniversalisClient.MaxItemsPerRequest))
        {
          token.ThrowIfCancellationRequested();

          this.pendingBaseline = this.Processed;
          this.pendingChunkSize = chunk.Length;
          this.pendingStartedUtc = DateTime.UtcNow;
          this.pendingDurationMilliseconds = RequestGuessMilliseconds + (firstChunk ? 0 : ChunkDelayMilliseconds);

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
            // One bad chunk should not abandon the rest of the category.
            this.plugin.Log.Warning(ex, $"Skipped {chunk.Length} items while adding {this.CategoryName} to the buy list.");
            this.Processed += chunk.Length;
            continue;
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

          // The buy list is read while the window draws, so it may only be touched on the framework thread.
          await this.plugin.Framework.RunOnFrameworkThread(() => this.Apply(entries)).ConfigureAwait(false);

          this.Processed += chunk.Length;
        }
      }
      catch (OperationCanceledException)
      {
        this.plugin.Log.Debug($"Cancelled adding {this.CategoryName} to the buy list.");
      }
    }

    private void Apply(IReadOnlyList<SavedItem> entries)
    {
      if (this.refreshing)
      {
        this.plugin.ShoppingList.Replace(entries);
        return;
      }

      var listed = this.plugin.ShoppingList.Select(s => s.SourceItem.RowId).ToHashSet();

      this.plugin.ShoppingList.AddRange(entries.Where(e => listed.Add(e.SourceItem.RowId)));
    }
  }
}
