// <copyright file="CraftedFilter.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>Whether a condition wants a listing put up by a crafter.</summary>
  public enum CraftedFilter
  {
    /// <summary>Crafted or not.</summary>
    Any = 0,

    /// <summary>Crafted listings only.</summary>
    CraftedOnly = 1,

    /// <summary>Listings that were not crafted.</summary>
    UncraftedOnly = 2,
  }
}
