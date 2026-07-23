// <copyright file="AggregatedResult.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Models.Universalis
{
  using System.Text.Json.Serialization;

  /// <summary>
  /// Represents an aggregated result for a world / datacenter / region.
  /// </summary>
  public class AggregatedResult
  {
    /// <summary>
    /// Gets or sets the name of the world.
    /// </summary>
    [JsonPropertyName("worldName")]
    public string? WorldName { get; set; }

    /// <summary>
    /// Gets or sets the name of the data center.
    /// </summary>
    [JsonPropertyName("dcName")]
    public string? DcName { get; set; }

    /// <summary>
    /// Gets or sets the name of the region.
    /// </summary>
    [JsonPropertyName("regionName")]
    public string? RegionName { get; set; }

    /// <summary>
    /// Gets or sets the lowest current listing price.
    /// </summary>
    [JsonPropertyName("minListing")]
    public long MinListing { get; set; }

    /// <summary>
    /// Gets or sets the median current listing price.
    /// </summary>
    [JsonPropertyName("medianListing")]
    public long MedianListing { get; set; }

    /// <summary>
    /// Gets or sets the average sale price.
    /// </summary>
    [JsonPropertyName("averageSalePrice")]
    public double AverageSalePrice { get; set; }

    /// <summary>
    /// Gets or sets the daily sale velocity.
    /// </summary>
    [JsonPropertyName("dailySaleVelocity")]
    public double DailySaleVelocity { get; set; }

    /// <summary>
    /// Gets or sets the last upload time.
    /// </summary>
    [JsonPropertyName("lastUploadTime")]
    public long LastUploadTime { get; set; }
  }
}
