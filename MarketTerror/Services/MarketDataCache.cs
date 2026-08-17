// <copyright file="MarketDataCache.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Concurrent;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// The market data every board shares, keyed by the item and the place it was priced at.
  /// </summary>
  /// <remarks>
  /// Keying on the query target as well as the item is what lets two windows sit on different
  /// worlds without one serving the other's prices. The Oceania add-on is part of the key too,
  /// since a region with it merged in queries the same target as the region on its own.
  /// </remarks>
  public sealed class MarketDataCache
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly ConcurrentDictionary<(uint ItemId, string QueryTarget, bool IncludeOceania), MarketDataResponse> entries = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataCache"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public MarketDataCache(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    /// <summary>
    /// Reads a cached response, when there is one recent enough to still be worth serving.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <param name="queryTarget">The world, data centre or region the data was priced at.</param>
    /// <param name="includeOceania">True when the Oceania data centre was merged into the data.</param>
    /// <returns>The cached response, or null when there is none or it has gone stale.</returns>
    public MarketDataResponse? Get(uint itemId, string queryTarget, bool includeOceania)
    {
      if (!this.entries.TryGetValue((itemId, queryTarget, includeOceania), out var entry))
      {
        return null;
      }

      var age = DateTimeOffset.Now.ToUnixTimeMilliseconds() - entry.FetchTimestamp;

      if (age < this.plugin.Config.ItemRefreshTimeout)
      {
        return entry;
      }

      this.entries.TryRemove((itemId, queryTarget, includeOceania), out _);

      return null;
    }

    /// <summary>
    /// Stores a freshly fetched response.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <param name="queryTarget">The world, data centre or region the data was priced at.</param>
    /// <param name="includeOceania">True when the Oceania data centre was merged into the data.</param>
    /// <param name="response">The response to keep.</param>
    public void Put(uint itemId, string queryTarget, bool includeOceania, MarketDataResponse response)
    {
      this.entries[(itemId, queryTarget, includeOceania)] = response;
    }

    /// <summary>
    /// Empties the cache.
    /// </summary>
    public void Clear()
    {
      this.entries.Clear();
    }
  }
}
