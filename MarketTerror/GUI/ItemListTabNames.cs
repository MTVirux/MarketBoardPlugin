// <copyright file="ItemListTabNames.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  /// <summary>
  /// The names the item lists are shown under.
  /// </summary>
  public static class ItemListTabNames
  {
    /// <summary>
    /// The name of a list, as it appears in a detached window's title.
    /// </summary>
    /// <param name="tab">The list.</param>
    /// <returns>The display name.</returns>
    public static string Label(ItemListTab tab)
    {
      return tab switch
      {
        ItemListTab.Search => "Search results",
        ItemListTab.Favorites => "Favorites",
        ItemListTab.History => "Recently viewed",
        _ => "All items",
      };
    }
  }
}
