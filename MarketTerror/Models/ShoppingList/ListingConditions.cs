// <copyright file="ListingConditions.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Globalization;
  using System.Linq;

  /// <summary>
  /// What a conditional shopping list entry will buy: every listing in its scope that each set
  /// condition holds for, cheapest first, for as long as the caps allow.
  /// </summary>
  /// <remarks>
  /// An unset field is not a condition. A rule with nothing set buys nothing rather than everything
  /// in the scope, which is never what was meant.
  /// </remarks>
  public sealed class ListingConditions
  {
    /// <summary>Gets or sets the least that may be paid per unit, or 0 for no floor.</summary>
    public double MinUnitPrice { get; set; }

    /// <summary>Gets or sets the most that may be paid per unit, or 0 for no ceiling.</summary>
    public double MaxUnitPrice { get; set; }

    /// <summary>Gets or sets the most one listing may cost in all, or 0 for no ceiling.</summary>
    public double MaxListingTotal { get; set; }

    /// <summary>Gets or sets the smallest stack that will do, or 0 for any.</summary>
    public long MinQuantity { get; set; }

    /// <summary>Gets or sets the largest stack that will do, or 0 for any.</summary>
    public long MaxQuantity { get; set; }

    /// <summary>Gets or sets which qualities are accepted.</summary>
    public QualityFilter Quality { get; set; }

    /// <summary>Gets or sets the worlds inside the scope that are accepted, or none for all of them.</summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Rewritten wholesale by the editor")]
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Rewritten wholesale by the editor")]
    public List<string> Worlds { get; set; } = new List<string>();

    /// <summary>Gets or sets the retainer name to look for, or an empty string for any.</summary>
    public string RetainerName { get; set; } = string.Empty;

    /// <summary>Gets or sets how <see cref="RetainerName"/> is compared.</summary>
    public RetainerMatch RetainerMatch { get; set; }

    /// <summary>Gets or sets whether the listing has to have been crafted.</summary>
    public CraftedFilter Crafted { get; set; }

    /// <summary>Gets or sets the crafter name to look for, or an empty string for any.</summary>
    public string CreatorName { get; set; } = string.Empty;

    /// <summary>Gets or sets the fewest materia that will do, or 0 for any.</summary>
    public int MinMateria { get; set; }

    /// <summary>Gets or sets the most materia that will do, or 0 for any.</summary>
    public int MaxMateria { get; set; }

    /// <summary>Gets or sets whether the listing has to be dyed.</summary>
    public DyeFilter Dye { get; set; }

    /// <summary>Gets or sets the dye to require while <see cref="Dye"/> is <see cref="DyeFilter.Specific"/>.</summary>
    public uint StainId { get; set; }

    /// <summary>Gets or sets how many listings the entry takes at most, or 0 for no cap.</summary>
    public int MaxListings { get; set; }

    /// <summary>Gets or sets the most the entry may cost in all, or 0 for no cap.</summary>
    public double MaxSpend { get; set; }

    /// <summary>
    /// Gets a value indicating whether anything at all has been set.
    /// </summary>
    public bool IsSet =>
      this.MinUnitPrice > 0 || this.MaxUnitPrice > 0 || this.MaxListingTotal > 0 ||
      this.MinQuantity > 0 || this.MaxQuantity > 0 || this.Quality != QualityFilter.Any ||
      this.Worlds.Count > 0 || this.RetainerName.Length > 0 ||
      this.Crafted != CraftedFilter.Any || this.CreatorName.Length > 0 ||
      this.MinMateria > 0 || this.MaxMateria > 0 || this.Dye != DyeFilter.Any ||
      this.MaxListings > 0 || this.MaxSpend > 0;

    /// <summary>
    /// Checks one listing against every condition that has been set.
    /// </summary>
    /// <param name="listing">The listing to check.</param>
    /// <returns>True when the listing may be bought.</returns>
    public bool Matches(ResolvedListing listing)
    {
      ArgumentNullException.ThrowIfNull(listing);

      return this.PriceHolds(listing)
        && this.StackHolds(listing)
        && this.QualityHolds(listing)
        && this.SellerHolds(listing)
        && this.CraftHolds(listing)
        && this.DyeHolds(listing);
    }

    /// <summary>
    /// Copies the rule, so a draft being edited is not the one an entry is using.
    /// </summary>
    /// <returns>The copy.</returns>
    public ListingConditions Clone()
    {
      return new ListingConditions
      {
        MinUnitPrice = this.MinUnitPrice,
        MaxUnitPrice = this.MaxUnitPrice,
        MaxListingTotal = this.MaxListingTotal,
        MinQuantity = this.MinQuantity,
        MaxQuantity = this.MaxQuantity,
        Quality = this.Quality,
        Worlds = new List<string>(this.Worlds),
        RetainerName = this.RetainerName,
        RetainerMatch = this.RetainerMatch,
        Crafted = this.Crafted,
        CreatorName = this.CreatorName,
        MinMateria = this.MinMateria,
        MaxMateria = this.MaxMateria,
        Dye = this.Dye,
        StainId = this.StainId,
        MaxListings = this.MaxListings,
        MaxSpend = this.MaxSpend,
      };
    }

    /// <summary>
    /// Sums the rule up short enough to sit on a table row.
    /// </summary>
    /// <returns>The summary, or "no conditions" when nothing is set.</returns>
    public string Summary()
    {
      var parts = new List<string>();

      if (this.MinUnitPrice > 0 && this.MaxUnitPrice > 0)
      {
        parts.Add(FormattableString.Invariant($"{this.MinUnitPrice:N0}-{this.MaxUnitPrice:N0}g"));
      }
      else if (this.MaxUnitPrice > 0)
      {
        parts.Add(FormattableString.Invariant($"≤{this.MaxUnitPrice:N0}g"));
      }
      else if (this.MinUnitPrice > 0)
      {
        parts.Add(FormattableString.Invariant($"≥{this.MinUnitPrice:N0}g"));
      }

      if (this.MaxListingTotal > 0)
      {
        parts.Add(FormattableString.Invariant($"listing ≤{this.MaxListingTotal:N0}g"));
      }

      if (this.MinQuantity > 0 && this.MaxQuantity > 0)
      {
        parts.Add(FormattableString.Invariant($"x{this.MinQuantity}-{this.MaxQuantity}"));
      }
      else if (this.MaxQuantity > 0)
      {
        parts.Add(FormattableString.Invariant($"x≤{this.MaxQuantity}"));
      }
      else if (this.MinQuantity > 0)
      {
        parts.Add(FormattableString.Invariant($"x≥{this.MinQuantity}"));
      }

      if (this.Quality != QualityFilter.Any)
      {
        parts.Add(this.Quality.Label());
      }

      if (this.Worlds.Count > 0)
      {
        parts.Add(this.Worlds.Count == 1
          ? this.Worlds[0]
          : this.Worlds.Count.ToString(CultureInfo.CurrentCulture) + " worlds");
      }

      if (this.RetainerName.Length > 0)
      {
        parts.Add(this.RetainerName);
      }

      if (this.Crafted != CraftedFilter.Any)
      {
        parts.Add(this.Crafted == CraftedFilter.CraftedOnly ? "crafted" : "not crafted");
      }

      if (this.CreatorName.Length > 0)
      {
        parts.Add("by " + this.CreatorName);
      }

      if (this.MinMateria > 0 || this.MaxMateria > 0)
      {
        parts.Add(this.MinMateria > 0 && this.MaxMateria > 0
          ? FormattableString.Invariant($"{this.MinMateria}-{this.MaxMateria} materia")
          : FormattableString.Invariant($"{Math.Max(this.MinMateria, this.MaxMateria)} materia"));
      }

      if (this.Dye != DyeFilter.Any)
      {
        parts.Add(this.Dye switch
        {
          DyeFilter.Undyed => "undyed",
          DyeFilter.Dyed => "dyed",
          _ => "one dye",
        });
      }

      if (this.MaxListings > 0)
      {
        parts.Add(FormattableString.Invariant($"first {this.MaxListings}"));
      }

      if (this.MaxSpend > 0)
      {
        parts.Add(FormattableString.Invariant($"up to {this.MaxSpend:N0}g"));
      }

      return parts.Count == 0 ? "no conditions" : string.Join(", ", parts);
    }

    private bool PriceHolds(ResolvedListing listing)
    {
      return (this.MinUnitPrice <= 0 || listing.Price >= this.MinUnitPrice)
        && (this.MaxUnitPrice <= 0 || listing.Price <= this.MaxUnitPrice)
        && (this.MaxListingTotal <= 0 || listing.Total <= this.MaxListingTotal);
    }

    private bool StackHolds(ResolvedListing listing)
    {
      return (this.MinQuantity <= 0 || listing.Quantity >= this.MinQuantity)
        && (this.MaxQuantity <= 0 || listing.Quantity <= this.MaxQuantity);
    }

    private bool QualityHolds(ResolvedListing listing)
    {
      return this.Quality.Accepts(listing.Hq);
    }

    private bool SellerHolds(ResolvedListing listing)
    {
      if (this.Worlds.Count > 0 &&
          !this.Worlds.Any(w => string.Equals(w, listing.World, StringComparison.OrdinalIgnoreCase)))
      {
        return false;
      }

      if (this.RetainerName.Length == 0)
      {
        return true;
      }

      return this.RetainerMatch == RetainerMatch.Is
        ? string.Equals(listing.RetainerName, this.RetainerName, StringComparison.OrdinalIgnoreCase)
        : listing.RetainerName.Contains(this.RetainerName, StringComparison.OrdinalIgnoreCase);
    }

    private bool CraftHolds(ResolvedListing listing)
    {
      var crafted = this.Crafted switch
      {
        CraftedFilter.CraftedOnly => listing.IsCrafted,
        CraftedFilter.UncraftedOnly => !listing.IsCrafted,
        _ => true,
      };

      if (!crafted)
      {
        return false;
      }

      if (this.CreatorName.Length > 0 &&
          !listing.CreatorName.Contains(this.CreatorName, StringComparison.OrdinalIgnoreCase))
      {
        return false;
      }

      return (this.MinMateria <= 0 || listing.MateriaCount >= this.MinMateria)
        && (this.MaxMateria <= 0 || listing.MateriaCount <= this.MaxMateria);
    }

    private bool DyeHolds(ResolvedListing listing)
    {
      return this.Dye switch
      {
        DyeFilter.Undyed => listing.StainId == 0,
        DyeFilter.Dyed => listing.StainId != 0,
        DyeFilter.Specific => listing.StainId == this.StainId,
        _ => true,
      };
    }
  }
}
