// <copyright file="MarketScope.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>
  /// How wide a market data query reaches around the selected world.
  /// </summary>
  public enum MarketScope
  {
    /// <summary>
    /// The selected world only.
    /// </summary>
    World,

    /// <summary>
    /// Every world on the selected world's data centre.
    /// </summary>
    DataCentre,

    /// <summary>
    /// Every data centre in the selected world's region.
    /// </summary>
    Region,
  }
}
