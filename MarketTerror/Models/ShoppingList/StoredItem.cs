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
    public StoredItem(uint itemId, double price, string world)
    {
      this.ItemId = itemId;
      this.Price = price;
      this.World = world;
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
  }
}
