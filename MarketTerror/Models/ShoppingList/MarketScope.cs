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
    World = 0,

    /// <summary>
    /// Every world on the selected world's data centre.
    /// </summary>
    DataCentre = 1,

    /// <summary>
    /// Every data centre in the selected world's region.
    /// </summary>
    Region = 2,

    /// <summary>
    /// The selected world's region, with the Oceania data centre priced alongside it.
    /// </summary>
    RegionWithOceania = 3,
  }
}
