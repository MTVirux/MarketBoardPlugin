// <copyright file="StoredItem.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>
  /// A shopping list entry as it is written to the plugin configuration.
  /// </summary>
  /// <remarks>The game item itself cannot be serialised, so only its row id is kept.</remarks>
  public class StoredItem
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="StoredItem"/> class.
    /// </summary>
    public StoredItem()
    {
      this.World = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoredItem"/> class.
    /// </summary>
    /// <param name="itemId">The row id of the saved item.</param>
    /// <param name="price">The price the item was saved at.</param>
    /// <param name="world">The world the price came from.</param>
    /// <param name="unlisted">True when the last refresh found nothing on sale.</param>
    /// <param name="quantity">The stack size of the listing the price came from.</param>
    /// <param name="hq">True when the listing the price came from is high quality.</param>
    public StoredItem(uint itemId, double price, string world, bool unlisted, long quantity, bool hq)
    {
      this.ItemId = itemId;
      this.Price = price;
      this.World = world;
      this.Unlisted = unlisted;
      this.Quantity = quantity;
      this.Hq = hq;
    }

    /// <summary>
    /// Gets or sets the row id of the saved item.
    /// </summary>
    public uint ItemId { get; set; }

    /// <summary>
    /// Gets or sets the price the item was saved at.
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// Gets or sets the world the price came from.
    /// </summary>
    public string World { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the last refresh found nothing on sale.
    /// </summary>
    public bool Unlisted { get; set; }

    /// <summary>
    /// Gets or sets the stack size of the listing the price came from, or 0 when it is unknown.
    /// </summary>
    public long Quantity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the listing the price came from is high quality.
    /// </summary>
    public bool Hq { get; set; }
  }
}
