// <copyright file="MarketBoardAutoSearch.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Diagnostics.CodeAnalysis;
  using System.Text;
  using Dalamud.Game.Addon.Lifecycle;
  using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
  using Dalamud.Game.Text.SeStringHandling;
  using Dalamud.Game.Text.SeStringHandling.Payloads;
  using Dalamud.Plugin;
  using Dalamud.Plugin.Ipc;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.UI;
  using FFXIVClientStructs.FFXIV.Client.UI.Agent;
  using FFXIVClientStructs.FFXIV.Component.GUI;
  using InteropGenerator.Runtime;

  /// <summary>
  /// Automatically fills and runs the game's Market Board search after a plugin-initiated Lifestream travel completes.
  /// </summary>
  public sealed class MarketBoardAutoSearch : IDisposable
  {
    private const string AddonName = "ItemSearch";
    private const string ResultAddonName = "ItemSearchResult";
    private const long PollSettleMs = 500;
    private const long FallbackArmMs = 300000;
    private const long LocalBoardArmMs = 10000;
    private const long AddonSettleMs = 500;
    private const long ResultsTimeoutMs = 10000;
    private const long ResultWindowTimeoutMs = 10000;
    private const long ResultCloseTimeoutMs = 3000;
    private const uint HqItemIdOffset = 1000000;

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IPluginLog log;
    private readonly Func<bool> isEnabled;
    private readonly Func<bool> isOpenResultEnabled;
    private readonly ICallGateSubscriber<bool> lifestreamIsBusy;

    private State state = State.Idle;
    private string itemName = string.Empty;
    private uint itemId;
    private long pollStartTick;
    private long fallbackDeadlineTick;
    private long resultsDeadlineTick;
    private long resultWindowDeadlineTick;
    private long resultCloseDeadlineTick;
    private long addonSettleTick;
    private bool addonSeen;
    private bool resultsLogged;
    private Action<bool>? onFinished;
    private bool forced;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardAutoSearch"/> class.
    /// </summary>
    /// <param name="pluginInterface">The Dalamud plugin interface.</param>
    /// <param name="framework">The framework.</param>
    /// <param name="gameGui">The game GUI.</param>
    /// <param name="addonLifecycle">The addon lifecycle.</param>
    /// <param name="log">The plugin log.</param>
    /// <param name="isEnabled">Returns whether the auto-search feature is currently enabled.</param>
    /// <param name="isOpenResultEnabled">Returns whether the matching result should be opened once the search returns.</param>
    public MarketBoardAutoSearch(
      IDalamudPluginInterface pluginInterface,
      IFramework framework,
      IGameGui gameGui,
      IAddonLifecycle addonLifecycle,
      IPluginLog log,
      Func<bool> isEnabled,
      Func<bool> isOpenResultEnabled)
    {
      ArgumentNullException.ThrowIfNull(pluginInterface);
      this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
      this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
      this.addonLifecycle = addonLifecycle ?? throw new ArgumentNullException(nameof(addonLifecycle));
      this.log = log ?? throw new ArgumentNullException(nameof(log));
      this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
      this.isOpenResultEnabled = isOpenResultEnabled ?? throw new ArgumentNullException(nameof(isOpenResultEnabled));

      this.lifestreamIsBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");

      this.framework.Update += this.OnFrameworkUpdate;
      this.addonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, this.OnItemSearchPostSetup);
    }

    private enum State
    {
      Idle,
      Traveling,
      WaitingAddon,
      ClosingResultWindow,
      WaitingResults,
      WaitingResultWindow,
    }

    /// <summary>
    /// Arms a one-shot auto-search for the given item. A subsequent call replaces the previous one.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    /// <param name="id">The row ID of the item, used to pick the matching result.</param>
    /// <param name="onFinished">Called exactly once with true when the item's listings were opened, false otherwise.</param>
    /// <param name="force">True to run regardless of the auto-search settings.</param>
    public void Arm(string name, uint id, Action<bool>? onFinished = null, bool force = false)
    {
      if (this.Arm(name, id, State.Traveling, FallbackArmMs, onFinished, force))
      {
        this.log.Debug($"Auto-search armed for \"{name}\"; waiting for travel to finish");
      }
      else
      {
        onFinished?.Invoke(false);
      }
    }

    /// <summary>
    /// Arms a one-shot auto-search for a Market Board we just interacted with in-world, skipping the travel wait.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    /// <param name="id">The row ID of the item, used to pick the matching result.</param>
    /// <param name="onFinished">Called exactly once with true when the item's listings were opened, false otherwise.</param>
    /// <param name="force">True to run regardless of the auto-search settings.</param>
    public void ArmForLocalBoard(string name, uint id, Action<bool>? onFinished = null, bool force = false)
    {
      if (this.Arm(name, id, State.WaitingAddon, LocalBoardArmMs, onFinished, force))
      {
        this.log.Debug($"Auto-search armed for \"{name}\"; waiting for the nearby Market Board to open");
      }
      else
      {
        onFinished?.Invoke(false);
      }
    }

    /// <summary>
    /// Cancels any pending auto-search.
    /// </summary>
    public void Disarm()
    {
      this.Finish(false);
    }

    /// <summary>
    /// Fills and runs the Market Board search immediately if its window is currently open. Does nothing otherwise.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    /// <param name="id">The row ID of the item, used to pick the matching result.</param>
    /// <param name="onFinished">Called exactly once with true when the item's listings were opened, false otherwise.</param>
    /// <param name="force">True to run regardless of the auto-search settings.</param>
    public void TryFillNow(string name, uint id, Action<bool>? onFinished = null, bool force = false)
    {
      nint addon = string.IsNullOrWhiteSpace(name) ? nint.Zero : this.gameGui.GetAddonByName(AddonName);

      if (addon == nint.Zero)
      {
        onFinished?.Invoke(false);
        return;
      }

      this.Finish(false);

      this.itemName = name;
      this.itemId = id;
      this.onFinished = onFinished;
      this.forced = force;
      this.BeginSearch(addon);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      this.framework.Update -= this.OnFrameworkUpdate;
      this.addonLifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, this.OnItemSearchPostSetup);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Describes a results row for the verbose log.
    /// </summary>
    /// <param name="index">The index of the row.</param>
    /// <param name="label">The raw label of the row.</param>
    /// <returns>The row's item ID and name.</returns>
    private static unsafe string DescribeRow(int index, CStringPointer label)
    {
      var text = label.HasValue ? SeString.Parse(label.AsSpan()).TextValue.Trim() : "<empty>";
      var agentItemId = GetResultItemId(index);
      return agentItemId == 0 ? $"id none \"{text}\"" : $"id {agentItemId} \"{text}\"";
    }

    /// <summary>
    /// Reads the item ID the game stored for a search result row. The visible labels are truncated
    /// display text without an item link, so the agent's buffer is the only reliable source.
    /// </summary>
    /// <param name="index">The index of the row.</param>
    /// <returns>The item ID, or zero when the buffer has no entry for that row.</returns>
    private static unsafe uint GetResultItemId(int index)
    {
      var agent = AgentItemSearch.Instance();
      if (agent == null || agent->ItemBuffer == null || index < 0 || index >= (int)agent->ItemCount)
      {
        return 0;
      }

      return agent->ItemBuffer[index] % HqItemIdOffset;
    }

    /// <summary>
    /// Strips the ellipsis the game appends when a name is too long for the results column.
    /// </summary>
    /// <param name="text">The visible row text.</param>
    /// <returns>The visible prefix, or an empty string when the text was not truncated.</returns>
    private static string TrimEllipsis(string text)
    {
      if (text.EndsWith("...", StringComparison.Ordinal))
      {
        return text[..^3].TrimEnd();
      }

      if (text.EndsWith('…'))
      {
        return text[..^1].TrimEnd();
      }

      return string.Empty;
    }

    /// <summary>
    /// Clears the pending auto-search and tells whoever armed it how it ended.
    /// </summary>
    /// <param name="opened">True when the item's listings were opened.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A caller's continuation must never throw into the framework update loop")]
    private void Finish(bool opened)
    {
      var callback = this.onFinished;

      this.onFinished = null;
      this.forced = false;
      this.state = State.Idle;
      this.itemName = string.Empty;
      this.itemId = 0;
      this.addonSeen = false;
      this.addonSettleTick = 0;
      this.resultsLogged = false;

      if (callback == null)
      {
        return;
      }

      try
      {
        callback(opened);
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "An auto-search continuation threw");
      }
    }

    private bool Arm(string name, uint id, State initialState, long timeoutMs, Action<bool>? onFinished, bool force)
    {
      if (string.IsNullOrWhiteSpace(name))
      {
        return false;
      }

      this.Finish(false);

      var now = Environment.TickCount64;
      this.itemName = name;
      this.itemId = id;
      this.onFinished = onFinished;
      this.forced = force;
      this.state = initialState;
      this.addonSeen = false;
      this.addonSettleTick = 0;
      this.pollStartTick = now + PollSettleMs;
      this.fallbackDeadlineTick = now + timeoutMs;
      return true;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Must never throw into the framework update loop")]
    private void OnFrameworkUpdate(IFramework framework)
    {
      if (this.state == State.Idle)
      {
        return;
      }

      if (!this.forced && !this.isEnabled())
      {
        this.Disarm();
        return;
      }

      var now = Environment.TickCount64;

      if (this.state == State.WaitingResults)
      {
        var clicked = this.TryOpenResult();

        if (clicked == true)
        {
          // The click only asks the board for the listings; the window opening is what says they are on their way.
          this.state = State.WaitingResultWindow;
          this.resultWindowDeadlineTick = now + ResultWindowTimeoutMs;
        }
        else if (clicked == false)
        {
          this.Finish(false);
        }
        else if (now > this.resultsDeadlineTick)
        {
          this.log.Debug($"No Market Board result for \"{this.itemName}\" (id {this.itemId}) arrived in time; leaving the search as-is");
          this.Finish(false);
        }

        return;
      }

      if (this.state == State.WaitingResultWindow)
      {
        if (this.IsResultWindowOpen())
        {
          this.log.Debug($"The Market Board listings for \"{this.itemName}\" are open");
          this.Finish(true);
        }
        else if (now > this.resultWindowDeadlineTick)
        {
          this.log.Debug($"The Market Board listings for \"{this.itemName}\" never opened");
          this.Finish(false);
        }

        return;
      }

      if (this.state == State.ClosingResultWindow)
      {
        this.WaitForResultWindowToClose(now);
        return;
      }

      if (now > this.fallbackDeadlineTick)
      {
        this.log.Debug($"Auto-search for \"{this.itemName}\" timed out before the Market Board was ready");
        this.Finish(false);
        return;
      }

      if (this.state == State.Traveling)
      {
        if (now < this.pollStartTick)
        {
          return;
        }

        bool busy;
        try
        {
          busy = this.lifestreamIsBusy.InvokeFunc();
        }
        catch
        {
          // Lifestream IPC unavailable: skip the busy-wait and just wait for the addon.
          busy = false;
        }

        if (busy)
        {
          return;
        }

        this.state = State.WaitingAddon;
        this.log.Debug("Travel finished; waiting for the Market Board to open and become ready");
      }

      // WaitingAddon: fire only once the ItemSearch addon exists and is fully built.
      // Firing earlier (e.g. at PostSetup) leaves the search text input unpopulated,
      // so the text never lands and the board shows its default view instead.
      var addonPtr = this.gameGui.GetAddonByName(AddonName);
      if (addonPtr == nint.Zero)
      {
        return;
      }

      if (!this.IsItemSearchReady(addonPtr))
      {
        if (!this.addonSeen)
        {
          this.addonSeen = true;
          this.log.Debug("Market Board addon found but not ready yet; deferring auto-search");
        }

        return;
      }

      // A board we just opened reports ready before it has finished talking to the server,
      // and a search fired at that point comes back empty. Give it a moment first.
      if (this.addonSettleTick == 0)
      {
        this.addonSettleTick = now + AddonSettleMs;
        return;
      }

      if (now < this.addonSettleTick)
      {
        return;
      }

      this.BeginSearch(addonPtr);
    }

    private void OnItemSearchPostSetup(AddonEvent type, AddonArgs args)
    {
      if (this.state == State.Idle)
      {
        return;
      }

      // The Market Board just opened. Switch to polling for readiness; the framework
      // update fires the search once the addon is fully built. Firing here is too early.
      this.state = State.WaitingAddon;
      this.addonSettleTick = 0;
    }

    /// <summary>
    /// Closes the listings window and only searches once it is gone. A search run while the window
    /// is still up leaves it showing the item it was already on, so nothing asks the board for the
    /// new item's listings.
    /// </summary>
    /// <param name="addonPtr">The Market Board addon to search on.</param>
    private unsafe void BeginSearch(nint addonPtr)
    {
      nint resultPtr = this.gameGui.GetAddonByName(ResultAddonName);
      if (resultPtr == nint.Zero)
      {
        this.Fire(addonPtr);
        return;
      }

      ((AtkUnitBase*)resultPtr)->Close(true);
      this.state = State.ClosingResultWindow;
      this.resultCloseDeadlineTick = Environment.TickCount64 + ResultCloseTimeoutMs;
      this.log.Debug($"Closing the listings window before searching for \"{this.itemName}\"");
    }

    /// <summary>
    /// Searches as soon as the listings window has gone, or once waiting for it stops being worth it.
    /// </summary>
    /// <param name="now">The current tick count.</param>
    private void WaitForResultWindowToClose(long now)
    {
      nint addonPtr = this.gameGui.GetAddonByName(AddonName);
      if (addonPtr == nint.Zero)
      {
        this.log.Debug($"The Market Board closed before \"{this.itemName}\" could be searched for");
        this.Finish(false);
        return;
      }

      if (this.gameGui.GetAddonByName(ResultAddonName) == nint.Zero)
      {
        this.Fire(addonPtr);
        return;
      }

      if (now > this.resultCloseDeadlineTick)
      {
        this.log.Debug($"The listings window never closed; searching for \"{this.itemName}\" anyway");
        this.Fire(addonPtr);
      }
    }

    private unsafe bool IsResultWindowOpen()
    {
      nint addonPtr = this.gameGui.GetAddonByName(ResultAddonName);
      if (addonPtr == nint.Zero)
      {
        return false;
      }

      var addon = (AddonItemSearchResult*)addonPtr;
      return addon->AtkUnitBase.IsFullyLoaded() && addon->AtkUnitBase.IsReady;
    }

    private unsafe bool IsItemSearchReady(nint addonPtr)
    {
      if (addonPtr == nint.Zero)
      {
        return false;
      }

      var addon = (AddonItemSearch*)addonPtr;
      return addon->AtkUnitBase.IsFullyLoaded()
        && addon->AtkUnitBase.IsReady
        && addon->SearchTextInput != null;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Failures must log and disarm, never propagate")]
    private unsafe void Fire(nint addonPtr)
    {
      var name = this.itemName;
      var id = this.itemId;
      var callback = this.onFinished;
      var wasForced = this.forced;

      this.onFinished = null;
      this.Disarm();

      this.onFinished = callback;
      this.forced = wasForced;

      if (addonPtr == nint.Zero)
      {
        this.Finish(false);
        return;
      }

      try
      {
        var addon = (AddonItemSearch*)addonPtr;
        if (addon->SearchTextInput == null)
        {
          this.log.Warning("ItemSearch addon has no search text input, skipping auto-search");
          this.Finish(false);
          return;
        }

        var bytes = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* textPtr = bytes)
        {
          addon->SearchTextInput->SetText(textPtr);
        }

        addon->RunSearch();
        this.log.Debug($"Auto-searched \"{name}\" on the Market Board");

        if (id != 0 && (wasForced || this.isOpenResultEnabled()))
        {
          this.itemName = name;
          this.itemId = id;
          this.state = State.WaitingResults;
          this.resultsDeadlineTick = Environment.TickCount64 + ResultsTimeoutMs;
          return;
        }

        this.Finish(false);
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "Failed to auto-search item on the Market Board");
        this.Finish(false);
      }
    }

    /// <summary>
    /// Selects the searched item in the results once the server has answered.
    /// </summary>
    /// <returns>True once the result has been clicked, false when it never can be, and null while it is still waiting.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Must never throw into the framework update loop")]
    private unsafe bool? TryOpenResult()
    {
      try
      {
        nint addonPtr = this.gameGui.GetAddonByName(AddonName);
        if (addonPtr == nint.Zero)
        {
          this.log.Debug("Market Board closed before its results arrived; dropping the pending result selection");
          return false;
        }

        var addon = (AddonItemSearch*)addonPtr;
        var results = addon->ResultsList;
        if (results == null)
        {
          return null;
        }

        var rowCount = results->GetItemCount();
        for (var i = 0; i < rowCount; i++)
        {
          if (!this.RowMatchesItem(i, results->GetItemLabel(i)))
          {
            continue;
          }

          // SelectItem only moves the highlight; the addon requests the listings off the dispatched click.
          results->ScrollToItem((short)i);
          results->SelectItem(i, true);
          results->DispatchItemEvent(i, AtkEventType.ListItemClick);
          this.log.Debug($"Opened \"{this.itemName}\" at result index {i} on the Market Board");
          return true;
        }

        if (rowCount > 0 && !this.resultsLogged)
        {
          this.resultsLogged = true;
          this.log.Debug($"Market Board returned {rowCount} results, none of them id {this.itemId}");

          for (var i = 0; i < rowCount; i++)
          {
            this.log.Verbose($"Market Board result {i}: {DescribeRow(i, results->GetItemLabel(i))}");
          }
        }

        return null;
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "Failed to open the Market Board search result");
        return false;
      }
    }

    /// <summary>
    /// Checks whether a results row is the item that was searched for. The row's item ID comes from the
    /// search agent; the visible label is only a fallback because the game truncates long names.
    /// </summary>
    /// <param name="index">The index of the row.</param>
    /// <param name="label">The raw label of the row.</param>
    /// <returns>True when the row is the searched item.</returns>
    private unsafe bool RowMatchesItem(int index, CStringPointer label)
    {
      if (GetResultItemId(index) == this.itemId)
      {
        return true;
      }

      if (!label.HasValue)
      {
        return false;
      }

      var parsed = SeString.Parse(label.AsSpan());

      foreach (var payload in parsed.Payloads)
      {
        if (payload is ItemPayload item && item.ItemId % HqItemIdOffset == this.itemId)
        {
          return true;
        }
      }

      var text = parsed.TextValue.Trim();
      if (string.Equals(text, this.itemName, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }

      var visiblePrefix = TrimEllipsis(text);
      return visiblePrefix.Length > 0
        && this.itemName.StartsWith(visiblePrefix, StringComparison.OrdinalIgnoreCase);
    }
  }
}
