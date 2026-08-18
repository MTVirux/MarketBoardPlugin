// <copyright file="SavedItem.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Linq;
  using Lumina.Excel.Sheets;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// A model representing an Item saved into the shopping list.
  /// </summary>
  /// <remarks>
  /// A row stands for one listing until listings are picked for it, and for the whole basket of picks
  /// after that. The price, stack size, total and world are read off the picks in that case, so the
  /// rest of the plugin can keep asking the row the same four questions either way. Only the picks
  /// still on sale are counted, so a sold out one adds nothing to the row's stack size or total.
  /// </remarks>
  public class SavedItem
  {
    /// <summary>
    /// What the world reads as when a row's picks are spread over more than one of them.
    /// </summary>
    private const string ManyWorldsSuffix = " worlds";

    /// <summary>
    /// Initializes a new instance of the <see cref="SavedItem"/> class.
    /// </summary>
    /// <param name="sourceItem"> Item class to save.</param>
    /// <param name="price"> Current cheapest price.</param>
    /// <param name="world"> Current world. </param>
    /// <param name="quantity">The stack size of the listing the price came from, or 0 when it is unknown.</param>
    /// <param name="hq">True when the listing the price came from is high quality.</param>
    public SavedItem(Item sourceItem, double price, string world, long quantity, bool hq)
    {
      this.SourceItem = sourceItem;
      this.Price = price;
      this.World = world;
      this.Quantity = quantity;
      this.Hq = hq;
    }

    /// <summary>
    ///  Gets or sets original Item Class.
    /// </summary>
    public Item SourceItem { get; set; }

    /// <summary>
    /// Gets the listings this row has been told to buy, or an empty list when it stands for one listing.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Rewritten wholesale whenever the picks change")]
    public List<PickedListing> Picks { get; } = new List<PickedListing>();

    /// <summary>
    /// Gets a value indicating whether listings have been picked for this row.
    /// </summary>
    public bool HasPicks => this.Picks.Count > 0;

    /// <summary>
    /// Gets the picks a buy run may still try, which is every one the last refresh could still find.
    /// </summary>
    public IEnumerable<PickedListing> LivePicks => this.Picks.Where(p => !p.Gone);

    /// <summary>
    ///  Gets or sets Cheapest price of the item saved.
    /// </summary>
    public double Price
    {
      get => this.HasPicks ? this.LivePicks.Select(p => p.Price).DefaultIfEmpty(0).Min() : this.SinglePrice;
      set => this.SinglePrice = value;
    }

    /// <summary>
    ///  Gets or sets world from where the price attribute was fetched.
    /// </summary>
    /// <remarks>Picks spread over several worlds read as a count rather than a name.</remarks>
    public string World
    {
      get
      {
        if (!this.HasPicks)
        {
          return this.SingleWorld;
        }

        var worlds = this.Worlds;

        if (worlds.Count == 0)
        {
          return string.Empty;
        }

        return worlds.Count == 1 ? worlds[0] : worlds.Count + ManyWorldsSuffix;
      }

      set => this.SingleWorld = value;
    }

    /// <summary>
    /// Gets the distinct worlds this row's picks sit on, closest name first.
    /// </summary>
    public IReadOnlyList<string> Worlds =>
      this.LivePicks
        .Select(p => p.World)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(w => w, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    /// <summary>
    ///  Gets or sets the stack size of the listing the price came from, or 0 when it is unknown.
    /// </summary>
    /// <remarks>Rows saved before buying existed have no stack size, and cannot be bought until refreshed.</remarks>
    public long Quantity
    {
      get => this.HasPicks ? this.LivePicks.Sum(p => p.Quantity) : this.SingleQuantity;
      set => this.SingleQuantity = value;
    }

    /// <summary>
    ///  Gets or sets a value indicating whether the listing the price came from is high quality.
    /// </summary>
    /// <remarks>A row of picks only counts as high quality when every one of them is.</remarks>
    public bool Hq
    {
      get => this.HasPicks ? this.LivePicks.Any() && this.LivePicks.All(p => p.Hq) : this.SingleHq;
      set => this.SingleHq = value;
    }

    /// <summary>
    ///  Gets how many of the row's items come from high quality listings.
    /// </summary>
    public long QuantityHq =>
      this.HasPicks ? this.LivePicks.Where(p => p.Hq).Sum(p => p.Quantity) : (this.Hq ? this.Quantity : 0);

    /// <summary>
    ///  Gets how many of the row's items come from normal quality listings.
    /// </summary>
    public long QuantityNq => this.Quantity - this.QuantityHq;

    /// <summary>
    ///  Gets the gil the whole listing costs.
    /// </summary>
    /// <remarks>
    ///  Picks differ in price, so this is the sum of their totals rather than the row's price times
    ///  its stack size.
    /// </remarks>
    public double Total => this.HasPicks ? this.LivePicks.Sum(p => p.Total) : this.Price * this.Quantity;

    /// <summary>
    ///  Gets or sets how the last buy attempt on this row ended.
    /// </summary>
    /// <remarks>Not saved to the configuration; it only lasts until the row is priced again.</remarks>
    public BuyOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry is waiting for a new price.
    /// </summary>
    public bool Refreshing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the last refresh found nothing on sale in the scope.
    /// </summary>
    public bool Unlisted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry was added straight from a listing.
    /// </summary>
    /// <remarks>Such an entry keeps the listing it was added from, so every refresh leaves it alone.</remarks>
    public bool IsDirect { get; set; }

    /// <summary>Gets or sets the price of the one listing a row without picks stands for.</summary>
    private double SinglePrice { get; set; }

    /// <summary>Gets or sets the world of the one listing a row without picks stands for.</summary>
    private string SingleWorld { get; set; } = string.Empty;

    /// <summary>Gets or sets the stack size of the one listing a row without picks stands for.</summary>
    private long SingleQuantity { get; set; }

    /// <summary>Gets or sets a value indicating whether the one listing a row without picks stands for is high quality.</summary>
    private bool SingleHq { get; set; }

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

      return new SavedItem(
        sourceItem,
        PickedListing.UnitPrice(cheapest, includeSalesTax),
        cheapest.WorldName ?? fallbackWorld,
        cheapest.Quantity,
        cheapest.Hq);
    }

    /// <summary>
    /// Builds an entry from one particular listing, kept as it is rather than priced again.
    /// </summary>
    /// <param name="sourceItem">The item the listing belongs to.</param>
    /// <param name="listing">The listing to keep.</param>
    /// <param name="includeSalesTax">True to fold the gil sales tax into the price.</param>
    /// <param name="fallbackWorld">The world to record when the listing carries none.</param>
    /// <returns>The entry.</returns>
    public static SavedItem FromListing(Item sourceItem, MarketDataListing listing, bool includeSalesTax, string fallbackWorld)
    {
      ArgumentNullException.ThrowIfNull(listing);

      return new SavedItem(
        sourceItem,
        PickedListing.UnitPrice(listing, includeSalesTax),
        listing.WorldName ?? fallbackWorld,
        listing.Quantity,
        listing.Hq)
      {
        IsDirect = true,
      };
    }

    /// <summary>
    /// Replaces the listings this row has been told to buy.
    /// </summary>
    /// <param name="picks">The picks, or an empty list to go back to standing for one listing.</param>
    public void SetPicks(IEnumerable<PickedListing> picks)
    {
      ArgumentNullException.ThrowIfNull(picks);

      var replacement = picks.ToArray();

      // Taking every pick off a row leaves it standing for one listing again, so it keeps the cheapest
      // of the picks it had rather than whatever it was priced at before they were made.
      if (replacement.Length == 0 && this.HasPicks)
      {
        var cheapest = this.Picks.OrderBy(p => p.Price).First();

        this.SinglePrice = cheapest.Price;
        this.SingleWorld = cheapest.World;
        this.SingleQuantity = cheapest.Quantity;
        this.SingleHq = cheapest.Hq;
      }

      this.Picks.Clear();
      this.Picks.AddRange(replacement);
      this.Outcome = BuyOutcome.None;

      if (this.HasPicks)
      {
        // The row is priced by its picks from here, and every one of them was on sale to be picked.
        this.Unlisted = false;
      }
    }

    /// <summary>
    /// Rolls the outcomes of a row's picks up into the row's own.
    /// </summary>
    public void RollUpOutcome()
    {
      if (!this.HasPicks)
      {
        return;
      }

      var tried = this.Picks.Where(p => p.Outcome != BuyOutcome.None).ToArray();

      if (tried.Length == 0)
      {
        return;
      }

      var bought = tried.Where(p => p.Outcome is BuyOutcome.Bought or BuyOutcome.BoughtCheaper).ToArray();

      this.Outcome = bought.Length switch
      {
        0 => BuyOutcome.Failed,
        _ when bought.Length < tried.Length => BuyOutcome.PartlyBought,
        _ when bought.Any(p => p.Outcome == BuyOutcome.BoughtCheaper) => BuyOutcome.BoughtCheaper,
        _ => BuyOutcome.Bought,
      };
    }
  }
}
