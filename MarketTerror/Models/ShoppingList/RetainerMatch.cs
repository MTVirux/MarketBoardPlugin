// <copyright file="RetainerMatch.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>How a retainer name condition is compared.</summary>
  public enum RetainerMatch
  {
    /// <summary>The name holds the text somewhere in it.</summary>
    Contains = 0,

    /// <summary>The name is exactly the text.</summary>
    Is = 1,
  }
}
