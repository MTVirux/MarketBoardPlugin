// <copyright file="StoredListing.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;

  /// <summary>
  /// One of a shopping list entry's listings, as it is written to the plugin configuration.
  /// </summary>
  public class StoredListing
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="StoredListing"/> class.
    /// </summary>
    public StoredListing()
    {
      this.World = string.Empty;
      this.RetainerName = string.Empty;
      this.ListingId = string.Empty;
      this.CreatorName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoredListing"/> class from a resolved listing.
    /// </summary>
    /// <param name="listing">The listing to write out.</param>
    public StoredListing(ResolvedListing listing)
    {
      ArgumentNullException.ThrowIfNull(listing);

      this.Price = listing.Price;
      this.Quantity = listing.Quantity;
      this.Hq = listing.Hq;
      this.World = listing.World;
      this.RetainerName = listing.RetainerName;
      this.ListingId = listing.ListingId;
      this.Gone = listing.Gone;
      this.OnMannequin = listing.OnMannequin;
      this.IsCrafted = listing.IsCrafted;
      this.CreatorName = listing.CreatorName;
      this.MateriaCount = listing.MateriaCount;
      this.StainId = listing.StainId;
    }

    /// <summary>Gets or sets the price per unit.</summary>
    public double Price { get; set; }

    /// <summary>Gets or sets the stack size.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets a value indicating whether the listing is high quality.</summary>
    public bool Hq { get; set; }

    /// <summary>Gets or sets the world the listing is on.</summary>
    public string World { get; set; }

    /// <summary>Gets or sets the name of the retainer selling it.</summary>
    public string RetainerName { get; set; }

    /// <summary>Gets or sets the Universalis listing id.</summary>
    public string ListingId { get; set; }

    /// <summary>Gets or sets a value indicating whether the last refresh could not find it.</summary>
    public bool Gone { get; set; }

    /// <summary>Gets or sets a value indicating whether it is sold off a mannequin.</summary>
    public bool OnMannequin { get; set; }

    /// <summary>Gets or sets a value indicating whether it was put up by a crafter.</summary>
    public bool IsCrafted { get; set; }

    /// <summary>Gets or sets the name of the crafter.</summary>
    public string CreatorName { get; set; }

    /// <summary>Gets or sets how many materia are melded in.</summary>
    public int MateriaCount { get; set; }

    /// <summary>Gets or sets the dye on it, or 0 for none.</summary>
    public uint StainId { get; set; }

    /// <summary>
    /// Reads the stored listing back.
    /// </summary>
    /// <returns>The listing.</returns>
    public ResolvedListing ToListing()
    {
      return new ResolvedListing(this.Price, this.Quantity, this.Hq, this.World, this.RetainerName, this.ListingId)
      {
        Gone = this.Gone,
        OnMannequin = this.OnMannequin,
        IsCrafted = this.IsCrafted,
        CreatorName = this.CreatorName,
        MateriaCount = this.MateriaCount,
        StainId = this.StainId,
      };
    }
  }
}
