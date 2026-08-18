// <copyright file="ListingResolver.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using System.Linq;

  /// <summary>
  /// Decides which of a market's listings a shopping list entry buys.
  /// </summary>
  /// <remarks>
  /// The one place any of the three kinds is interpreted. Pure on purpose: it takes the listings and
  /// gives back the choice, so nothing about pricing, saving or drawing can change what an entry means.
  /// </remarks>
  public static class ListingResolver
  {
    /// <summary>
    /// Chooses the listings an entry buys.
    /// </summary>
    /// <param name="entry">The entry to resolve.</param>
    /// <param name="onSale">Every listing of the entry's item in the entry's scope.</param>
    /// <returns>The listings to buy, cheapest first.</returns>
    public static IReadOnlyList<ResolvedListing> Resolve(ListingEntry entry, IReadOnlyList<ResolvedListing> onSale)
    {
      ArgumentNullException.ThrowIfNull(entry);
      ArgumentNullException.ThrowIfNull(onSale);

      // Mannequin listings are never bought, whatever the entry asks for.
      var available = onSale
        .Where(l => !l.OnMannequin)
        .OrderBy(l => l.Price)
        .ThenBy(l => l.Total)
        .ToArray();

      return entry.Kind switch
      {
        ListingKind.Lowest => available.Take(Math.Max(1, entry.Count)).ToArray(),
        ListingKind.Direct => Direct(entry, available),
        _ => Conditional(entry, available),
      };
    }

    private static ResolvedListing[] Direct(ListingEntry entry, ResolvedListing[] available)
    {
      if (entry.Target == null)
      {
        return Array.Empty<ResolvedListing>();
      }

      var found = available.FirstOrDefault(l => entry.Target.SameAs(l));

      if (found != null)
      {
        return new[] { found };
      }

      // Keeping the target, marked gone, is what lets the row still say which listing it wanted.
      entry.Target.Gone = true;
      return new[] { entry.Target };
    }

    private static ResolvedListing[] Conditional(ListingEntry entry, ResolvedListing[] available)
    {
      var conditions = entry.Conditions;

      if (conditions == null || !conditions.IsSet)
      {
        return Array.Empty<ResolvedListing>();
      }

      var chosen = new List<ResolvedListing>();
      var spent = 0d;

      foreach (var listing in available.Where(conditions.Matches))
      {
        if (conditions.MaxListings > 0 && chosen.Count >= conditions.MaxListings)
        {
          break;
        }

        // A listing too big for what is left is passed over, so a cap with room in it still fills
        // up from the smaller stacks further down.
        if (conditions.MaxSpend > 0 && spent + listing.Total > conditions.MaxSpend)
        {
          continue;
        }

        spent += listing.Total;
        chosen.Add(listing);
      }

      return chosen.ToArray();
    }
  }
}
