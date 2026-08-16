// <copyright file="ShoppingListBuyer.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// Buys shopping list rows off the Market Board, one row at a time.
  /// </summary>
  /// <remarks>
  /// A Buy-all run is ordered by how far it has to travel - the current world first, then the rest of
  /// the data centre, then the rest of the region, then anywhere else - and rows on the same world are
  /// kept together so each world is only travelled to once.
  /// </remarks>
  public sealed class ShoppingListBuyer : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly MarketBoardPurchase purchase;

    private readonly Queue<SavedItem> queue = new Queue<SavedItem>();

    private string boardWorld = string.Empty;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingListBuyer"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public ShoppingListBuyer(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      this.purchase = new MarketBoardPurchase(
        this.plugin.Framework,
        this.plugin.GameGui,
        this.plugin.MarketBoard,
        this.plugin.Log,
        () => !this.plugin.Config.NoGilSalesTax);
    }

    /// <summary>
    /// Gets a value indicating whether a run is going.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the name of the item being bought right now.
    /// </summary>
    public string CurrentItemName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the number of rows the run has finished with.
    /// </summary>
    public int Done { get; private set; }

    /// <summary>
    /// Gets the number of rows the run started with.
    /// </summary>
    public int Total { get; private set; }

    /// <summary>
    /// Checks whether a row can be bought, and says why when it cannot.
    /// </summary>
    /// <param name="row">The row to check.</param>
    /// <param name="reason">Why the row cannot be bought, or an empty string when it can.</param>
    /// <returns>True when the row can be bought.</returns>
    public bool CanBuy(SavedItem row, out string reason)
    {
      ArgumentNullException.ThrowIfNull(row);

      if (!this.plugin.Config.ShoppingListBuyEnabled)
      {
        reason = "Buying from the shopping list is switched off in the settings.";
        return false;
      }

      if (row.Unlisted || row.Refreshing)
      {
        reason = "This row has no listing to buy.";
        return false;
      }

      if (string.IsNullOrEmpty(row.World))
      {
        reason = "This row has no world to travel to.";
        return false;
      }

      if (row.Quantity <= 0)
      {
        reason = "Refresh the list first so the stack size is recorded.";
        return false;
      }

      if (this.plugin.Config.SkipUnlockedWhenBuying && ItemUnlock.IsUnlocked(this.plugin.PlayerState, row.SourceItem) == true)
      {
        reason = "This character has already unlocked this item.";
        return false;
      }

      reason = string.Empty;
      return true;
    }

    /// <summary>
    /// Buys a single row.
    /// </summary>
    /// <param name="row">The row to buy.</param>
    public void BuyOne(SavedItem row)
    {
      this.BuyAll(new[] { row });
    }

    /// <summary>
    /// Buys every row that can be bought, cheapest travel first.
    /// </summary>
    /// <param name="rows">The rows to buy.</param>
    public void BuyAll(IEnumerable<SavedItem> rows)
    {
      ArgumentNullException.ThrowIfNull(rows);

      if (this.IsRunning)
      {
        return;
      }

      var buyable = new List<SavedItem>();
      var skipped = 0;

      foreach (var row in rows)
      {
        if (this.CanBuy(row, out _))
        {
          buyable.Add(row);
        }
        else
        {
          skipped++;
        }
      }

      if (skipped > 0)
      {
        this.plugin.ChatGui.Print($"Skipped {skipped} shopping list rows that cannot be bought yet.");
      }

      if (buyable.Count == 0)
      {
        return;
      }

      foreach (var row in this.Order(buyable))
      {
        this.queue.Enqueue(row);
      }

      this.IsRunning = true;
      this.Done = 0;
      this.Total = this.queue.Count;
      this.boardWorld = string.Empty;

      this.StartNext();
    }

    /// <summary>
    /// Stops the run after the row that is in flight.
    /// </summary>
    public void Cancel()
    {
      this.queue.Clear();
      this.plugin.AutoSearch.Disarm();
      this.purchase.Cancel();

      // Neither of those fires a continuation when nothing is in flight, so settle the run here.
      if (this.IsRunning)
      {
        this.StartNext();
      }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.queue.Clear();
      this.purchase.Dispose();
      this.isDisposed = true;
    }

    private static string TieBreak(string region)
    {
      // Oceania is the most expensive hop, so it goes last inside the "somewhere else" bucket.
      return string.Equals(region, WorldRegions.Oceania, StringComparison.Ordinal) ? "1" : "0";
    }

    private IEnumerable<SavedItem> Order(IEnumerable<SavedItem> rows)
    {
      return rows
        .Select(row => new { Row = row, Tier = this.TravelTier(row.World), Region = this.plugin.WorldCatalogue.Find(row.World)?.Region ?? string.Empty })
        .OrderBy(entry => entry.Tier)
        .ThenBy(entry => TieBreak(entry.Region), StringComparer.Ordinal)
        .ThenBy(entry => entry.Row.World, StringComparer.Ordinal)
        .Select(entry => entry.Row);
    }

    /// <summary>
    /// Scores how far a world is from the one the player is standing on.
    /// </summary>
    /// <param name="world">The world to score.</param>
    /// <returns>0 for the current world, 1 for its data centre, 2 for its region, 3 for anywhere else.</returns>
    private int TravelTier(string world)
    {
      var current = this.plugin.PlayerState.IsLoaded
        ? this.plugin.PlayerState.CurrentWorld.Value.Name.ExtractText()
        : string.Empty;

      if (string.Equals(world, current, StringComparison.OrdinalIgnoreCase))
      {
        return 0;
      }

      var target = this.plugin.WorldCatalogue.Find(world);
      var here = this.plugin.WorldCatalogue.Find(current);

      if (target == null || here == null)
      {
        return 3;
      }

      if (string.Equals(target.DataCentre, here.DataCentre, StringComparison.Ordinal))
      {
        return 1;
      }

      return string.Equals(target.Region, here.Region, StringComparison.Ordinal) ? 2 : 3;
    }

    private void StartNext()
    {
      if (this.queue.Count == 0)
      {
        this.purchase.CloseBoard();
        this.IsRunning = false;
        this.CurrentItemName = string.Empty;
        this.boardWorld = string.Empty;
        return;
      }

      var row = this.queue.Peek();

      if (this.boardWorld.Length > 0 && !string.Equals(this.boardWorld, row.World, StringComparison.OrdinalIgnoreCase))
      {
        this.purchase.CloseBoard();
      }

      this.CurrentItemName = row.SourceItem.Name.ExtractText();
      this.plugin.MarketBoardContext.GoToMarketBoardForBuy(row.World, row.SourceItem, opened => this.OnSearchFinished(row, opened));
    }

    private void OnSearchFinished(SavedItem row, bool opened)
    {
      if (!opened)
      {
        this.Report(row, BuyResult.Failed("the Market Board listings never opened"));
        return;
      }

      this.boardWorld = row.World;

      this.purchase.Start(
        new BuyRequest(row.SourceItem.RowId, row.SourceItem.Name.ExtractText(), row.Hq, row.Quantity, row.Price),
        result => this.Report(row, result));
    }

    private void Report(SavedItem row, BuyResult result)
    {
      var name = row.SourceItem.Name.ExtractText();

      if (result.Success)
      {
        row.Outcome = result.UnitPrice < row.Price ? BuyOutcome.BoughtCheaper : BuyOutcome.Bought;

        var total = (result.UnitPrice * row.Quantity).ToString("N0", CultureInfo.CurrentCulture);
        this.plugin.ChatGui.Print($"Bought {name} x{row.Quantity} for {total} gil");
      }
      else
      {
        row.Outcome = BuyOutcome.Failed;

        var limit = row.Price.ToString("N0", CultureInfo.CurrentCulture);
        this.plugin.ChatGui.Print($"{name}: {result.Reason} (limit: {limit}) - not bought");
      }

      if (this.queue.Count > 0)
      {
        this.queue.Dequeue();
      }

      this.Done++;
      this.StartNext();
    }
  }
}
