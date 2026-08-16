// <copyright file="GilfluxResponse.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.FFXIVMT
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Text.Json.Serialization;

  /// <summary>
  /// Envelope response from the FFXIVMT Gilflux API.
  /// </summary>
  public class GilfluxResponse
  {
    /// <summary>
    /// Gets or sets a value indicating whether the request was successful.
    /// </summary>
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the gilflux ranking data, one entry per world in the requested scope.
    /// </summary>
    [JsonPropertyName("data")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for JSON deserialization")]
    public IList<GilfluxRankingItem> Data { get; set; } = new List<GilfluxRankingItem>();

    /// <summary>
    /// Gets or sets the gilflux timeframe definitions in milliseconds, keyed by timeframe name.
    /// </summary>
    [JsonPropertyName("gilflux_timeframe_in_ms")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for JSON deserialization")]
    public IDictionary<string, long> GilfluxTimeframeInMs { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets or sets the request ID.
    /// </summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
  }
}
