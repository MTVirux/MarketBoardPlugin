// <copyright file="ListingKind.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>
  /// How a shopping list entry chooses the listings it buys.
  /// </summary>
  public enum ListingKind
  {
    /// <summary>The cheapest listings in the entry's scope, however many the entry asks for.</summary>
    Lowest = 0,

    /// <summary>One particular listing, kept as it is rather than chosen again.</summary>
    Direct = 1,

    /// <summary>Every listing in the scope a set of conditions holds for.</summary>
    Conditional = 2,
  }
}
