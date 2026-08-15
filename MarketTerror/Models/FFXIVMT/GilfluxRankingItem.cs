// <copyright file="GilfluxRankingItem.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.FFXIVMT
{
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
    /// Gets or sets the all-time gilflux ranking (total gil moved).
    /// </summary>
    [JsonPropertyName("ranking_alltime")]
    public long RankingAlltime { get; set; }

    /// <summary>
    /// Gets or sets the 1-hour gilflux (total gil moved in last hour).
    /// </summary>
    [JsonPropertyName("ranking_1h")]
    public long Ranking1h { get; set; }

    /// <summary>
    /// Gets or sets the 3-hour gilflux (total gil moved in last 3 hours).
    /// </summary>
    [JsonPropertyName("ranking_3h")]
    public long Ranking3h { get; set; }

    /// <summary>
    /// Gets or sets the 6-hour gilflux (total gil moved in last 6 hours).
    /// </summary>
    [JsonPropertyName("ranking_6h")]
    public long Ranking6h { get; set; }

    /// <summary>
    /// Gets or sets the 12-hour gilflux (total gil moved in last 12 hours).
    /// </summary>
    [JsonPropertyName("ranking_12h")]
    public long Ranking12h { get; set; }

    /// <summary>
    /// Gets or sets the 1-day gilflux (total gil moved in last day).
    /// </summary>
    [JsonPropertyName("ranking_1d")]
    public long Ranking1d { get; set; }

    /// <summary>
    /// Gets or sets the 3-day gilflux (total gil moved in last 3 days).
    /// </summary>
    [JsonPropertyName("ranking_3d")]
    public long Ranking3d { get; set; }

    /// <summary>
    /// Gets or sets the 7-day gilflux (total gil moved in last 7 days).
    /// </summary>
    [JsonPropertyName("ranking_7d")]
    public long Ranking7d { get; set; }

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
  }
}
