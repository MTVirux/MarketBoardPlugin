// <copyright file="StoredPick.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;

  /// <summary>
  /// A picked listing as it is written to the plugin configuration.
  /// </summary>
  public class StoredPick
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="StoredPick"/> class.
    /// </summary>
    public StoredPick()
    {
      this.World = string.Empty;
      this.RetainerName = string.Empty;
      this.ListingId = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoredPick"/> class from a pick.
    /// </summary>
    /// <param name="pick">The pick to store.</param>
    public StoredPick(PickedListing pick)
    {
      ArgumentNullException.ThrowIfNull(pick);

      this.Price = pick.Price;
      this.Quantity = pick.Quantity;
      this.Hq = pick.Hq;
      this.World = pick.World;
      this.RetainerName = pick.RetainerName;
      this.ListingId = pick.ListingId;
      this.Gone = pick.Gone;
    }

    /// <summary>Gets or sets the price per unit.</summary>
    public double Price { get; set; }

    /// <summary>Gets or sets the stack size of the listing.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets a value indicating whether the listing is high quality.</summary>
    public bool Hq { get; set; }

    /// <summary>Gets or sets the world the listing is on.</summary>
    public string World { get; set; }

    /// <summary>Gets or sets the name of the retainer selling it.</summary>
    public string RetainerName { get; set; }

    /// <summary>Gets or sets the Universalis listing id.</summary>
    public string ListingId { get; set; }

    /// <summary>Gets or sets a value indicating whether the last refresh could not find the listing.</summary>
    public bool Gone { get; set; }

    /// <summary>
    /// Rebuilds the pick this was stored from.
    /// </summary>
    /// <returns>The pick.</returns>
    public PickedListing ToPick()
    {
      return new PickedListing(this.Price, this.Quantity, this.Hq, this.World, this.RetainerName, this.ListingId)
      {
        Gone = this.Gone,
      };
    }
  }
}
