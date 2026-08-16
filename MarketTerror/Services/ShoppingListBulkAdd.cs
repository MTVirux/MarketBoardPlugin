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
    private const int ChunkDelayMilliseconds = 1000;

    /// <summary>
    /// How long the first request is assumed to take. Later chunks use the time the previous one took.
    /// </summary>
    private const double FirstRequestGuessMilliseconds = 700;

    private readonly MarketTerrorPlugin plugin;

    private DateTime pendingStartedUtc;

    private int pendingBaseline;

    private int pendingChunkSize;

    private double pendingDurationMilliseconds;

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
    /// Gets the item count the progress display should show.
    /// </summary>
    /// <remarks>
    /// The chunk being worked on is counted in one item at a time over its cooldown and request, so
    /// the count ticks up instead of standing still and then jumping a whole chunk. The ramp eases
    /// towards the end of the chunk without reaching it, so a slow request slows the count down
    /// rather than stopping it dead.
    /// </remarks>
    public int Counted
    {
      get
      {
        if (this.pendingChunkSize <= 0)
        {
          return this.Processed;
        }

        var elapsed = (DateTime.UtcNow - this.pendingStartedUtc).TotalMilliseconds;
        var ramp = 1d - Math.Exp(-3d * Math.Max(elapsed, 0d) / this.pendingDurationMilliseconds);
        var ticked = this.pendingBaseline + (int)(this.pendingChunkSize * ramp);

        // Never below Processed, so the count cannot fall back once the chunk lands.
        return Math.Max(this.Processed, ticked);
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

          // Every chunk counts at the same speed per item, so a short last chunk finishes early
          // instead of stretching its few items over a whole cooldown.
          var perItem = (cooldown + requestGuess) / UniversalisClient.MaxItemsPerRequest;

          // Written before the baseline so a half seen update reads as "just started", not as a full chunk.
          this.pendingStartedUtc = startedUtc;
          this.pendingDurationMilliseconds = perItem * chunk.Length;
          this.pendingChunkSize = chunk.Length;
          this.pendingBaseline = this.Processed;

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
            requestGuess = Math.Max(200d, (DateTime.UtcNow - startedUtc).TotalMilliseconds - cooldown);
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
          await this.plugin.Framework.RunOnFrameworkThread(() => this.Apply(entries, chunk)).ConfigureAwait(false);

          // How long this chunk really took, so the next one paces its count against a measured time.
          requestGuess = Math.Max(200d, (DateTime.UtcNow - startedUtc).TotalMilliseconds - cooldown);
          this.Processed += chunk.Length;
        }
      }
      catch (OperationCanceledException)
      {
        this.plugin.Log.Debug($"Cancelled adding {this.CategoryName} to the buy list.");
      }
      finally
      {
        if (this.refreshing)
        {
          // Anything skipped or cancelled keeps the price it already had.
          await this.plugin.Framework.RunOnFrameworkThread(() => this.plugin.ShoppingList.ClearRefreshing()).ConfigureAwait(false);
        }
      }
    }

    private void Apply(IReadOnlyList<SavedItem> entries, IReadOnlyList<Item> chunk)
    {
      if (this.refreshing)
      {
        this.plugin.ShoppingList.Replace(entries);
        this.plugin.ShoppingList.ClearRefreshing(chunk.Select(i => i.RowId));
        return;
      }

      var listed = this.plugin.ShoppingList.Select(s => s.SourceItem.RowId).ToHashSet();

      this.plugin.ShoppingList.AddRange(entries.Where(e => listed.Add(e.SourceItem.RowId)));
    }
  }
}
