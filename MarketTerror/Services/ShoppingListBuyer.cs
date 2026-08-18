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
  /// Buys shopping list rows off the Market Board, one listing at a time.
  /// </summary>
  /// <remarks>
  /// A Buy-all run is ordered by how far it has to travel - the current world first, then the rest of
  /// the data centre, then the rest of the region, then anywhere else - and listings on the same world
  /// are kept together so each world is only travelled to once. Rows whose picked listings are spread
  /// over several worlds go after the rest, since they are the ones that cost more than one trip.
  /// </remarks>
  public sealed class ShoppingListBuyer : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly MarketBoardPurchase purchase;

    private readonly MarketBoardRefresh refresh;

    private readonly Queue<BuyJob> queue = new Queue<BuyJob>();

    /// <summary>
    /// How many jobs of each row the run still has to get through, so a row is only reported once.
    /// </summary>
    private readonly Dictionary<SavedItem, int> outstanding = new Dictionary<SavedItem, int>();

    private string boardWorld = string.Empty;

    /// <summary>
    /// The item the board is showing, and whether anything of it has been bought there, so a
    /// refresh only runs once the run is finished with that item on that world.
    /// </summary>
    private string boardItemName = string.Empty;

    private uint boardItemId;

    private bool boughtOnBoard;

    /// <summary>
    /// Whether the listing in flight is being bought off a listings window the last purchase left up,
    /// and whether the board turned out to still be holding that purchase's listings.
    /// </summary>
    private bool reusingListings;

    private bool listingsWentStale;

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

      this.refresh = new MarketBoardRefresh(
        this.plugin.Framework,
        this.plugin.GameGui,
        this.plugin.MarketBoard,
        this.plugin.Log,
        this.plugin.AutoSearch);
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
    /// Gets the number of listings the run has finished with.
    /// </summary>
    public int Done { get; private set; }

    /// <summary>
    /// Gets the number of listings the run started with.
    /// </summary>
    public int Total { get; private set; }

    /// <summary>
    /// Gets the row being bought right now, or null when no run is going.
    /// </summary>
    public SavedItem? CurrentRow { get; private set; }

    /// <summary>
    /// Gets the picked listing being bought right now, or null when the row stands for one listing.
    /// </summary>
    public PickedListing? CurrentPick { get; private set; }

    /// <summary>
    /// Checks whether a run has still to get to a picked listing.
    /// </summary>
    /// <param name="pick">The picked listing to look for.</param>
    /// <returns>True when the listing is waiting in the queue, the one in flight included.</returns>
    public bool IsQueued(PickedListing pick)
    {
      return this.IsRunning && this.queue.Any(job => ReferenceEquals(job.Pick, pick));
    }

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

      // A run that is already going travels through the main menu, so only the start is guarded.
      if (!this.plugin.ClientState.IsLoggedIn)
      {
        reason = "No character is logged in.";
        return false;
      }

      if (row.Refreshing)
      {
        reason = "This row is being priced again.";
        return false;
      }

      if (this.plugin.Config.SkipUnlockedWhenBuying && ItemUnlock.IsUnlocked(this.plugin.PlayerState, row.SourceItem) == true)
      {
        reason = "This character has already unlocked this item.";
        return false;
      }

      if (row.HasPicks)
      {
        if (!row.LivePicks.Any())
        {
          reason = "None of the picked listings are on sale any more.";
          return false;
        }

        reason = string.Empty;
        return true;
      }

      if (row.Unlisted)
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

      if (!this.plugin.ClientState.IsLoggedIn)
      {
        return;
      }

      var jobs = new List<BuyJob>();
      var skipped = 0;

      foreach (var row in rows)
      {
        if (!this.CanBuy(row, out _))
        {
          skipped++;
          continue;
        }

        jobs.AddRange(Jobs(row));
      }

      if (skipped > 0)
      {
        this.plugin.ChatGui.Print($"Skipped {skipped} shopping list rows that cannot be bought yet.");
      }

      if (jobs.Count == 0)
      {
        return;
      }

      this.outstanding.Clear();

      foreach (var job in this.Order(jobs))
      {
        this.queue.Enqueue(job);
        this.outstanding[job.Row] = this.outstanding.GetValueOrDefault(job.Row) + 1;
      }

      this.IsRunning = true;
      this.Done = 0;
      this.Total = this.queue.Count;
      this.ForgetBoardItem();

      this.StartNext();
    }

    /// <summary>
    /// Stops the run after the listing that is in flight.
    /// </summary>
    public void Cancel()
    {
      this.queue.Clear();
      this.plugin.AutoSearch.Disarm();
      this.refresh.Cancel();
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
      this.outstanding.Clear();
      this.purchase.Dispose();
      this.refresh.Dispose();
      this.isDisposed = true;
    }

    private static string TieBreak(string region)
    {
      // Oceania is the most expensive hop, so it goes last inside the "somewhere else" bucket.
      return string.Equals(region, WorldRegions.Oceania, StringComparison.Ordinal) ? "1" : "0";
    }

    /// <summary>
    /// Breaks a row into the listings a run has to buy for it.
    /// </summary>
    /// <param name="row">The row to break up.</param>
    /// <returns>One job per picked listing, or a single job for a row that stands for one listing.</returns>
    private static BuyJob[] Jobs(SavedItem row)
    {
      if (!row.HasPicks)
      {
        return new[] { new BuyJob(row, null) };
      }

      // Two runs without a refresh in between would otherwise report the first run's results again.
      foreach (var pick in row.Picks)
      {
        pick.Outcome = BuyOutcome.None;
        pick.Paid = null;
      }

      // Cheapest first, so a run that is cancelled part way through has bought the best of them.
      return row.LivePicks
        .OrderBy(pick => pick.Price)
        .Select(pick => new BuyJob(row, pick))
        .ToArray();
    }

    private static string Gil(double value)
    {
      return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private IEnumerable<BuyJob> Order(IEnumerable<BuyJob> jobs)
    {
      return jobs
        .Select(job => new
        {
          Job = job,
          Spread = job.Row.HasPicks && job.Row.Worlds.Count > 1 ? 1 : 0,
          Tier = this.TravelTier(job.World),
          Region = this.plugin.WorldCatalogue.Find(job.World)?.Region ?? string.Empty,
        })
        .OrderBy(entry => entry.Spread)
        .ThenBy(entry => entry.Tier)
        .ThenBy(entry => TieBreak(entry.Region), StringComparer.Ordinal)
        .ThenBy(entry => entry.Job.World, StringComparer.Ordinal)
        .Select(entry => entry.Job);
    }

    /// <summary>
    /// Scores how far a world is from the one the player is standing on.
    /// </summary>
    /// <param name="world">The world to score.</param>
    /// <returns>0 for the current world, 1 for its data centre, 2 for its region, 3 for anywhere else.</returns>
    private int TravelTier(string world)
    {
      var current = PlayerWorld.CurrentName(this.plugin.PlayerState);

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
      if (this.TryRefreshBoardItem())
      {
        return;
      }

      // Travel goes through the main menu, so a run only stops when the next job comes round with nobody logged in.
      if (this.queue.Count > 0 && !this.plugin.ClientState.IsLoggedIn)
      {
        this.queue.Clear();
        this.plugin.ChatGui.Print("Stopped buying: no character is logged in.");
      }

      if (this.queue.Count == 0)
      {
        this.purchase.CloseBoard();
        this.IsRunning = false;
        this.CurrentItemName = string.Empty;
        this.CurrentRow = null;
        this.CurrentPick = null;
        this.ForgetBoardItem();
        this.outstanding.Clear();
        return;
      }

      var job = this.queue.Peek();

      if (this.boardWorld.Length > 0 && !string.Equals(this.boardWorld, job.World, StringComparison.OrdinalIgnoreCase))
      {
        this.purchase.CloseBoard();
      }

      this.CurrentItemName = this.Describe(job);
      this.CurrentRow = job.Row;
      this.CurrentPick = job.Pick;

      var sameBoardItem = this.boardItemId == job.Row.SourceItem.RowId
        && string.Equals(this.boardWorld, job.World, StringComparison.OrdinalIgnoreCase);

      // Another listing of the same item, and the last purchase left the listings up: buy straight
      // off them. The buy only takes a listing the board has sent since, so nothing stale is bought.
      this.reusingListings = sameBoardItem && !this.listingsWentStale && this.purchase.IsListingsWindowOpen;
      this.listingsWentStale = false;

      if (this.reusingListings)
      {
        this.OnSearchFinished(job, true);
        return;
      }

      // Same item, but the listings have gone: they only have to be opened again rather than
      // searched for from scratch.
      if (sameBoardItem
        && this.plugin.MarketBoardContext.TryReopenListingsForBuy(job.World, job.Row.SourceItem, opened => this.OnSearchFinished(job, opened)))
      {
        return;
      }

      this.plugin.MarketBoardContext.GoToMarketBoardForBuy(job.World, job.Row.SourceItem, opened => this.OnSearchFinished(job, opened));
    }

    /// <summary>
    /// Opens the listings and sales history of the item the board is showing, once the run has bought
    /// everything it wanted of it on that world.
    /// </summary>
    /// <returns>True when a refresh was started, which calls back into <see cref="StartNext"/> when it ends.</returns>
    /// <remarks>
    /// Buying takes the listing off the board without anything asking for the board again, so a
    /// Universalis uploader would otherwise keep serving the listing that has just been bought.
    /// </remarks>
    private bool TryRefreshBoardItem()
    {
      if (!this.boughtOnBoard || !this.plugin.Config.RefreshListingsAfterBuy)
      {
        return false;
      }

      // More of the same item still to buy on the same world: that buy waits for the board to send
      // its listings again anyway, which is all an uploader needs.
      if (this.queue.Count > 0)
      {
        var next = this.queue.Peek();

        if (next.Row.SourceItem.RowId == this.boardItemId
          && string.Equals(next.World, this.boardWorld, StringComparison.OrdinalIgnoreCase))
        {
          return false;
        }
      }

      var name = this.boardItemName;
      var id = this.boardItemId;

      this.boughtOnBoard = false;
      this.refresh.Start(name, id, this.StartNext);
      return true;
    }

    /// <summary>
    /// Forgets which item the board is showing, so nothing is refreshed once it has been closed.
    /// </summary>
    private void ForgetBoardItem()
    {
      this.boardWorld = string.Empty;
      this.boardItemName = string.Empty;
      this.boardItemId = 0;
      this.boughtOnBoard = false;
      this.reusingListings = false;
      this.listingsWentStale = false;
    }

    /// <summary>
    /// Names what is being bought, counting the listings off for a row that has more than one.
    /// </summary>
    /// <param name="job">The job about to start.</param>
    /// <returns>The text the progress line shows.</returns>
    private string Describe(BuyJob job)
    {
      var name = job.Row.SourceItem.Name.ExtractText();

      if (job.Pick == null || !job.Row.HasPicks)
      {
        return name;
      }

      var live = job.Row.LivePicks.Count();

      if (live <= 1)
      {
        return name;
      }

      var left = this.outstanding.GetValueOrDefault(job.Row);

      return $"{name} (listing {live - left + 1} of {live})";
    }

    private void OnSearchFinished(BuyJob job, bool opened)
    {
      if (!opened)
      {
        this.Report(job, BuyResult.Failed("the Market Board listings never opened"));
        return;
      }

      this.boardWorld = job.World;
      this.boardItemId = job.Row.SourceItem.RowId;
      this.boardItemName = job.Row.SourceItem.Name.ExtractText();

      this.purchase.Start(
        new BuyRequest(job.Row.SourceItem.RowId, job.Row.SourceItem.Name.ExtractText(), job.Hq, job.Quantity, job.Price),
        result => this.Report(job, result),
        this.reusingListings);
    }

    private void Report(BuyJob job, BuyResult result)
    {
      // The board kept its window up but not its listings, so this job has not been tried yet.
      if (result.Stale)
      {
        this.listingsWentStale = true;
        this.StartNext();
        return;
      }

      var outcome = result.Success
        ? (result.UnitPrice < job.Price ? BuyOutcome.BoughtCheaper : BuyOutcome.Bought)
        : BuyOutcome.Failed;

      this.boughtOnBoard |= result.Success;

      if (job.Pick == null)
      {
        job.Row.Outcome = outcome;
        this.PrintOne(job, result);
      }
      else
      {
        job.Pick.Outcome = outcome;
        job.Pick.Paid = result.Success ? result.UnitPrice : null;
        job.Row.RollUpOutcome();
      }

      var left = this.outstanding.GetValueOrDefault(job.Row) - 1;
      this.outstanding[job.Row] = left;

      if (left == 0 && job.Pick != null)
      {
        this.PrintRow(job.Row);
      }

      if (this.queue.Count > 0)
      {
        this.queue.Dequeue();
      }

      this.Done++;
      this.StartNext();
    }

    /// <summary>
    /// Says in chat how a row that stands for one listing ended.
    /// </summary>
    /// <param name="job">The finished job.</param>
    /// <param name="result">How it ended.</param>
    private void PrintOne(BuyJob job, BuyResult result)
    {
      var name = job.Row.SourceItem.Name.ExtractText();

      if (result.Success)
      {
        this.plugin.ChatGui.Print($"Bought {name} x{job.Quantity} for {Gil(result.UnitPrice * job.Quantity)} gil");
      }
      else
      {
        this.plugin.ChatGui.Print($"{name}: {result.Reason} (limit: {Gil(job.Price)}) - not bought");
      }
    }

    /// <summary>
    /// Says in chat how a row of picked listings ended, once all of them have been tried.
    /// </summary>
    /// <param name="row">The finished row.</param>
    private void PrintRow(SavedItem row)
    {
      var name = row.SourceItem.Name.ExtractText();
      var tried = row.Picks.Where(p => p.Outcome != BuyOutcome.None).ToArray();
      var bought = tried.Where(p => p.Paid.HasValue).ToArray();

      if (bought.Length == 0)
      {
        this.plugin.ChatGui.Print($"{name}: none of the {tried.Length} picked listings could be bought");
        return;
      }

      var units = bought.Sum(p => p.Quantity);
      var spent = bought.Sum(p => p.Paid!.Value * p.Quantity);
      var listings = bought.Length == tried.Length
        ? $"{bought.Length} listings"
        : $"{bought.Length} of {tried.Length} listings";

      this.plugin.ChatGui.Print($"Bought {name} x{units} for {Gil(spent)} gil over {listings}");
    }

    /// <summary>
    /// One listing a buy run has to go and get.
    /// </summary>
    private sealed class BuyJob
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="BuyJob"/> class.
      /// </summary>
      /// <param name="row">The row the listing belongs to.</param>
      /// <param name="pick">The picked listing, or null when the row stands for one listing itself.</param>
      public BuyJob(SavedItem row, PickedListing? pick)
      {
        this.Row = row;
        this.Pick = pick;
      }

      /// <summary>Gets the row the listing belongs to.</summary>
      public SavedItem Row { get; }

      /// <summary>Gets the picked listing, or null when the row stands for one listing itself.</summary>
      public PickedListing? Pick { get; }

      /// <summary>Gets the world to buy on.</summary>
      public string World => this.Pick?.World ?? this.Row.World;

      /// <summary>Gets the stack size the listing has to have.</summary>
      public long Quantity => this.Pick?.Quantity ?? this.Row.Quantity;

      /// <summary>Gets a value indicating whether the listing has to be high quality.</summary>
      public bool Hq => this.Pick?.Hq ?? this.Row.Hq;

      /// <summary>Gets the highest price per unit that may be paid.</summary>
      public double Price => this.Pick?.Price ?? this.Row.Price;
    }
  }
}
