// <copyright file="WorldRegions.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  /// <summary>
  /// The names Universalis knows the game regions by.
  /// </summary>
  internal static class WorldRegions
  {
    /// <summary>
    /// Oceania sits on its own, so it is only ever priced when it is asked for by name.
    /// </summary>
    public const string Oceania = "Oceania";

    /// <summary>
    /// Resolves the name a region is queried by.
    /// </summary>
    /// <param name="regionRowId">The region row id of a data centre.</param>
    /// <returns>The region name, or an empty string when the region is unknown.</returns>
    public static string GetName(uint regionRowId)
    {
      return regionRowId switch
      {
        1 => "Japan",
        2 => "North-America",
        3 => "Europe",
        4 => Oceania,
        5 => "中国",
        _ => string.Empty,
      };
    }
  }
}
