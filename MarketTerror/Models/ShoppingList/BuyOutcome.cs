// <copyright file="BuyOutcome.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>
  /// How the last buy attempt on a shopping list row ended.
  /// </summary>
  public enum BuyOutcome
  {
    /// <summary>Nothing has been bought since the row was last priced.</summary>
    None,

    /// <summary>The item was bought at the price the row was saved at.</summary>
    Bought,

    /// <summary>The item was bought below the price the row was saved at.</summary>
    BoughtCheaper,

    /// <summary>Some of the row's picked listings were bought and some were not.</summary>
    PartlyBought,

    /// <summary>Nothing was bought.</summary>
    Failed,
  }
}
