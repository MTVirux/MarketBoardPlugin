// <copyright file="SavedItem.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System.Linq;
  using Lumina.Excel.Sheets;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// A model representing an Item saved into the shopping list.
  /// </summary>
  public class SavedItem
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="SavedItem"/> class.
    /// </summary>
    /// <param name="sourceItem"> Item class to save.</param>
    /// <param name="price"> Current cheapest price.</param>
    /// <param name="world"> Current world. </param>
    public SavedItem(Item sourceItem, double price, string world)
    {
      this.SourceItem = sourceItem;
      this.Price = price;
      this.World = world;
    }

    /// <summary>
    ///  Gets or sets original Item Class.
    /// </summary>
    public Item SourceItem { get; set; }

    /// <summary>
    ///  Gets or sets Cheapest price of the item saved.
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    ///  Gets or sets world from where the price attribute was fetched.
    /// </summary>
    public string World { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry is waiting for a new price.
    /// </summary>
    public bool Refreshing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the last refresh found nothing on sale in the scope.
    /// </summary>
    public bool Unlisted { get; set; }

    /// <summary>
    /// Builds an entry from the cheapest listing of a market data response.
    /// </summary>
    /// <param name="sourceItem">The item the market data belongs to.</param>
    /// <param name="marketData">The market data, or null when none could be fetched.</param>
    /// <param name="includeSalesTax">True to fold the gil sales tax into the price.</param>
    /// <param name="fallbackWorld">The world to record when the listing carries none.</param>
    /// <returns>The entry, or null when the item has no listings to buy.</returns>
    public static SavedItem? FromCheapestListing(Item sourceItem, MarketDataResponse? marketData, bool includeSalesTax, string fallbackWorld)
    {
      // The listings can be replaced by a background refresh while this runs.
      var listings = marketData?.Listings.ToArray();

      if (listings == null || listings.Length == 0)
      {
        return null;
      }

      var cheapest = listings.OrderBy(l => l.PricePerUnit).First();
      var price = includeSalesTax
        ? cheapest.PricePerUnit + (cheapest.Tax / cheapest.Quantity)
        : cheapest.PricePerUnit;

      return new SavedItem(sourceItem, price, cheapest.WorldName ?? fallbackWorld);
    }
  }
}
