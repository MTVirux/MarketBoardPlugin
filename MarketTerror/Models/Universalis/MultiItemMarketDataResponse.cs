// <copyright file="MultiItemMarketDataResponse.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.Universalis
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Text.Json.Serialization;

  /// <summary>
  /// A model representing the market data response Universalis returns when several item ids are requested at once.
  /// </summary>
  /// <remarks>
  /// Items with no market data are left out of <see cref="Items"/> instead of being reported as an error.
  /// </remarks>
  public class MultiItemMarketDataResponse
  {
    /// <summary>
    /// Gets or sets the market data of every item that resolved, keyed by the item id as a string.
    /// </summary>
    [JsonPropertyName("items")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for JSON deserialization")]
    public Dictionary<string, MarketDataResponse> Items { get; set; } = new Dictionary<string, MarketDataResponse>();
  }
}
