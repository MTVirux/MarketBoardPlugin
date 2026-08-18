// <copyright file="MarketBoardPurchase.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Globalization;
  using System.Threading;
  using Dalamud.Game.Network.Structures;
  using Dalamud.Game.Text.SeStringHandling;
  using Dalamud.Game.Text.SeStringHandling.Payloads;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.UI;
  using FFXIVClientStructs.FFXIV.Client.UI.Info;
  using FFXIVClientStructs.FFXIV.Component.GUI;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// Buys one Market Board listing off a board that is already showing the item's listings.
  /// </summary>
  /// <remarks>
  /// The confirmation dialog the game raises is answered automatically, so the dialog is checked
  /// rather than trusted: the prompt has to name the expected item and ask for no more gil than the
  /// request allows, otherwise it is declined. That check is also what catches a mispicked row, since
  /// the visible listing order is the game's sort order and need not match the listing array.
  /// </remarks>
  public sealed class MarketBoardPurchase : IDisposable
  {
    private const string ResultAddonName = "ItemSearchResult";
    private const string BoardAddonName = "ItemSearch";
    private const string ConfirmAddonName = "SelectYesno";
    private const long ListingsTimeoutMs = 15000;
    private const long ReusedListingsTimeoutMs = 3000;
    private const long ListingsSettleMs = 500;
    private const long ReusedSettleMs = 500;
    private const long EarlyArrivalMs = 3000;
    private const long ConfirmTimeoutMs = 10000;
    private const long ResultTimeoutMs = 15000;
    private const uint HqItemIdOffset = 1000000;
    private const int MaxWarningPrompts = 2;
    private const int YesButton = 0;
    private const int NoButton = 1;

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IMarketBoard marketBoard;
    private readonly IPluginLog log;
    private readonly Func<bool> includesSalesTax;

    /// <summary>
    /// The listings bought off the window that is up, so a window the game leaves as it was cannot
    /// offer the same listing twice.
    /// </summary>
    private readonly HashSet<ulong> boughtListings = new HashSet<ulong>();

    private State state = State.Idle;
    private bool reusedListings;
    private BuyRequest? request;
    private Action<BuyResult>? onFinished;
    private string answeredPrompt = string.Empty;
    private int warningPrompts;
    private long deadlineTick;
    private long startTick;
    private long lastOfferingsTick;
    private ulong purchasedListingBefore;
    private ulong targetListingId;
    private double targetUnitPrice;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardPurchase"/> class.
    /// </summary>
    /// <param name="framework">The framework.</param>
    /// <param name="gameGui">The game GUI.</param>
    /// <param name="marketBoard">The market board events the game raises as it receives listings.</param>
    /// <param name="log">The plugin log.</param>
    /// <param name="includesSalesTax">Returns whether saved prices have the gil sales tax folded in.</param>
    public MarketBoardPurchase(IFramework framework, IGameGui gameGui, IMarketBoard marketBoard, IPluginLog log, Func<bool> includesSalesTax)
    {
      this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
      this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
      this.marketBoard = marketBoard ?? throw new ArgumentNullException(nameof(marketBoard));
      this.log = log ?? throw new ArgumentNullException(nameof(log));
      this.includesSalesTax = includesSalesTax ?? throw new ArgumentNullException(nameof(includesSalesTax));

      this.framework.Update += this.OnFrameworkUpdate;
      this.marketBoard.OfferingsReceived += this.OnOfferingsReceived;
    }

    private enum State
    {
      Idle,
      WaitingListings,
      WaitingConfirm,
      WaitingResult,
    }

    /// <summary>
    /// Gets a value indicating whether a purchase is in flight.
    /// </summary>
    public bool IsRunning => this.state != State.Idle;

    /// <summary>
    /// Gets a value indicating whether the listings window is up.
    /// </summary>
    public bool IsListingsWindowOpen => this.gameGui.GetAddonByName(ResultAddonName) != nint.Zero;

    /// <summary>
    /// Starts buying the requested listing off the board that is currently open.
    /// </summary>
    /// <param name="buyRequest">What may be bought.</param>
    /// <param name="finished">Called once with how the attempt ended.</param>
    /// <param name="reusingListings">
    /// True when the listings window was left up by the last purchase rather than opened for this one,
    /// so only what the board sends after this point may be bought off.
    /// </param>
    public void Start(BuyRequest buyRequest, Action<BuyResult> finished, bool reusingListings = false)
    {
      ArgumentNullException.ThrowIfNull(buyRequest);
      ArgumentNullException.ThrowIfNull(finished);

      if (this.IsRunning)
      {
        finished(BuyResult.Failed("another purchase was still running, so this one never started"));
        return;
      }

      if (!reusingListings)
      {
        // Listings opened afresh come from the server, so nothing already bought can be among them.
        this.boughtListings.Clear();
      }

      this.request = buyRequest;
      this.onFinished = finished;
      this.state = State.WaitingListings;
      this.reusedListings = reusingListings;
      this.startTick = Environment.TickCount64;
      this.deadlineTick = this.startTick + (reusingListings ? ReusedListingsTimeoutMs : ListingsTimeoutMs);
      this.answeredPrompt = string.Empty;
      this.warningPrompts = 0;
      this.targetListingId = 0;
      this.targetUnitPrice = 0;
    }

    /// <summary>
    /// Abandons a purchase that has not been confirmed yet.
    /// </summary>
    public void Cancel()
    {
      if (this.IsRunning)
      {
        this.Finish(BuyResult.Failed("the run was cancelled part way through this purchase"));
      }
    }

    /// <summary>
    /// Closes the Market Board and its results window if either is open.
    /// </summary>
    public unsafe void CloseBoard()
    {
      CloseAddon(this.gameGui.GetAddonByName(ResultAddonName));
      CloseAddon(this.gameGui.GetAddonByName(BoardAddonName));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      this.framework.Update -= this.OnFrameworkUpdate;
      this.marketBoard.OfferingsReceived -= this.OnOfferingsReceived;
      GC.SuppressFinalize(this);
    }

    private static unsafe void CloseAddon(nint addonPtr)
    {
      if (addonPtr != nint.Zero)
      {
        ((AtkUnitBase*)addonPtr)->Close(true);
      }
    }

    /// <summary>
    /// Puts a prompt on one line so the way the game wrapped it cannot get in the way of reading it.
    /// </summary>
    /// <param name="text">The text to flatten.</param>
    /// <returns>The text with every run of whitespace turned into a single space.</returns>
    /// <remarks>The dialog is wrapped to its own width, which lands mid-name often enough to matter.</remarks>
    private static string Flatten(string text)
    {
      return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Checks whether a prompt is about the item being bought.
    /// </summary>
    /// <param name="prompt">The parsed prompt.</param>
    /// <param name="buy">What is being bought.</param>
    /// <returns>True when the prompt names the item.</returns>
    /// <remarks>
    /// An item link carries the row id, which beats reading the name, but the purchase prompt only
    /// colours the name rather than linking it, so the text is usually what answers this.
    /// </remarks>
    private static bool NamesItem(SeString prompt, BuyRequest buy)
    {
      foreach (var payload in prompt.Payloads)
      {
        if (payload is ItemPayload item && item.ItemId % HqItemIdOffset == buy.ItemId)
        {
          return true;
        }
      }

      return Flatten(prompt.TextValue).Contains(Flatten(buy.ItemName), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the largest number out of a confirmation prompt, which is the gil total it is asking about.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>The largest number found, or -1 when the prompt has none.</returns>
    /// <remarks>
    /// Thousands separators differ by client language, so anything that is not a digit or a separator
    /// ends a figure. Two figures running together would read as one larger number, which can only
    /// decline a purchase that should have gone through - never the other way round.
    /// </remarks>
    private static double LargestNumber(string prompt)
    {
      var largest = -1d;
      var current = 0d;
      var reading = false;

      foreach (var character in prompt)
      {
        if (char.IsDigit(character))
        {
          current = (current * 10) + (character - '0');
          reading = true;
          continue;
        }

        if (reading && (character is ',' or '.' or '\u00A0' or '\u202F'))
        {
          continue;
        }

        if (reading)
        {
          largest = Math.Max(largest, current);
          current = 0;
          reading = false;
        }
      }

      return reading ? Math.Max(largest, current) : largest;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Must never throw into the framework update loop")]
    private void OnFrameworkUpdate(IFramework updatedFramework)
    {
      if (this.state == State.Idle || this.request == null)
      {
        return;
      }

      try
      {
        if (Environment.TickCount64 > this.deadlineTick)
        {
          if (this.state == State.WaitingListings && this.reusedListings)
          {
            // The window stayed up but the board never sent the listings again, so it is still
            // showing the ones the last purchase came off. The caller opens them again instead.
            this.log.Debug($"The board never sent its listings again after the last purchase of \"{this.request!.ItemName}\"");
            this.Finish(BuyResult.StaleListings());
            return;
          }

          if (this.state == State.WaitingListings)
          {
            this.LogListingsTimeout();
          }

          this.Finish(BuyResult.Failed(this.state switch
          {
            State.WaitingListings => Interlocked.Read(ref this.lastOfferingsTick) < this.startTick - EarlyArrivalMs
              ? "the board never sent its listings for this item, so there was nothing to buy off"
              : "the board's listings were still coming in when the buy gave up waiting for them",
            State.WaitingConfirm => "the board never asked to confirm the purchase after the listing was clicked",
            _ => "the purchase was confirmed but the server never said it went through",
          }));

          return;
        }

        switch (this.state)
        {
          case State.WaitingListings:
            this.TrySelectListing();
            break;
          case State.WaitingConfirm:
            this.TryAnswerConfirm();
            break;
          case State.WaitingResult:
            this.TryReadResult();
            break;
          default:
            break;
        }
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "The Market Board purchase state machine threw");
        this.Finish(BuyResult.Failed($"the purchase hit an unexpected error: {ex.Message}"));
      }
    }

    /// <summary>
    /// Says what the board was doing when a buy gave up waiting for its listings.
    /// </summary>
    private unsafe void LogListingsTimeout()
    {
      var proxy = InfoProxyItemSearch.Instance();
      var windowOpen = this.gameGui.GetAddonByName(ResultAddonName) != nint.Zero;
      var name = this.request!.ItemName;
      var sinceArrival = Interlocked.Read(ref this.lastOfferingsTick) - this.startTick;

      this.log.Warning(proxy == null
        ? $"Gave up waiting for the listings of \"{name}\": no item search proxy, last page {sinceArrival}ms into the buy, listings window open {windowOpen}"
        : $"Gave up waiting for the listings of \"{name}\": last page {sinceArrival}ms into the buy, count {proxy->ListingCount}, search item {proxy->SearchItemId}, listings window open {windowOpen}");
    }

    /// <summary>
    /// Stamps the clock every time the board sends a page of listings.
    /// </summary>
    /// <param name="offerings">The listings the server sent.</param>
    /// <remarks>Raised off the framework thread, and before a buy starts as often as during one.</remarks>
    private void OnOfferingsReceived(IMarketBoardCurrentOfferings offerings)
    {
      if (offerings != null && offerings.ItemListings.Count > 0)
      {
        Interlocked.Exchange(ref this.lastOfferingsTick, Environment.TickCount64);
      }
    }

    /// <summary>
    /// Checks whether the listings the game is holding are this item's and all of them.
    /// </summary>
    /// <param name="proxy">The item search info proxy.</param>
    /// <param name="buy">What is being bought.</param>
    /// <returns>True when the listings can be matched against.</returns>
    /// <remarks>
    /// The board keeps the previous item's listings until the new ones land, and they land a page at a
    /// time. Waiting for the server to send them and then go quiet is what keeps a listing that is
    /// plainly on the board from looking like it is not there. The pages can land while the results
    /// window is still opening, so an arrival from just before the buy started counts as this one's.
    /// </remarks>
    private unsafe bool AreListingsReady(InfoProxyItemSearch* proxy, BuyRequest buy)
    {
      var now = Environment.TickCount64;
      var arrived = Interlocked.Read(ref this.lastOfferingsTick);

      if (proxy->ListingCount == 0 || proxy->SearchItemId % HqItemIdOffset != buy.ItemId)
      {
        return false;
      }

      // A window that was left up is already showing this item's listings, so they are what the buy
      // works off. The wait is only there to let a page the game asked for itself land first.
      if (this.reusedListings)
      {
        return now - this.startTick >= ReusedSettleMs && now - arrived >= ListingsSettleMs;
      }

      return arrived >= this.startTick - EarlyArrivalMs && now - arrived >= ListingsSettleMs;
    }

    private unsafe void TrySelectListing()
    {
      nint addonPtr = this.gameGui.GetAddonByName(ResultAddonName);
      if (addonPtr == nint.Zero)
      {
        // The window this buy was going to come off has gone, so there is nothing to wait for.
        if (this.reusedListings)
        {
          this.Finish(BuyResult.StaleListings());
        }

        return;
      }

      var addon = (AddonItemSearchResult*)addonPtr;
      if (!addon->AtkUnitBase.IsFullyLoaded() || !addon->AtkUnitBase.IsReady || addon->Results == null)
      {
        return;
      }

      var proxy = InfoProxyItemSearch.Instance();
      if (proxy == null)
      {
        return;
      }

      var buy = this.request!;
      if (!this.AreListingsReady(proxy, buy))
      {
        return;
      }

      var index = -1;
      var best = double.MaxValue;
      var withTax = this.includesSalesTax();

      for (var i = 0; i < (int)proxy->ListingCount; i++)
      {
        ref var listing = ref proxy->Listings[i];

        if (listing.ItemId % HqItemIdOffset != buy.ItemId
          || listing.IsHqItem != buy.Hq
          || listing.Quantity != buy.Quantity
          || listing.Quantity == 0
          || this.boughtListings.Contains(listing.ListingId))
        {
          continue;
        }

        var unitPrice = withTax
          ? listing.UnitPrice + ((double)listing.TotalTax / listing.Quantity)
          : listing.UnitPrice;

        if (unitPrice <= buy.MaxUnitPrice && unitPrice < best)
        {
          best = unitPrice;
          index = i;
          this.targetListingId = listing.ListingId;
        }
      }

      if (index < 0)
      {
        this.log.Debug($"None of the {proxy->ListingCount} listings on the board are \"{buy.ItemName}\" x{buy.Quantity} {(buy.Hq ? "HQ" : "NQ")} at {buy.MaxUnitPrice:F0} or less per unit");

        // The window may just be showing a list the game never refreshed, so say so rather than
        // give up: the caller opens the listings again and the search is done over.
        this.Finish(this.reusedListings ? BuyResult.StaleListings() : BuyResult.Failed(this.DescribeCheapest(proxy, withTax)));
        return;
      }

      if (index >= addon->Results->GetItemCount())
      {
        this.Finish(BuyResult.Failed($"the listing was in the search results but not among the {addon->Results->GetItemCount()} rows the board had drawn"));
        return;
      }

      this.targetUnitPrice = best;
      this.purchasedListingBefore = proxy->LastPurchasedMarketboardItem.ListingId;

      addon->Results->ScrollToItem((short)index);
      addon->Results->SelectItem(index, true);
      addon->Results->DispatchItemEvent(index, AtkEventType.ListItemClick);

      this.log.Debug($"Selected listing {index} of {proxy->ListingCount} for \"{buy.ItemName}\" at {best:F0} per unit");

      this.state = State.WaitingConfirm;
      this.deadlineTick = Environment.TickCount64 + ConfirmTimeoutMs;
    }

    private unsafe void TryAnswerConfirm()
    {
      nint addonPtr = this.gameGui.GetAddonByName(ConfirmAddonName);
      if (addonPtr == nint.Zero)
      {
        return;
      }

      var addon = (AddonSelectYesno*)addonPtr;
      if (!addon->AtkUnitBase.IsFullyLoaded() || !addon->AtkUnitBase.IsReady || addon->PromptText == null)
      {
        return;
      }

      var buy = this.request!;

      // The prompt carries the item as a link, so its raw text is full of payload bytes. Only the
      // parsed text is readable, and the payload is the surest way to tell which item it is about.
      var parsed = SeString.Parse(addon->PromptText->NodeText.AsSpan());
      var prompt = Flatten(parsed.TextValue);

      if (string.Equals(prompt, this.answeredPrompt, StringComparison.Ordinal))
      {
        // The prompt already said yes to is still on screen; wait for the next one.
        return;
      }

      var asked = LargestNumber(prompt);

      // The game asks its own questions before the price, such as having already learned the action an
      // item teaches. A prompt without a single figure in it cannot be the one asking for gil.
      if (asked < 0)
      {
        this.AnswerWarning(addon, prompt);
        return;
      }

      // A gil figure is written with separators, so allow a rounding gil either way rather than an exact compare.
      var namesItem = NamesItem(parsed, buy);
      var withinLimit = asked >= 0 && asked <= Math.Ceiling(buy.TotalLimit) + 1;

      if (!namesItem || !withinLimit)
      {
        this.log.Warning($"Declined a Market Board confirmation for \"{buy.ItemName}\": asked {asked:F0}, limit {buy.TotalLimit:F0}, prompt \"{prompt}\"");
        addon->AtkUnitBase.FireCallbackInt(NoButton);
        this.Finish(BuyResult.Failed(namesItem
          ? $"the board asked for {asked:N0} gil, more than the {buy.TotalLimit:N0} this listing may cost"
          : $"the board asked to confirm something other than {buy.ItemName}"));
        return;
      }

      this.log.Debug($"Confirming a Market Board purchase of \"{buy.ItemName}\" for {asked:F0} gil");
      addon->AtkUnitBase.FireCallbackInt(YesButton);

      this.state = State.WaitingResult;
      this.deadlineTick = Environment.TickCount64 + ResultTimeoutMs;
    }

    /// <summary>
    /// Says yes to a question the game asks before it gets round to the price.
    /// </summary>
    /// <param name="addon">The prompt.</param>
    /// <param name="prompt">Its text.</param>
    /// <remarks>
    /// Only a handful are answered, and only while a buy is in flight, so an unrelated dialog that
    /// happens to be up cannot be clicked through. The gil check on the price prompt still has to pass.
    /// </remarks>
    private unsafe void AnswerWarning(AddonSelectYesno* addon, string prompt)
    {
      if (this.warningPrompts >= MaxWarningPrompts)
      {
        this.log.Warning($"Gave up on a Market Board purchase of \"{this.request!.ItemName}\": still being asked \"{prompt}\"");
        addon->AtkUnitBase.FireCallbackInt(NoButton);
        this.Finish(BuyResult.Failed($"the board kept asking questions instead of the price, the last being \"{prompt}\""));
        return;
      }

      this.warningPrompts++;
      this.answeredPrompt = prompt;

      this.log.Information($"Answering yes to a Market Board prompt with no price in it: \"{prompt}\"");
      addon->AtkUnitBase.FireCallbackInt(YesButton);
    }

    private unsafe void TryReadResult()
    {
      var proxy = InfoProxyItemSearch.Instance();
      if (proxy == null)
      {
        return;
      }

      var purchased = proxy->LastPurchasedMarketboardItem;
      if (purchased.ListingId == this.purchasedListingBefore || purchased.ListingId != this.targetListingId)
      {
        return;
      }

      this.boughtListings.Add(this.targetListingId);
      this.Finish(BuyResult.Bought(this.targetUnitPrice));
    }

    private unsafe string DescribeCheapest(InfoProxyItemSearch* proxy, bool withTax)
    {
      var buy = this.request!;
      var quality = buy.Hq ? "HQ" : "NQ";
      var limit = buy.MaxUnitPrice.ToString("N0", CultureInfo.CurrentCulture);

      var cheapestMatch = double.MaxValue;
      var cheapestAnySize = double.MaxValue;
      var taken = 0;
      var wrongQuality = 0;

      for (var i = 0; i < (int)proxy->ListingCount; i++)
      {
        ref var listing = ref proxy->Listings[i];

        if (listing.ItemId % HqItemIdOffset != buy.ItemId || listing.Quantity == 0)
        {
          continue;
        }

        if (listing.IsHqItem != buy.Hq)
        {
          wrongQuality++;
          continue;
        }

        var unitPrice = withTax
          ? listing.UnitPrice + ((double)listing.TotalTax / listing.Quantity)
          : listing.UnitPrice;

        cheapestAnySize = Math.Min(cheapestAnySize, unitPrice);

        if (listing.Quantity != buy.Quantity)
        {
          continue;
        }

        // A listing this run has already bought is off the board as far as the game is concerned.
        if (this.boughtListings.Contains(listing.ListingId))
        {
          taken++;
          continue;
        }

        cheapestMatch = Math.Min(cheapestMatch, unitPrice);
      }

      if (cheapestMatch < double.MaxValue)
      {
        var found = cheapestMatch.ToString("N0", CultureInfo.CurrentCulture);
        return $"the cheapest {quality} stack of {buy.Quantity} on the board was {found} per unit, over the {limit} this row will pay";
      }

      if (taken > 0)
      {
        return $"the only {quality} stack{(taken == 1 ? string.Empty : "s")} of {buy.Quantity} left had already been bought earlier in this run";
      }

      if (cheapestAnySize < double.MaxValue)
      {
        var other = cheapestAnySize.ToString("N0", CultureInfo.CurrentCulture);
        return $"nobody was selling a {quality} stack of exactly {buy.Quantity}, only other stack sizes from {other} per unit";
      }

      if (wrongQuality > 0)
      {
        return $"every one of the {wrongQuality} listings on the board was {(buy.Hq ? "NQ" : "HQ")}, and this row wants {quality}";
      }

      return $"the board had no listing of {buy.ItemName} left at all";
    }

    private void Finish(BuyResult result)
    {
      var callback = this.onFinished;

      this.state = State.Idle;
      this.request = null;
      this.onFinished = null;
      this.targetListingId = 0;
      this.reusedListings = false;

      callback?.Invoke(result);
    }
  }
}
