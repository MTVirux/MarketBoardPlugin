// <copyright file="GilfluxResponse.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Models.FFXIVMT
{
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
    /// Gets or sets the JSON-encoded gilflux ranking data.
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets the JSON-encoded gilflux timeframe definitions in milliseconds.
    /// </summary>
    [JsonPropertyName("gilflux_timeframe_in_ms")]
    public string? GilfluxTimeframeInMs { get; set; }

    /// <summary>
    /// Gets or sets the request ID.
    /// </summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
  }
}
