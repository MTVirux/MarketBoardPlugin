// <copyright file="MarketDataProvider.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Lumina.Excel.Sheets;
  using MarketTerror.Helpers;
  using MarketTerror.Models.FFXIVMT;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// Fetches and caches market data, gilflux rankings and the Universalis service status.
  /// </summary>
  public sealed class MarketDataProvider : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly Dictionary<uint, MarketDataResponse> cache = [];

    private readonly CancellationTokenSource statusCheckCancellationTokenSource = new();

    private Task? currentRefreshTask;

    private CancellationTokenSource? currentRefreshCancellationTokenSource;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataProvider"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public MarketDataProvider(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      this.StartStatusCheckTask(this.statusCheckCancellationTokenSource.Token);
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
    /// Gets a value indicating whether the Universalis API responded to the last status check,
    /// or null while the first check is still running.
    /// </summary>
    public bool? IsUniversalisUp { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the FFXIVMT API responded to the last status check,
    /// or null while the first check is still running.
    /// </summary>
    public bool? IsFFXIVMTUp { get; private set; }

    /// <summary>
    /// Empties the market data cache.
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
          var cachedItem = this.cache.GetValueOrDefault(itemId);
          if (
            cachedItem != default(MarketDataResponse)
            && DateTimeOffset.Now.ToUnixTimeMilliseconds() - cachedItem.FetchTimestamp < this.plugin.Config.ItemRefreshTimeout)
          {
            this.MarketData = cachedItem;
            return;
          }

          this.cache.Remove(itemId);

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
              var oceaniaMarketData = await this.plugin.UniversalisClient
                .GetMarketData(
                  itemId,
                  WorldRegions.Oceania,
                  this.plugin.Config.ListingCount,
                  this.plugin.Config.HistoryCount,
                  cancellationTokenSource.Token)
                .ConfigureAwait(false);

              if (oceaniaMarketData != null)
              {
                if (this.MarketData == null)
                {
                  this.MarketData = oceaniaMarketData;
                }
                else
                {
                  foreach (var listing in oceaniaMarketData.Listings)
                  {
                    this.MarketData.Listings.Add(listing);
                  }

                  foreach (var history in oceaniaMarketData.RecentHistory)
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
            this.cache.Add(itemId, this.MarketData);
          }

          this.IsLoadingGilflux = true;

          try
          {
            this.Gilflux = await this.plugin.FFXIVMTClient
              .GetGilfluxForItem(
                itemId,
                queryTarget,
                cancellationTokenSource.Token)
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
      this.statusCheckCancellationTokenSource.Cancel();
      this.statusCheckCancellationTokenSource.Dispose();
      this.isDisposed = true;
    }

    private void StartStatusCheckTask(CancellationToken cancellationToken)
    {
      Task.Run(
        async () =>
        {
          while (!cancellationToken.IsCancellationRequested)
          {
            this.IsUniversalisUp = await this.plugin.UniversalisClient.CheckStatus(cancellationToken).ConfigureAwait(false);
            this.IsFFXIVMTUp = await this.plugin.FFXIVMTClient.CheckStatus(cancellationToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
          }
        },
        cancellationToken);
    }
  }
}
