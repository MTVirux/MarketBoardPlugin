// <copyright file="DyeFilter.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>Whether a condition wants a listing dyed.</summary>
  public enum DyeFilter
  {
    /// <summary>Dyed or not.</summary>
    Any = 0,

    /// <summary>Listings carrying no dye.</summary>
    Undyed = 1,

    /// <summary>Listings carrying any dye.</summary>
    Dyed = 2,

    /// <summary>Listings carrying one particular dye.</summary>
    Specific = 3,
  }
}
