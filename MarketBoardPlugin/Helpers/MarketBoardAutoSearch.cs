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
    private const long GraceMs = 3000;
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
    private long graceEndTick;
    private long fallbackDeadlineTick;

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
      Grace,
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
      this.pollStartTick = now + PollSettleMs;
      this.fallbackDeadlineTick = now + FallbackArmMs;
    }

    /// <summary>
    /// Cancels any pending auto-search.
    /// </summary>
    public void Disarm()
    {
      this.state = State.Idle;
      this.itemName = string.Empty;
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
          // Lifestream IPC unavailable: flat timeout, firing only happens via PostSetup.
          if (now > this.fallbackDeadlineTick)
          {
            this.Disarm();
          }

          return;
        }

        if (busy)
        {
          return;
        }

        var addon = this.gameGui.GetAddonByName(AddonName);
        if (addon != nint.Zero)
        {
          this.Fire(addon);
          return;
        }

        this.state = State.Grace;
        this.graceEndTick = now + GraceMs;
        return;
      }

      // Grace: waiting for the addon to open (PostSetup fires the search).
      if (now > this.graceEndTick)
      {
        this.Disarm();
      }
    }

    private void OnItemSearchPostSetup(AddonEvent type, AddonArgs args)
    {
      if (this.state == State.Idle)
      {
        return;
      }

      this.Fire((nint)args.Addon);
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
