// <copyright file="IntegrationStatus.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;

  /// <summary>
  /// The state of every optional plugin and service MarketTerror works with, and which of their
  /// warnings the user has dismissed.
  /// </summary>
  public sealed class IntegrationStatus
  {
    private const string UniversalisDetail = "Listings and sale history come from Universalis as you browse.";

    private const string FfxivmtDetail = "Gilflux rankings in the Stats tab come from the FFXIVMT API.";

    private readonly MarketTerrorPlugin plugin;

    private readonly MarketDataProvider marketData;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationStatus"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="marketData">The market data provider the service states are read from.</param>
    public IntegrationStatus(MarketTerrorPlugin plugin, MarketDataProvider marketData)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
    }

    /// <summary>
    /// Gets every integration, in the order they are listed to the user.
    /// </summary>
    public IReadOnlyList<Integration> All =>
    [
      this.Lifestream(),
      this.Universalis(),
      this.Ffxivmt(),
    ];

    private ICollection<string> DismissedNames => this.plugin.Config.DismissedIntegrationWarnings;

    /// <summary>
    /// Hides an integration's warning until it starts working and fails again.
    /// </summary>
    /// <param name="integration">The integration to dismiss.</param>
    public void Dismiss(Integration integration)
    {
      if (!integration.CanDismiss || !integration.IsWarning || this.DismissedNames.Contains(integration.Name))
      {
        return;
      }

      this.DismissedNames.Add(integration.Name);
      this.Save();
    }

    /// <summary>
    /// Brings a dismissed integration's warning back.
    /// </summary>
    /// <param name="integration">The integration to restore.</param>
    public void Restore(Integration integration)
    {
      if (this.DismissedNames.Remove(integration.Name))
      {
        this.Save();
      }
    }

    /// <summary>
    /// Drops the dismissal of every integration that is currently working.
    /// </summary>
    /// <remarks>
    /// This is what re-arms a warning: a dismissal only lives as long as the integration stays
    /// broken, so one that recovers and then fails again is reported afresh.
    /// </remarks>
    public void Update()
    {
      if (this.DismissedNames.Count == 0)
      {
        return;
      }

      var restored = false;

      foreach (var integration in this.All)
      {
        if (!integration.IsWarning && this.DismissedNames.Remove(integration.Name))
        {
          restored = true;
        }
      }

      if (restored)
      {
        this.Save();
      }
    }

    private Integration Lifestream()
    {
      if (this.plugin.IsLifestreamAvailable)
      {
        return this.Build(
          "Lifestream",
          IntegrationState.Ok,
          "enabled",
          "Clicking a listing can travel to its world and open the Market Board there.",
          true);
      }

      var disabled = this.plugin.IsLifestreamDisabled;
      var detail = disabled
        ? "Listing clicks stay where you are. Switch Lifestream back on in the plugin\ninstaller to travel to the listing's world automatically."
        : "Listing clicks stay where you are. Install Lifestream to travel to the\nlisting's world automatically.";

      return this.Build(
        "Lifestream",
        IntegrationState.Idle,
        disabled ? "installed but disabled" : "not detected",
        detail,
        true);
    }

    /// <summary>
    /// Builds the Universalis entry, which is never dismissible: every listing and sale in the
    /// plugin comes from it, so a silent outage would look like empty market data instead.
    /// </summary>
    /// <returns>The Universalis integration.</returns>
    private Integration Universalis()
    {
      var up = this.marketData.IsUniversalisUp;

      if (up == null)
      {
        return this.Build("Universalis", IntegrationState.Idle, "checking", UniversalisDetail, false);
      }

      return up.Value
        ? this.Build("Universalis", IntegrationState.Ok, "reachable", UniversalisDetail, false)
        : this.Build(
          "Universalis",
          IntegrationState.Down,
          "not answering",
          "Listings and sale history cannot be fetched right now.\nCheck status.universalis.app.",
          false);
    }

    private Integration Ffxivmt()
    {
      var up = this.marketData.IsFFXIVMTUp;

      if (up == null)
      {
        return this.Build("FFXIVMT", IntegrationState.Idle, "checking", FfxivmtDetail, true);
      }

      return up.Value
        ? this.Build("FFXIVMT", IntegrationState.Ok, "reachable", FfxivmtDetail, true)
        : this.Build(
          "FFXIVMT",
          IntegrationState.Down,
          "not answering",
          "The FFXIVMT API is not answering, so the Stats tab has no rankings to show.",
          true);
    }

    private Integration Build(string name, IntegrationState state, string status, string detail, bool canDismiss)
    {
      return new Integration(name, state, status, detail, canDismiss, canDismiss && this.DismissedNames.Contains(name));
    }

    private void Save()
    {
      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }
  }
}
