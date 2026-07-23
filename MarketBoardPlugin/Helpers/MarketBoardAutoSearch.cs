// <copyright file="MarketBoardAutoSearch.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Helpers
{
  using System;
  using System.Diagnostics.CodeAnalysis;
  using System.Text;
  using Dalamud.Game.Addon.Lifecycle;
  using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
  using Dalamud.Plugin;
  using Dalamud.Plugin.Ipc;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.UI;

  /// <summary>
  /// Automatically fills and runs the game's Market Board search after a plugin-initiated Lifestream travel completes.
  /// </summary>
  public sealed class MarketBoardAutoSearch : IDisposable
  {
    private const string AddonName = "ItemSearch";
    private const long PollSettleMs = 500;
    private const long FallbackArmMs = 300000;

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IPluginLog log;
    private readonly Func<bool> isEnabled;
    private readonly ICallGateSubscriber<bool> lifestreamIsBusy;

    private State state = State.Idle;
    private string itemName = string.Empty;
    private long pollStartTick;
    private long fallbackDeadlineTick;
    private bool addonSeen;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardAutoSearch"/> class.
    /// </summary>
    /// <param name="pluginInterface">The Dalamud plugin interface.</param>
    /// <param name="framework">The framework.</param>
    /// <param name="gameGui">The game GUI.</param>
    /// <param name="addonLifecycle">The addon lifecycle.</param>
    /// <param name="log">The plugin log.</param>
    /// <param name="isEnabled">Returns whether the auto-search feature is currently enabled.</param>
    public MarketBoardAutoSearch(
      IDalamudPluginInterface pluginInterface,
      IFramework framework,
      IGameGui gameGui,
      IAddonLifecycle addonLifecycle,
      IPluginLog log,
      Func<bool> isEnabled)
    {
      ArgumentNullException.ThrowIfNull(pluginInterface);
      this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
      this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
      this.addonLifecycle = addonLifecycle ?? throw new ArgumentNullException(nameof(addonLifecycle));
      this.log = log ?? throw new ArgumentNullException(nameof(log));
      this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));

      this.lifestreamIsBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");

      this.framework.Update += this.OnFrameworkUpdate;
      this.addonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, this.OnItemSearchPostSetup);
    }

    private enum State
    {
      Idle,
      Traveling,
      WaitingAddon,
    }

    /// <summary>
    /// Arms a one-shot auto-search for the given item name. A subsequent call replaces the previous one.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    public void Arm(string name)
    {
      if (string.IsNullOrWhiteSpace(name))
      {
        return;
      }

      var now = Environment.TickCount64;
      this.itemName = name;
      this.state = State.Traveling;
      this.addonSeen = false;
      this.pollStartTick = now + PollSettleMs;
      this.fallbackDeadlineTick = now + FallbackArmMs;
      this.log.Debug($"Auto-search armed for \"{name}\"; waiting for travel to finish");
    }

    /// <summary>
    /// Cancels any pending auto-search.
    /// </summary>
    public void Disarm()
    {
      this.state = State.Idle;
      this.itemName = string.Empty;
      this.addonSeen = false;
    }

    /// <summary>
    /// Fills and runs the Market Board search immediately if its window is currently open. Does nothing otherwise.
    /// </summary>
    /// <param name="name">The item name to search for.</param>
    public void TryFillNow(string name)
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
      this.Fire(addon);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      this.framework.Update -= this.OnFrameworkUpdate;
      this.addonLifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, this.OnItemSearchPostSetup);
      GC.SuppressFinalize(this);
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
      }
      catch (Exception ex)
      {
        this.log.Error(ex, "Failed to auto-search item on the Market Board");
      }
    }
  }
}
