// <copyright file="QualityFilterExtensions.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>Reads a quality filter against a listing.</summary>
  public static class QualityFilterExtensions
  {
    /// <summary>
    /// Checks whether a filter lets a listing of a given quality through.
    /// </summary>
    /// <param name="quality">The filter to read.</param>
    /// <param name="hq">True when the listing is high quality.</param>
    /// <returns>True when the listing is accepted.</returns>
    public static bool Accepts(this QualityFilter quality, bool hq)
    {
      return quality switch
      {
        QualityFilter.HqOnly => hq,
        QualityFilter.NqOnly => !hq,
        _ => true,
      };
    }

    /// <summary>
    /// Names a filter short enough to sit on a button.
    /// </summary>
    /// <param name="quality">The filter to name.</param>
    /// <returns>The label.</returns>
    public static string Label(this QualityFilter quality)
    {
      return quality switch
      {
        QualityFilter.HqOnly => "HQ",
        QualityFilter.NqOnly => "NQ",
        _ => "Any",
      };
    }

    /// <summary>
    /// Gives the filter a click on a cycling button moves to.
    /// </summary>
    /// <param name="quality">The filter being cycled.</param>
    /// <returns>The next filter, wrapping back round to <see cref="QualityFilter.Any"/>.</returns>
    public static QualityFilter Next(this QualityFilter quality)
    {
      return quality switch
      {
        QualityFilter.Any => QualityFilter.HqOnly,
        QualityFilter.HqOnly => QualityFilter.NqOnly,
        _ => QualityFilter.Any,
      };
    }
  }
}
