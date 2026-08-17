// <copyright file="ItemListTab.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  /// <summary>
  /// The lists the item panel can show.
  /// </summary>
  public enum ItemListTab
  {
    /// <summary>
    /// The whole item catalogue.
    /// </summary>
    All,

    /// <summary>
    /// The items matching the search string.
    /// </summary>
    Search,

    /// <summary>
    /// The item lists the user has made.
    /// </summary>
    /// <remarks>
    /// Took the place of the favourites list, whose ordinal it keeps so saved windows still load.
    /// </remarks>
    Lists,

    /// <summary>
    /// The recently viewed items.
    /// </summary>
    History,
  }
}
