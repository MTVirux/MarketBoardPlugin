// <copyright file="MarketBoardRefresh.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Diagnostics.CodeAnalysis;
  using System.Threading;
  using Dalamud.Game.Network.Structures;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.UI;
  using FFXIVClientStructs.FFXIV.Component.GUI;

  /// <summary>
  /// Opens an item's listings again on a Market Board that is already open, then its sales history.
  /// </summary>
  /// <remarks>
  /// Buying closes the listings window, so the last thing a Universalis uploader saw still has the
  /// bought listing in it. Asking the server for the listings and then the history is what puts the
  /// sale up, since each of them is only sent when the window that shows it is opened.
  /// </remarks>
  public sealed class MarketBoardRefresh : IDisposable
  {
    private const string ResultAddonName = "ItemSearchResult";
    private const long ClickTimeoutMs = 5000;
    private const long HistoryTimeoutMs = 5000;
    private const uint HqItemIdOffset = 1000000;

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IMarketBoard marketBoard;
    private readonly IPluginLog log;
    private readonly MarketBoardAutoSearch autoSearch;

    private State state = State.Idle;
    private string itemName = string.Empty;
    private long watchedItemId;
    private long deadlineTick;
    private long historyBaselineTick;
    private long lastHistoryTick;
    private Action? onFinished;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardRefresh"/> class.
    /// </summary>
    /// <param name="framework">The framework.</param>
    /// <param name="gameGui">The game GUI.</param>
    /// <param name="marketBoard">The market board events the game raises as it receives data.</param>
    /// <param name="log">The plugin log.</param>
    /// <param name="autoSearch">The auto-search, which is what opens the listings again.</param>
    public MarketBoardRefresh(IFramework framework, IGameGui gameGui, IMarketBoard marketBoard, IPluginLog log, MarketBoardAutoSearch autoSearch)
    {
      this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
      this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
      this.marketBoard = marketBoard ?? throw new ArgumentNullException(nameof(marketBoard));
      this.log = log ?? throw new ArgumentNullException(nameof(log));
      this.autoSearch = autoSearch ?? throw new ArgumentNullException(nameof(autoSearch));

      this.framework.Update += this.OnFrameworkUpdate;
      this.marketBoard.HistoryReceived += this.OnHistoryReceived;
    }

    private enum State
    {
      Idle,
      WaitingListings,
      ClickingHistory,
      WaitingHistory,
    }

    /// <summary>
    /// Gets a value indicating whether a refresh is in flight.
    /// </summary>
    public bool IsRunning => this.state != State.Idle;

    /// <summary>
    /// Opens an item's listings and sales history again on the board that is currently open.
    /// </summary>
    /// <param name="name">The name of the item to open.</param>
    /// <param name="id">The row id of the item to open.</param>
    /// <param name="finished">Called once when the refresh has run its course, however it ended.</param>
    public void Start(string name, uint id, Action finished)
    {
      ArgumentNullException.ThrowIfNull(finished);

      if (this.IsRunning)
      {
        finished();
        return;
      }

      this.itemName = name;
      this.onFinished = finished;
      this.state = State.WaitingListings;
      Interlocked.Exchange(ref this.watchedItemId, id);

      this.autoSearch.TryFillNow(name, id, this.OnListingsOpened, true);
    }

    /// <summary>
    /// Abandons a refresh that is still going.
    /// </summary>
    public void Cancel()
    {
      if (this.IsRunning)
      {
        this.Finish();
      }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      this.framework.Update -= this.OnFrameworkUpdate;
      this.marketBoard.HistoryReceived -= this.OnHistoryReceived;
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Fires an addon's own click event for one of its buttons.
    /// </summary>
    /// <param name="addon">The addon the button belongs to.</param>
    /// <param name="button">The button to click.</param>
    /// <returns>True when the button had a click event to fire.</returns>
    /// <remarks>
    /// The event carries the parameter the addon uses to tell its buttons apart, so it has to be read
    /// off the button's own node rather than guessed at.
    /// </remarks>
    private static unsafe bool ClickButton(AtkUnitBase* addon, AtkComponentButton* button)
    {
      var owner = button->AtkComponentBase.OwnerNode;
      if (owner == null)
      {
        return false;
      }

      for (var raised = owner->AtkResNode.AtkEventManager.Event; raised != null; raised = raised->NextEvent)
      {
        if (raised->State.EventType != AtkEventType.ButtonClick)
        {
          continue;
        }

        var data = default(AtkEventData);
        addon->ReceiveEvent(AtkEventType.ButtonClick, (int)raised->Param, raised, &data);
        return true;
      }

      return false;
    }

    /// <summary>
    /// Moves on to the sales history once the listings are back up.
    /// </summary>
    /// <param name="opened">True when the listings were opened.</param>
    private void OnListingsOpened(bool opened)
    {
      if (this.state != State.WaitingListings)
      {
        return;
      }

      if (!opened)
      {
        this.log.Debug($"Could not open the listings of \"{this.itemName}\" again after buying it");
        this.Finish();
        return;
      }

      this.state = State.ClickingHistory;
      this.deadlineTick = Environment.TickCount64 + ClickTimeoutMs;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Must never throw into the framework update loop")]
    private void OnFrameworkUpdate(IFramework updatedFramework)
    {
      if (this.state is State.Idle or State.WaitingListings)
      {
        return;
      }

      try
      {
        if (Environment.TickCount64 > this.deadlineTick)
        {
          this.log.Debug(this.state == State.ClickingHistory
            ? $"The sales history button for \"{this.itemName}\" never became clickable"
            : $"The sales history of \"{this.itemName}\" never arrived");

          this.Finish();
          return;
        }

        if (this.state == State.ClickingHistory)
        {
          this.TryOpenHistory();
          return;
        }

        if (Interlocked.Read(ref this.lastHistoryTick) >= this.historyBaselineTick)
        {
          this.log.Debug($"Opened the listings and sales history of \"{this.itemName}\" again");
          this.Finish();
        }
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "The Market Board refresh threw");
        this.Finish();
      }
    }

    /// <summary>
    /// Stamps the clock when the sales history of the item being refreshed lands.
    /// </summary>
    /// <param name="history">The history the server sent.</param>
    /// <remarks>Raised off the framework thread.</remarks>
    private void OnHistoryReceived(IMarketBoardHistory history)
    {
      if (history != null && (long)(history.ItemId % HqItemIdOffset) == Interlocked.Read(ref this.watchedItemId))
      {
        Interlocked.Exchange(ref this.lastHistoryTick, Environment.TickCount64);
      }
    }

    private unsafe void TryOpenHistory()
    {
      nint addonPtr = this.gameGui.GetAddonByName(ResultAddonName);
      if (addonPtr == nint.Zero)
      {
        return;
      }

      var addon = (AddonItemSearchResult*)addonPtr;
      if (!addon->AtkUnitBase.IsFullyLoaded() || !addon->AtkUnitBase.IsReady || addon->History == null || !addon->History->IsEnabled)
      {
        return;
      }

      // Stamped before the click so a history that comes straight back still counts as this one's.
      this.historyBaselineTick = Environment.TickCount64;

      if (!ClickButton(&addon->AtkUnitBase, addon->History))
      {
        this.log.Debug($"The sales history button for \"{this.itemName}\" had no click event to fire");
        this.Finish();
        return;
      }

      this.state = State.WaitingHistory;
      this.deadlineTick = this.historyBaselineTick + HistoryTimeoutMs;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A caller's continuation must never throw into the framework update loop")]
    private void Finish()
    {
      var callback = this.onFinished;

      this.state = State.Idle;
      this.onFinished = null;
      this.itemName = string.Empty;
      Interlocked.Exchange(ref this.watchedItemId, 0);

      if (callback == null)
      {
        return;
      }

      try
      {
        callback();
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "A Market Board refresh continuation threw");
      }
    }
  }
}
