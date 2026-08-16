// <copyright file="WorldSelection.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Dalamud.Game.Text;
  using Dalamud.Plugin.Services;
  using Dalamud.Utility;
  using Lumina.Excel.Sheets;
  using Lumina.Extensions;
  using MarketTerror.Extensions;
  using MarketTerror.Helpers;
  using MarketTerror.Models;

  /// <summary>
  /// The list of worlds the market board can be queried against, and which one is selected.
  /// </summary>
  public sealed class WorldSelection : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly List<(string Query, string Display, MarketScope Scope)> worlds = new();

    private ulong playerId;

    private int selectedIndex = -1;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldSelection"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public WorldSelection(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      this.plugin.Framework.Update += this.HandleFrameworkUpdateEvent;

#if DEBUG
      this.worlds.Add(("Chaos", "Chaos", MarketScope.DataCentre));
      this.worlds.Add(("Moogle", "Moogle", MarketScope.DataCentre));
#endif
    }

    /// <summary>
    /// Gets the available worlds, as the name used for queries, the name shown in the combo and how wide the entry reaches.
    /// </summary>
    public IReadOnlyList<(string Query, string Display, MarketScope Scope)> Worlds => this.worlds;

    /// <summary>
    /// Gets the index of the selected world, or -1 when nothing is selected yet.
    /// </summary>
    public int SelectedIndex => this.selectedIndex;

    /// <summary>
    /// Gets a value indicating whether a world is selected.
    /// </summary>
    public bool HasSelection => this.selectedIndex >= 0;

    /// <summary>
    /// Gets the world, data centre or region name that market data is queried for.
    /// </summary>
    public string QueryTarget => this.selectedIndex >= 0 ? this.worlds[this.selectedIndex].Query : string.Empty;

    /// <summary>
    /// Gets the label shown in the world combo.
    /// </summary>
    public string SelectedDisplayName => this.selectedIndex >= 0 ? this.worlds[this.selectedIndex].Display : string.Empty;

    /// <summary>
    /// Gets how wide the selected entry reaches.
    /// </summary>
    public MarketScope SelectedScope => this.selectedIndex >= 0 ? this.worlds[this.selectedIndex].Scope : MarketScope.World;

    /// <summary>
    /// Gets a value indicating whether the selection spans more than one world, so listings carry their own world name.
    /// </summary>
    public bool IsMultiWorld => this.SelectedScope != MarketScope.World;

    /// <summary>
    /// Gets a value indicating whether the selection spans more than one data centre.
    /// </summary>
    public bool IsRegionWide => this.SelectedScope is MarketScope.Region or MarketScope.RegionWithOceania;

    /// <summary>
    /// Gets a value indicating whether the Oceania data centre is priced alongside the selection.
    /// </summary>
    public bool IncludesOceania => this.SelectedScope == MarketScope.RegionWithOceania;

    /// <summary>
    /// Selects a world and records how wide it reaches in the configuration.
    /// </summary>
    /// <param name="index">The index into <see cref="Worlds"/>.</param>
    public void Select(int index)
    {
      this.selectedIndex = index;
      this.plugin.Config.MarketBoardScope = this.SelectedScope;
    }

    /// <summary>
    /// Resolves the data centre name a world belongs to.
    /// </summary>
    /// <param name="worldId">The world row id.</param>
    /// <returns>The data centre name, or an empty string when the world is unknown.</returns>
    public string GetDataCenterName(int worldId)
    {
      var world = this.plugin.DataManager.GetExcelSheet<World>().FirstOrNull(w => w.RowId == worldId);

      if (world != null)
      {
        return world.Value.DataCenter.Value.Name.ExtractText();
      }

      return string.Empty;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.plugin.Framework.Update -= this.HandleFrameworkUpdateEvent;
      this.isDisposed = true;
    }

    private void HandleFrameworkUpdateEvent(IFramework framework)
    {
      if (!this.plugin.PlayerState.IsLoaded)
      {
        this.playerId = 0;
        return;
      }

      if (this.playerId != this.plugin.PlayerState.ContentId)
      {
        var currentWorld = this.plugin.PlayerState.CurrentWorld.Value;
        var currentDc = currentWorld.DataCenter;
        var dcWorlds = this.plugin.DataManager.GetExcelSheet<World>()
          .Where(w => w.DataCenter.RowId == currentDc.RowId && w.IsPublic)
          .OrderBy(w => w.Name.ExtractText())
          .Select(w =>
          {
            string displayName = w.Name.ExtractText();

            if (currentWorld.RowId == w.RowId)
            {
              displayName += $" {SeIconChar.Hyadelyn.ToChar()}";
            }

            return (w.Name.ExtractText(), displayName, MarketScope.World);
          });

        var regionName = WorldRegions.GetName(this.plugin.PlayerState.HomeWorld.Value.DataCenter.Value.Region.RowId);
        var dcName = currentDc.Value.Name.ExtractText();

        this.worlds.Clear();

        // An Oceania world already reaches Oceania at plain region scope, so there is nothing to add on.
        if (regionName != WorldRegions.Oceania)
        {
          this.AddScope(regionName, MarketScope.RegionWithOceania, regionName, WorldRegions.Oceania);
        }

        this.AddScope(regionName, MarketScope.Region, regionName);
        this.AddScope(dcName, MarketScope.DataCentre, dcName);
        this.worlds.AddRange(dcWorlds);

        this.selectedIndex = this.RestoreSelection(currentWorld.Name.ExtractText());

        if (this.worlds.Count > 1)
        {
          this.playerId = this.plugin.PlayerState.ContentId;
        }
      }

      if (this.plugin.PlayerState.ContentId == 0)
      {
        this.playerId = 0;
      }
    }

    private void AddScope(string query, MarketScope scope, params string[] targets)
    {
      this.worlds.Add((query, MarketScopeLabel.For(scope, targets), scope));
    }

    private int RestoreSelection(string currentWorldName)
    {
      var stored = this.plugin.Config.MarketBoardScope;

      // An Oceania character is not offered the "+ Oceania" entry, so it settles for the plain region.
      if (stored == MarketScope.RegionWithOceania && !this.worlds.Any(w => w.Scope == stored))
      {
        stored = MarketScope.Region;
      }

      return stored == MarketScope.World
        ? this.worlds.FindIndex(w => w.Scope == MarketScope.World && w.Query == currentWorldName)
        : this.worlds.FindIndex(w => w.Scope == stored);
    }
  }
}
