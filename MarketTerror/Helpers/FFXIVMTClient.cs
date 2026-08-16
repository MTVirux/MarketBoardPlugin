// <copyright file="FFXIVMTClient.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Net.Http;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using MarketTerror.Models.FFXIVMT;
  using Polly;

  /// <summary>
  /// FFXIVMT API Client for retrieving gilflux ranking data.
  /// </summary>
  public class FFXIVMTClient : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly HttpClient client;

    private readonly ResiliencePipeline resiliencePipeline;

    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="FFXIVMTClient"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/> instance.</param>
    /// <exception cref="ArgumentNullException">One of the required arguments is null.</exception>
    public FFXIVMTClient(MarketTerrorPlugin plugin)
    {
      ArgumentNullException.ThrowIfNull(plugin);

      this.plugin = plugin;

      this.client = new HttpClient
      {
        BaseAddress = new Uri("https://mtvirux.app/api/v1/"),
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
    /// Checks whether the FFXIVMT API is answering, using the world list as a cheap probe.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>True when the API responded successfully.</returns>
    public async Task<bool> CheckStatus(CancellationToken cancellationToken)
    {
      try
      {
        using var response = await this.client
          .GetAsync(new Uri("worlds", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
          .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
          this.plugin.Log.Warning($"FFXIVMT seems down, it answered {(int)response.StatusCode}.");
          return false;
        }
      }
      catch (HttpRequestException ex)
      {
        this.plugin.Log.Warning(ex, "FFXIVMT seems down.");
        return false;
      }

      this.plugin.Log.Verbose("FFXIVMT seems up.");
      return true;
    }

    /// <summary>
    /// Retrieves gilflux ranking data for a specific item on the given scope.
    /// </summary>
    /// <param name="itemId">The ID of the item to retrieve gilflux data for.</param>
    /// <param name="scope">The world, datacenter, or region name to query.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="GilfluxRankingItem"/> for the requested item, or null if not found.</returns>
    public async Task<GilfluxRankingItem?> GetGilfluxForItem(uint itemId, string scope, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        using var content = await this.resiliencePipeline.ExecuteAsync(
            async (ct) =>
              await this.client.GetStreamAsync(new Uri($"gilflux/item/{itemId}?target_location={Uri.EscapeDataString(scope)}", UriKind.Relative), ct).ConfigureAwait(false),
            cancellationToken)
          .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var envelope = await JsonSerializer
          .DeserializeAsync<GilfluxResponse>(content, cancellationToken: cancellationToken)
          .ConfigureAwait(false);

        if (envelope?.Status != true || string.IsNullOrEmpty(envelope.Data))
        {
          return null;
        }

        var rankings = JsonSerializer.Deserialize<IList<GilfluxRankingItem>>(envelope.Data);

        return rankings is { Count: > 0 } ? rankings[0] : null;
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (HttpRequestException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to fetch gilflux data for scope {scope}.");
        throw;
      }
      catch (JsonException ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to parse gilflux data for scope {scope}.");
        throw;
      }
    }

    /// <summary>
    /// Disposes the FFXIVMT client resources.
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
