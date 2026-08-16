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
  using FFXIVClientStructs.FFXIV.Component.GUI;
  using InteropGenerator.Runtime;

  /// <summary>
  /// Automatically fills and runs the game's Market Board search after a plugin-initiated Lifestream travel completes.
  /// </summary>
  public sealed class MarketBoardAutoSearch : IDisposable
  {
    private const string AddonName = "ItemSearch";
    private const long PollSettleMs = 500;
    private const long FallbackArmMs = 300000;
    private const long LocalBoardArmMs = 10000;
    private const long AddonSettleMs = 500;
    private const long ResultsTimeoutMs = 10000;
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
    private long addonSettleTick;
    private bool addonSeen;
    private bool resultsLogged;

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
      WaitingResults,
    }

    /// <summary>
    /// Arms a one-shot auto-search for the given item. A subsequent call replaces the previous one.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    /// <param name="id">The row ID of the item, used to pick the matching result.</param>
    public void Arm(string name, uint id)
    {
      if (this.Arm(name, id, State.Traveling, FallbackArmMs))
      {
        this.log.Debug($"Auto-search armed for \"{name}\"; waiting for travel to finish");
      }
    }

    /// <summary>
    /// Arms a one-shot auto-search for a Market Board we just interacted with in-world, skipping the travel wait.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    /// <param name="id">The row ID of the item, used to pick the matching result.</param>
    public void ArmForLocalBoard(string name, uint id)
    {
      if (this.Arm(name, id, State.WaitingAddon, LocalBoardArmMs))
      {
        this.log.Debug($"Auto-search armed for \"{name}\"; waiting for the nearby Market Board to open");
      }
    }

    /// <summary>
    /// Cancels any pending auto-search.
    /// </summary>
    public void Disarm()
    {
      this.state = State.Idle;
      this.itemName = string.Empty;
      this.itemId = 0;
      this.addonSeen = false;
      this.addonSettleTick = 0;
      this.resultsLogged = false;
    }

    /// <summary>
    /// Fills and runs the Market Board search immediately if its window is currently open. Does nothing otherwise.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    /// <param name="id">The row ID of the item, used to pick the matching result.</param>
    public void TryFillNow(string name, uint id)
    {
      if (string.IsNullOrWhiteSpace(name))
      {
        return;
      }

      var addon = this.gameGui.GetAddonByName(AddonName);
      if (addon == nint.Zero)
      {
        return;
      }

      this.itemName = name;
      this.itemId = id;
      this.Fire(addon);
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
    /// <param name="label">The raw label of the row.</param>
    /// <returns>The row's item ID and name.</returns>
    private static string DescribeRow(CStringPointer label)
    {
      if (!label.HasValue)
      {
        return "<empty>";
      }

      var parsed = SeString.Parse(label.AsSpan());
      var text = parsed.TextValue.Trim();

      foreach (var payload in parsed.Payloads)
      {
        if (payload is ItemPayload item)
        {
          return $"id {item.ItemId} \"{text}\"";
        }
      }

      return $"id none \"{text}\"";
    }

    private bool Arm(string name, uint id, State initialState, long timeoutMs)
    {
      if (string.IsNullOrWhiteSpace(name))
      {
        return false;
      }

      var now = Environment.TickCount64;
      this.itemName = name;
      this.itemId = id;
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

      if (!this.isEnabled())
      {
        this.Disarm();
        return;
      }

      var now = Environment.TickCount64;

      if (this.state == State.WaitingResults)
      {
        if (this.TryOpenResult())
        {
          this.Disarm();
        }
        else if (now > this.resultsDeadlineTick)
        {
          this.log.Debug($"No Market Board result for \"{this.itemName}\" (id {this.itemId}) arrived in time; leaving the search as-is");
          this.Disarm();
        }

        return;
      }

      if (now > this.fallbackDeadlineTick)
      {
        this.log.Debug($"Auto-search for \"{this.itemName}\" timed out before the Market Board was ready");
        this.Disarm();
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

      this.Fire(addonPtr);
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
      this.Disarm();

      if (addonPtr == nint.Zero)
      {
        return;
      }

      try
      {
        var addon = (AddonItemSearch*)addonPtr;
        if (addon->SearchTextInput == null)
        {
          this.log.Warning("ItemSearch addon has no search text input, skipping auto-search");
          return;
        }

        var bytes = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* textPtr = bytes)
        {
          addon->SearchTextInput->SetText(textPtr);
        }

        addon->RunSearch();
        this.log.Debug($"Auto-searched \"{name}\" on the Market Board");

        if (id != 0 && this.isOpenResultEnabled())
        {
          this.itemName = name;
          this.itemId = id;
          this.state = State.WaitingResults;
          this.resultsDeadlineTick = Environment.TickCount64 + ResultsTimeoutMs;
        }
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "Failed to auto-search item on the Market Board");
      }
    }

    /// <summary>
    /// Selects the searched item in the results once the server has answered.
    /// </summary>
    /// <returns>True once the result has been opened, or when it can never be.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Must never throw into the framework update loop")]
    private unsafe bool TryOpenResult()
    {
      try
      {
        nint addonPtr = this.gameGui.GetAddonByName(AddonName);
        if (addonPtr == nint.Zero)
        {
          this.log.Debug("Market Board closed before its results arrived; dropping the pending result selection");
          return true;
        }

        var addon = (AddonItemSearch*)addonPtr;
        var results = addon->ResultsList;
        if (results == null)
        {
          return false;
        }

        var rowCount = results->GetItemCount();
        for (var i = 0; i < rowCount; i++)
        {
          if (!this.RowMatchesItem(results->GetItemLabel(i)))
          {
            continue;
          }

          // SelectItem only moves the highlight; the addon requests the listings off the dispatched click.
          results->ScrollToItem((short)i);
          results->SelectItem(i, true);
          results->DispatchItemEvent(i, AtkEventType.ListItemClick);
          this.log.Debug($"Opened \"{this.itemName}\" at result index {i} on the Market Board");
          this.log.Information($"[MBDIAG] dispatched index={i} of {rowCount} for id {this.itemId}");
          return true;
        }

        if (rowCount > 0 && !this.resultsLogged)
        {
          this.resultsLogged = true;
          this.log.Debug($"Market Board returned {rowCount} results, none of them id {this.itemId}");

          for (var i = 0; i < rowCount; i++)
          {
            this.log.Verbose($"Market Board result {i}: {DescribeRow(results->GetItemLabel(i))}");
          }
        }

        return false;
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "Failed to open the Market Board search result");
        return true;
      }
    }

    /// <summary>
    /// Checks whether a results row is the item that was searched for. Rows carry an item link payload,
    /// so the row's own item ID is matched first and the visible name is only a fallback.
    /// </summary>
    /// <param name="label">The raw label of the row.</param>
    /// <returns>True when the row is the searched item.</returns>
    private bool RowMatchesItem(CStringPointer label)
    {
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

      return string.Equals(parsed.TextValue.Trim(), this.itemName, StringComparison.OrdinalIgnoreCase);
    }
  }
}
