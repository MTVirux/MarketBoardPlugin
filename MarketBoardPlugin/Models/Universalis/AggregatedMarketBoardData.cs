// <copyright file="AggregatedMarketBoardData.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Models.Universalis
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Text.Json.Serialization;

  /// <summary>
  /// Aggregated market board data model for Universalis aggregated endpoints.
  /// </summary>
  public class AggregatedMarketBoardData
  {
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    [JsonPropertyName("itemID")]
    public long ItemId { get; set; }

    /// <summary>
    /// Gets or sets the per-world / per-dc / per-region results.
    /// </summary>
    [JsonPropertyName("results")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for JSON deserialization")]
    public IList<AggregatedResult> Results { get; set; } = new List<AggregatedResult>();

    /// <summary>
    /// Gets the world upload times map.
    /// </summary>
    [JsonPropertyName("worldUploadTimes")]
    public Dictionary<string, long> WorldUploadTimes { get; } = new Dictionary<string, long>();
  }
}
