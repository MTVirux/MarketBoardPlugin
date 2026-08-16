// <copyright file="QueryStats.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>
  /// What the last finished pricing job asked for, as it is written to the plugin configuration.
  /// </summary>
  public class QueryStats
  {
    /// <summary>
    /// Gets or sets the number of items the job queried.
    /// </summary>
    public int Items { get; set; }

    /// <summary>
    /// Gets or sets the number of requests the job sent.
    /// </summary>
    public int Queries { get; set; }

    /// <summary>
    /// Gets or sets the worlds, data centres or regions the job priced at.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how long the job took, from its first request to its last answer.
    /// </summary>
    public long Milliseconds { get; set; }
  }
}
