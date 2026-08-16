// <copyright file="BuyRequest.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>
  /// The listing a buy attempt is allowed to take off the Market Board.
  /// </summary>
  public sealed class BuyRequest
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="BuyRequest"/> class.
    /// </summary>
    /// <param name="itemId">The row id of the item to buy.</param>
    /// <param name="itemName">The name of the item, used to check the confirmation prompt.</param>
    /// <param name="hq">The quality the listing has to have.</param>
    /// <param name="quantity">The stack size the listing has to have.</param>
    /// <param name="maxUnitPrice">The highest per-unit price that may be paid.</param>
    public BuyRequest(uint itemId, string itemName, bool hq, long quantity, double maxUnitPrice)
    {
      this.ItemId = itemId;
      this.ItemName = itemName;
      this.Hq = hq;
      this.Quantity = quantity;
      this.MaxUnitPrice = maxUnitPrice;
    }

    /// <summary>Gets the row id of the item to buy.</summary>
    public uint ItemId { get; }

    /// <summary>Gets the name of the item.</summary>
    public string ItemName { get; }

    /// <summary>Gets a value indicating whether the listing has to be high quality.</summary>
    public bool Hq { get; }

    /// <summary>Gets the stack size the listing has to have.</summary>
    public long Quantity { get; }

    /// <summary>Gets the highest per-unit price that may be paid.</summary>
    public double MaxUnitPrice { get; }

    /// <summary>Gets the most gil the whole purchase may cost.</summary>
    public double TotalLimit => this.MaxUnitPrice * this.Quantity;
  }
}
