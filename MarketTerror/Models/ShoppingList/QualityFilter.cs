// <copyright file="QualityFilter.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>Which qualities of a listing a condition accepts.</summary>
  public enum QualityFilter
  {
    /// <summary>Both qualities.</summary>
    Any = 0,

    /// <summary>High quality listings only.</summary>
    HqOnly = 1,

    /// <summary>Normal quality listings only.</summary>
    NqOnly = 2,
  }
}
