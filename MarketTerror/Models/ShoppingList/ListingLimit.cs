// <copyright file="ListingLimit.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using System.Linq;

  /// <summary>
  /// A standing rule that picks a shopping list row's listings for it: every listing at or under a
  /// price per unit, cheapest first, for as long as they fit under a total.
  /// </summary>
  /// <remarks>
  /// A row carrying one of these is picked again from scratch every time it is priced, so it always
  /// buys whatever is under the limit right now rather than the listings that were under it once.
  /// </remarks>
  public sealed class ListingLimit
  {
    /// <summary>
    /// Gets or sets the most that may be paid per unit, or 0 for no limit on the price.
    /// </summary>
    public double MaxUnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the most that may be spent on the row in all, or 0 for no limit on the total.
    /// </summary>
    public double MaxTotal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule looks somewhere other than the shopping list's scope.
    /// </summary>
    public bool HasOwnScope { get; set; }

    /// <summary>
    /// Gets or sets how wide the rule's own scope reaches.
    /// </summary>
    public MarketScope Scope { get; set; }

    /// <summary>
    /// Gets or sets the world the rule's own scope is anchored to.
    /// </summary>
    public string ScopeWorld { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether either limit has been set, without which nothing is picked.
    /// </summary>
    /// <remarks>A rule with neither would buy every listing in the scope, which is never what was meant.</remarks>
    public bool IsSet => this.MaxUnitPrice > 0 || this.MaxTotal > 0;

    /// <summary>
    /// Chooses the listings the rule buys.
    /// </summary>
    /// <param name="listings">Every listing on sale in the rule's scope.</param>
    /// <returns>The listings to buy, cheapest first.</returns>
    /// <remarks>
    /// A listing too big to fit under what is left of the total is passed over rather than ending the
    /// sweep, so a cap with a little room left still fills it with the smaller stacks further down.
    /// </remarks>
    public IEnumerable<PickedListing> Apply(IEnumerable<PickedListing> listings)
    {
      ArgumentNullException.ThrowIfNull(listings);

      if (!this.IsSet)
      {
        yield break;
      }

      var spent = 0d;

      foreach (var listing in listings.Where(l => !l.Gone).OrderBy(l => l.Price).ThenBy(l => l.Total))
      {
        // Sorted by price, so nothing further down is cheap enough either.
        if (this.MaxUnitPrice > 0 && listing.Price > this.MaxUnitPrice)
        {
          yield break;
        }

        if (this.MaxTotal > 0 && spent + listing.Total > this.MaxTotal)
        {
          continue;
        }

        spent += listing.Total;
        yield return listing;
      }
    }

    /// <summary>
    /// Copies the rule, so a draft being edited is not the one the row is using.
    /// </summary>
    /// <returns>The copy.</returns>
    public ListingLimit Clone()
    {
      return new ListingLimit
      {
        MaxUnitPrice = this.MaxUnitPrice,
        MaxTotal = this.MaxTotal,
        HasOwnScope = this.HasOwnScope,
        Scope = this.Scope,
        ScopeWorld = this.ScopeWorld,
      };
    }
  }
}
