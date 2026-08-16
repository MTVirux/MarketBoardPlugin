// <copyright file="MarketBoardPurchase.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Diagnostics.CodeAnalysis;
  using System.Globalization;
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
    private const long ConfirmTimeoutMs = 10000;
    private const long ResultTimeoutMs = 15000;
    private const uint HqItemIdOffset = 1000000;
    private const int YesButton = 0;
    private const int NoButton = 1;

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private readonly Func<bool> includesSalesTax;

    private State state = State.Idle;
    private BuyRequest? request;
    private Action<BuyResult>? onFinished;
    private long deadlineTick;
    private ulong purchasedListingBefore;
    private ulong targetListingId;
    private double targetUnitPrice;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardPurchase"/> class.
    /// </summary>
    /// <param name="framework">The framework.</param>
    /// <param name="gameGui">The game GUI.</param>
    /// <param name="log">The plugin log.</param>
    /// <param name="includesSalesTax">Returns whether saved prices have the gil sales tax folded in.</param>
    public MarketBoardPurchase(IFramework framework, IGameGui gameGui, IPluginLog log, Func<bool> includesSalesTax)
    {
      this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
      this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
      this.log = log ?? throw new ArgumentNullException(nameof(log));
      this.includesSalesTax = includesSalesTax ?? throw new ArgumentNullException(nameof(includesSalesTax));

      this.framework.Update += this.OnFrameworkUpdate;
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
      this.deadlineTick = Environment.TickCount64 + ListingsTimeoutMs;
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
          this.Finish(BuyResult.Failed(this.state switch
          {
            State.WaitingListings => "the listings never arrived",
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
      if (proxy == null || proxy->ListingCount == 0)
      {
        return;
      }

      var buy = this.request!;
      if (proxy->SearchItemId % HqItemIdOffset != buy.ItemId)
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
      var prompt = addon->PromptText->NodeText.ToString();
      var asked = LargestNumber(prompt);

      // A gil figure is written with separators, so allow a rounding gil either way rather than an exact compare.
      var namesItem = prompt.Contains(buy.ItemName, StringComparison.OrdinalIgnoreCase);
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
