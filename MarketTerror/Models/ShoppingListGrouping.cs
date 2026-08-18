// <copyright file="ShoppingListGrouping.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models
{
  /// <summary>
  /// What the shopping list tree groups by at its top level.
  /// </summary>
  public enum ShoppingListGrouping
  {
    /// <summary>Items at the top, the scopes they are wanted in underneath.</summary>
    ItemFirst = 0,

    /// <summary>Scopes at the top, the items wanted in them underneath.</summary>
    ScopeFirst = 1,
  }
}
