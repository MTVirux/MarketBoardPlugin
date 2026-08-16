// <copyright file="GilfluxRankingItem.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.FFXIVMT
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Text.Json.Serialization;

  /// <summary>
  /// Represents a single item's gilflux ranking data from the FFXIVMT API.
  /// </summary>
  public class GilfluxRankingItem
  {
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    [JsonPropertyName("item_id")]
    public long ItemId { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    [JsonPropertyName("item_name")]
    public string? ItemName { get; set; }

    /// <summary>
    /// Gets or sets the world ID.
    /// </summary>
    [JsonPropertyName("world_id")]
    public long WorldId { get; set; }

    /// <summary>
    /// Gets or sets the world name.
    /// </summary>
    [JsonPropertyName("world_name")]
    public string? WorldName { get; set; }

    /// <summary>
    /// Gets or sets the datacenter name.
    /// </summary>
    [JsonPropertyName("datacenter")]
    public string? Datacenter { get; set; }

    /// <summary>
    /// Gets or sets the region name.
    /// </summary>
    [JsonPropertyName("region")]
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the gilflux rankings (total gil moved), keyed by timeframe name.
    /// </summary>
    [JsonPropertyName("rankings")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for JSON deserialization")]
    public IDictionary<string, long> Rankings { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets the 1-hour gilflux (total gil moved in last hour).
    /// </summary>
    [JsonIgnore]
    public long Ranking1h => this.GetRanking("1h");

    /// <summary>
    /// Gets the 3-hour gilflux (total gil moved in last 3 hours).
    /// </summary>
    [JsonIgnore]
    public long Ranking3h => this.GetRanking("3h");

    /// <summary>
    /// Gets the 6-hour gilflux (total gil moved in last 6 hours).
    /// </summary>
    [JsonIgnore]
    public long Ranking6h => this.GetRanking("6h");

    /// <summary>
    /// Gets the 12-hour gilflux (total gil moved in last 12 hours).
    /// </summary>
    [JsonIgnore]
    public long Ranking12h => this.GetRanking("12h");

    /// <summary>
    /// Gets the 1-day gilflux (total gil moved in last day).
    /// </summary>
    [JsonIgnore]
    public long Ranking1d => this.GetRanking("1d");

    /// <summary>
    /// Gets the 3-day gilflux (total gil moved in last 3 days).
    /// </summary>
    [JsonIgnore]
    public long Ranking3d => this.GetRanking("3d");

    /// <summary>
    /// Gets the 7-day gilflux (total gil moved in last 7 days).
    /// </summary>
    [JsonIgnore]
    public long Ranking7d => this.GetRanking("7d");

    /// <summary>
    /// Gets or sets the last update timestamp in milliseconds.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last sale timestamp in milliseconds.
    /// </summary>
    [JsonPropertyName("last_sale_time")]
    public long LastSaleTime { get; set; }

    private long GetRanking(string timeframe)
      => this.Rankings != null && this.Rankings.TryGetValue(timeframe, out var value) ? value : 0;
  }
}
