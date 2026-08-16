// <copyright file="MarketBoardPurchase.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
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
    private const long ListingsSettleMs = 500;
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

    private State state = State.Idle;
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
    /// Starts buying the requested listing off the board that is currently open.
    /// </summary>
    /// <param name="buyRequest">What may be bought.</param>
    /// <param name="finished">Called once with how the attempt ended.</param>
    public void Start(BuyRequest buyRequest, Action<BuyResult> finished)
    {
      ArgumentNullException.ThrowIfNull(buyRequest);
      ArgumentNullException.ThrowIfNull(finished);

      if (this.IsRunning)
      {
        finished(BuyResult.Failed("another purchase is still running"));
        return;
      }

      this.request = buyRequest;
      this.onFinished = finished;
      this.state = State.WaitingListings;
      this.startTick = Environment.TickCount64;
      this.deadlineTick = this.startTick + ListingsTimeoutMs;
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
        this.Finish(BuyResult.Failed("cancelled"));
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
    /// Checks whether a prompt is about the item being bought.
    /// </summary>
    /// <param name="prompt">The parsed prompt.</param>
    /// <param name="buy">What is being bought.</param>
    /// <returns>True when the prompt names the item.</returns>
    /// <remarks>
    /// The item link the prompt is written with carries the row id, which beats reading the name: the
    /// game writes it in lower case mid-sentence, and other languages do their own thing with it.
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

      return prompt.TextValue.Contains(buy.ItemName, StringComparison.OrdinalIgnoreCase);
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
          if (this.state == State.WaitingListings)
          {
            this.LogListingsTimeout();
          }

          this.Finish(BuyResult.Failed(this.state switch
          {
            State.WaitingListings => Interlocked.Read(ref this.lastOfferingsTick) < this.startTick - EarlyArrivalMs
              ? "the board never sent its listings"
              : "the listings never settled",
            State.WaitingConfirm => "the confirmation never appeared",
            _ => "the purchase was never confirmed by the server",
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
        this.Finish(BuyResult.Failed("the purchase failed unexpectedly"));
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
      var arrived = Interlocked.Read(ref this.lastOfferingsTick);

      return arrived >= this.startTick - EarlyArrivalMs
        && Environment.TickCount64 - arrived >= ListingsSettleMs
        && proxy->ListingCount > 0
        && proxy->SearchItemId % HqItemIdOffset == buy.ItemId;
    }

    private unsafe void TrySelectListing()
    {
      nint addonPtr = this.gameGui.GetAddonByName(ResultAddonName);
      if (addonPtr == nint.Zero)
      {
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
          || listing.Quantity == 0)
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
        this.Finish(BuyResult.Failed(this.DescribeCheapest(proxy, withTax)));
        return;
      }

      if (index >= addon->Results->GetItemCount())
      {
        this.Finish(BuyResult.Failed("the listing was not on the board's list"));
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
      var prompt = parsed.TextValue;

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
        this.Finish(BuyResult.Failed("the confirmation did not match the listing"));
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
        this.Finish(BuyResult.Failed("the board kept asking questions"));
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

      this.Finish(BuyResult.Bought(this.targetUnitPrice));
    }

    private unsafe string DescribeCheapest(InfoProxyItemSearch* proxy, bool withTax)
    {
      var buy = this.request!;
      var cheapest = double.MaxValue;

      for (var i = 0; i < (int)proxy->ListingCount; i++)
      {
        ref var listing = ref proxy->Listings[i];

        if (listing.ItemId % HqItemIdOffset != buy.ItemId || listing.IsHqItem != buy.Hq || listing.Quantity == 0)
        {
          continue;
        }

        var unitPrice = withTax
          ? listing.UnitPrice + ((double)listing.TotalTax / listing.Quantity)
          : listing.UnitPrice;

        cheapest = Math.Min(cheapest, unitPrice);
      }

      return cheapest < double.MaxValue
        ? $"cheapest {cheapest.ToString("N0", CultureInfo.CurrentCulture)}"
        : "no matching listing";
    }

    private void Finish(BuyResult result)
    {
      var callback = this.onFinished;

      this.state = State.Idle;
      this.request = null;
      this.onFinished = null;
      this.targetListingId = 0;

      callback?.Invoke(result);
    }
  }
}
