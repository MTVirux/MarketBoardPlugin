// <copyright file="UniversalisClient.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Net.Http;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using MarketTerror.Models.Universalis;
  using Polly;

  /// <summary>
  /// Universalis API Client.
  /// </summary>
  /// <remarks>
  /// Initializes a new instance of the <see cref="UniversalisClient"/> class.
  /// </remarks>
  public class UniversalisClient : IDisposable
  {
    /// <summary>
    /// The number of item ids Universalis accepts in one request.
    /// </summary>
    /// <remarks>
    /// Ids past this limit are dropped silently: the response still comes back as a 200 and mentions
    /// neither the extra ids nor that anything was left out, so the caller has to do the chunking.
    /// </remarks>
    public const int MaxItemsPerRequest = 100;

    /// <summary>
    /// The response fields a shopping list entry is built from. The retainer and listing ids are only in
    /// there because <see cref="MarketDataListing"/> marks them required, so leaving them out fails the parse.
    /// </summary>
    /// <remarks>
    /// A field left out of this list is not missing from the parse, it comes back as its default, so
    /// leaving the quality out quietly calls every listing normal quality and buys the wrong one.
    /// </remarks>
    private const string CheapestListingFields =
      "items.listings.pricePerUnit,items.listings.quantity,items.listings.hq,items.listings.tax," +
      "items.listings.worldName,items.listings.listingID,items.listings.retainerID,items.listings.retainerName";

    private readonly MarketTerrorPlugin plugin;

    private readonly HttpClient client;

    private readonly ResiliencePipeline resiliencePipeline;

    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="UniversalisClient"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/> instance.</param>
    /// <exception cref="ArgumentNullException">One of the required arguments is null.</exception>
    public UniversalisClient(MarketTerrorPlugin plugin)
    {
      ArgumentNullException.ThrowIfNull(plugin);

      this.plugin = plugin;

      this.client = new HttpClient
      {
        BaseAddress = new Uri("https://universalis.app/api/v2/"),
      };
      this.client.DefaultRequestHeaders.UserAgent.ParseAdd($"MarketTerror/{this.plugin.PluginInterface.Manifest.AssemblyVersion}");

      this.resiliencePipeline = new ResiliencePipelineBuilder()
        .AddRetry(new()
        {
          BackoffType = DelayBackoffType.Exponential,
          MaxRetryAttempts = 3,
          ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
          UseJitter = true,
        })
        .Build();
    }

    /// <summary>
    /// Retrieves market data for a specific item from the Universalis API.
    /// </summary>
    /// <param name="itemId">The ID of the item to retrieve market data for.</param>
    /// <param name="worldName">The name of the world to retrieve market data from.</param>
    /// <param name="listingCount">The number of current listings to retrieve.</param>
    /// <param name="historyCount">The number of historical entries to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MarketDataResponse"/> object containing the retrieved market data, or null if the operation fails.</returns>
    public async Task<MarketDataResponse> GetMarketData(uint itemId, string worldName, int listingCount, int historyCount, CancellationToken cancellationToken)
    {
      try
      {
        using var content = await this.resiliencePipeline.ExecuteAsync(
            async (ct) =>
              await this.client.GetStreamAsync(new Uri($"{worldName}/{itemId}?listings={listingCount}&entries={historyCount}", UriKind.Relative), ct).ConfigureAwait(false),
            cancellationToken)
          .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var parsedRes = await JsonSerializer
          .DeserializeAsync<MarketDataResponse>(content, cancellationToken: cancellationToken)
          .ConfigureAwait(false) ?? throw new InvalidOperationException($"Failed to parse market data for item {itemId} on world {worldName}.");

        parsedRes.FetchTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        return parsedRes;
      }
      catch (HttpRequestException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to fetch market data for item {itemId} on world {worldName}.");
        throw;
      }
      catch (JsonException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to parse market data for item {itemId} on world {worldName}.");
        throw;
      }
    }

    /// <summary>
    /// Retrieves the cheapest current listing of several items in a single request.
    /// </summary>
    /// <param name="itemIds">The item ids to retrieve listings for, at most <see cref="MaxItemsPerRequest"/> of them.</param>
    /// <param name="worldName">The world, data centre or region to retrieve market data from.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The market data of the items that resolved, keyed by item id. Items with no listings are left out.</returns>
    /// <exception cref="ArgumentException">More ids than <see cref="MaxItemsPerRequest"/> were passed.</exception>
    public async Task<IReadOnlyDictionary<uint, MarketDataResponse>> GetCheapestListings(
      IReadOnlyCollection<uint> itemIds,
      string worldName,
      CancellationToken cancellationToken)
    {
      ArgumentNullException.ThrowIfNull(itemIds);

      if (itemIds.Count > MaxItemsPerRequest)
      {
        throw new ArgumentException(
          $"Universalis silently drops ids past {MaxItemsPerRequest} per request, so the caller has to chunk.",
          nameof(itemIds));
      }

      if (itemIds.Count == 0)
      {
        return new Dictionary<uint, MarketDataResponse>();
      }

      // A lone id makes Universalis answer with the single item shape, which has no "items" map to read.
      if (itemIds.Count == 1)
      {
        var onlyId = itemIds.First();
        var single = await this.GetMarketData(onlyId, worldName, 1, 0, cancellationToken).ConfigureAwait(false);

        return new Dictionary<uint, MarketDataResponse> { [onlyId] = single };
      }

      var ids = string.Join(',', itemIds);

      try
      {
        using var content = await this.resiliencePipeline.ExecuteAsync(
            async (ct) =>
              await this.client.GetStreamAsync(
                new Uri($"{worldName}/{ids}?listings=1&entries=0&fields={CheapestListingFields}", UriKind.Relative), ct)
                .ConfigureAwait(false),
            cancellationToken)
          .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var parsedRes = await JsonSerializer
          .DeserializeAsync<MultiItemMarketDataResponse>(content, cancellationToken: cancellationToken)
          .ConfigureAwait(false)
          ?? throw new InvalidOperationException($"Failed to parse market data for {itemIds.Count} items on world {worldName}.");

        var byId = new Dictionary<uint, MarketDataResponse>(parsedRes.Items.Count);

        foreach (var entry in parsedRes.Items)
        {
          if (uint.TryParse(entry.Key, CultureInfo.InvariantCulture, out var itemId))
          {
            byId[itemId] = entry.Value;
          }
        }

        return byId;
      }
      catch (HttpRequestException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to fetch market data for {itemIds.Count} items on world {worldName}.");
        throw;
      }
      catch (JsonException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to parse market data for {itemIds.Count} items on world {worldName}.");
        throw;
      }
    }

    /// <summary>
    /// Retrieves the collection of data centers.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of <see cref="DataCenter"/> objects containing the data centers.</returns>
    public async Task<ICollection<DataCenter>> GetDataCenters(CancellationToken cancellationToken)
    {
      try
      {
        using var content = await this.resiliencePipeline.ExecuteAsync(
            async (ct) =>
              await this.client.GetStreamAsync(new Uri("data-centers", UriKind.Relative), ct).ConfigureAwait(false),
            cancellationToken)
          .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return await JsonSerializer
          .DeserializeAsync<ICollection<DataCenter>>(content, cancellationToken: cancellationToken)
          .ConfigureAwait(false) ?? throw new InvalidOperationException("Failed to parse data centers.");
      }
      catch (HttpRequestException ex)
      {
        this.plugin.Log.Warning(ex, "Failed to fetch data centers.");
        throw;
      }
      catch (JsonException ex)
      {
        this.plugin.Log.Warning(ex, "Failed to parse data centers.");
        throw;
      }
    }

    /// <summary>
    /// Checks if the Universalis API is up and running.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if the Universalis API is up; otherwise, <c>false</c>.</returns>
    public async Task<bool> CheckStatus(CancellationToken cancellationToken)
    {
      try
      {
        await this.GetDataCenters(cancellationToken).ConfigureAwait(false);
      }
      catch (HttpRequestException ex)
      {
        this.plugin.Log.Warning(ex, "Universalis seems down.");
        return false;
      }
      catch (JsonException ex)
      {
        this.plugin.Log.Warning(ex, "Universalis seems down.");
        return false;
      }

      this.plugin.Log.Verbose("Universalis seems up.");
      return true;
    }

    /// <summary>
    /// Retrieves aggregated market data for a specific item from the Universalis API.
    /// </summary>
    /// <param name="itemId">The ID of the item to retrieve aggregated data for.</param>
    /// <param name="worldOrDcOrRegion">The world, datacenter or region scope to query (e.g., "Omega" or "Primal" or "NA").</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="AggregatedMarketBoardData"/> object containing the aggregated data, or null if the operation fails.</returns>
    public async Task<AggregatedMarketBoardData> GetAggregatedMarketData(uint itemId, string worldOrDcOrRegion, CancellationToken cancellationToken)
    {
      try
      {
        using var content = await this.resiliencePipeline.ExecuteAsync(
            async (ct) =>
              await this.client.GetStreamAsync(new Uri($"aggregated/{worldOrDcOrRegion}/{itemId}", UriKind.Relative), ct).ConfigureAwait(false),
            cancellationToken)
          .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var parsedRes = await JsonSerializer
          .DeserializeAsync<AggregatedMarketBoardData>(content, cancellationToken: cancellationToken)
          .ConfigureAwait(false) ?? throw new InvalidOperationException($"Failed to parse aggregated market data for item {itemId} on scope {worldOrDcOrRegion}.");

        return parsedRes;
      }
      catch (HttpRequestException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to fetch aggregated market data for item {itemId} on scope {worldOrDcOrRegion}.");
        throw;
      }
      catch (JsonException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to parse aggregated market data for item {itemId} on scope {worldOrDcOrRegion}.");
        throw;
      }
    }

    /// <summary>
    /// Disposes the Universalis client resources.
    /// </summary>
    public void Dispose()
    {
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
      if (!this.disposedValue)
      {
        if (disposing)
        {
          this.client.Dispose();
        }

        this.disposedValue = true;
      }
    }
  }
}
