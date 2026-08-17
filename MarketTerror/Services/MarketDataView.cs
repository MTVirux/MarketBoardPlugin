// <copyright file="MarketDataView.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Lumina.Excel.Sheets;
  using MarketTerror.Helpers;
  using MarketTerror.Models.FFXIVMT;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// One board's market data: what it is showing now, and the fetch that is filling it.
  /// </summary>
  /// <remarks>Responses come from and go back to the shared cache, so two boards on the same
  /// item and world pay for one fetch between them.</remarks>
  public sealed class MarketDataView : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly MarketDataCache cache;

    private readonly ApiStatus status;

    private Task? currentRefreshTask;

    private CancellationTokenSource? currentRefreshCancellationTokenSource;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataView"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="cache">The market data shared by every board.</param>
    /// <param name="status">The shared API status poller.</param>
    public MarketDataView(MarketTerrorPlugin plugin, MarketDataCache cache, ApiStatus status)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
      this.status = status ?? throw new ArgumentNullException(nameof(status));
    }

    /// <summary>
    /// Gets the market data for the item currently being shown, or null while it is loading or unavailable.
    /// </summary>
    public MarketDataResponse? MarketData { get; private set; }

    /// <summary>
    /// Gets the gilflux ranking for the item currently being shown, or null when there is none.
    /// </summary>
    public GilfluxRankingItem? Gilflux { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the gilflux ranking is being fetched.
    /// </summary>
    public bool IsLoadingGilflux { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the Universalis API answered the last check,
    /// or null while the first check is still running.
    /// </summary>
    public bool? IsUniversalisUp => this.status.IsUniversalisUp;

    /// <summary>
    /// Gets a value indicating whether the FFXIVMT API answered the last check,
    /// or null while the first check is still running.
    /// </summary>
    public bool? IsFFXIVMTUp => this.status.IsFFXIVMTUp;

    /// <summary>
    /// Empties the shared market data cache.
    /// </summary>
    public void ClearCache()
    {
      this.cache.Clear();
    }

    /// <summary>
    /// Starts fetching market data and gilflux rankings for an item, cancelling any fetch already in flight.
    /// </summary>
    /// <param name="item">The item to fetch data for.</param>
    /// <param name="queryTarget">The world, data centre or region to query.</param>
    /// <param name="includeOceania">True to merge the Oceania data centre's listings into the result.</param>
    public void Refresh(Item item, string queryTarget, bool includeOceania)
    {
      this.MarketData = null;
      this.Gilflux = null;

      if (this.currentRefreshTask?.Status != TaskStatus.RanToCompletion)
      {
        this.plugin.Log.Debug("Cancelling previous refresh task.");
        this.currentRefreshCancellationTokenSource?.Cancel();
      }

      this.currentRefreshCancellationTokenSource?.Dispose();
      this.currentRefreshCancellationTokenSource = new CancellationTokenSource();

      var itemId = item.RowId;
      var cancellationTokenSource = this.currentRefreshCancellationTokenSource;

      this.currentRefreshTask = Task.Run(
        async () =>
        {
          var cached = this.cache.Get(itemId, queryTarget);

          if (cached != null)
          {
            this.MarketData = cached;
            return;
          }

          try
          {
            this.MarketData = await this.plugin.UniversalisClient
              .GetMarketData(
                itemId,
                queryTarget,
                this.plugin.Config.ListingCount,
                this.plugin.Config.HistoryCount,
                cancellationTokenSource.Token)
              .ConfigureAwait(false);

            if (includeOceania)
            {
              await this.MergeOceania(itemId, cancellationTokenSource.Token).ConfigureAwait(false);
            }
          }
          catch (AggregateException ae)
          {
            this.plugin.Log.Warning(ae, $"Failed to fetch market data for item {itemId} from Universalis.");

            foreach (var ex in ae.InnerExceptions)
            {
              this.plugin.Log.Warning(ex, "Inner exception");
            }

            this.MarketData = null;
          }

          if (this.MarketData != null)
          {
            this.cache.Put(itemId, queryTarget, this.MarketData);
          }

          this.IsLoadingGilflux = true;

          try
          {
            this.Gilflux = await this.plugin.FFXIVMTClient
              .GetGilfluxForItem(itemId, queryTarget, cancellationTokenSource.Token)
              .ConfigureAwait(false);
          }
          catch (OperationCanceledException)
          {
            this.Gilflux = null;
          }
          catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
          {
            this.plugin.Log.Warning(ex, "Failed to fetch FFXIVMT gilflux data.");
            this.Gilflux = null;
          }
          finally
          {
            this.IsLoadingGilflux = false;
          }
        },
        cancellationTokenSource.Token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.currentRefreshCancellationTokenSource?.Cancel();
      this.currentRefreshCancellationTokenSource?.Dispose();
      this.isDisposed = true;
    }

    private async Task MergeOceania(uint itemId, CancellationToken cancellationToken)
    {
      var oceania = await this.plugin.UniversalisClient
        .GetMarketData(
          itemId,
          WorldRegions.Oceania,
          this.plugin.Config.ListingCount,
          this.plugin.Config.HistoryCount,
          cancellationToken)
        .ConfigureAwait(false);

      if (oceania == null)
      {
        return;
      }

      if (this.MarketData == null)
      {
        this.MarketData = oceania;
        return;
      }

      foreach (var listing in oceania.Listings)
      {
        this.MarketData.Listings.Add(listing);
      }

      foreach (var history in oceania.RecentHistory)
      {
        this.MarketData.RecentHistory.Add(history);
      }

      this.MarketData.Listings = this.MarketData.Listings
        .OrderBy(l => l.PricePerUnit)
        .Take(this.plugin.Config.ListingCount)
        .ToList();
      this.MarketData.RecentHistory = this.MarketData.RecentHistory
        .OrderByDescending(h => h.Timestamp)
        .Take(this.plugin.Config.HistoryCount)
        .ToList();
    }
  }
}
