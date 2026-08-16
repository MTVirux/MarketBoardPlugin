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

  /// <summary>
  /// The list of worlds the market board can be queried against, and which one is selected.
  /// </summary>
  public sealed class WorldSelection : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly List<(string Query, string Display)> worlds = new();

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
      this.worlds.Add(("Chaos", "Chaos"));
      this.worlds.Add(("Moogle", "Moogle"));
#endif
    }

    /// <summary>
    /// Gets the available worlds, as pairs of the name used for queries and the name shown in the combo.
    /// </summary>
    public IReadOnlyList<(string Query, string Display)> Worlds => this.worlds;

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
    /// Selects a world and records the matching cross-world and cross-data-centre flags in the configuration.
    /// </summary>
    /// <param name="index">The index into <see cref="Worlds"/>.</param>
    public void Select(int index)
    {
      this.selectedIndex = index;
      this.plugin.Config.CrossDataCenter = index == 0;
      this.plugin.Config.CrossWorld = index == 1;
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
        var currentDc = this.plugin.PlayerState.CurrentWorld.Value.DataCenter;
        var dcWorlds = this.plugin.DataManager.GetExcelSheet<World>()
          .Where(w => w.DataCenter.RowId == currentDc.RowId && w.IsPublic)
          .OrderBy(w => w.Name.ExtractText())
          .Select(w =>
          {
            string displayName = w.Name.ExtractText();

            if (this.plugin.PlayerState.CurrentWorld.Value.RowId == w.RowId)
            {
              displayName += $" {SeIconChar.Hyadelyn.ToChar()}";
            }

            return (w.Name.ExtractText(), displayName);
          });

        var regionName = this.plugin.PlayerState.HomeWorld.Value.DataCenter.Value.Region.RowId switch
        {
          1 => "Japan",
          2 => "North-America",
          3 => "Europe",
          4 => "Oceania",
          5 => "中国",
          _ => string.Empty,
        };

        this.worlds.Clear();
        this.worlds.Add((regionName, $"Cross-DC {SeIconChar.CrossWorld.ToChar()}"));
        this.worlds.Add((currentDc.Value.Name.ExtractText(), $"Cross-World {SeIconChar.CrossWorld.ToChar()}"));
        this.worlds.AddRange(dcWorlds);

        if (this.plugin.Config.CrossDataCenter)
        {
          this.selectedIndex = 0;
        }
        else if (this.plugin.Config.CrossWorld)
        {
          this.selectedIndex = 1;
        }
        else
        {
          this.selectedIndex = this.worlds.FindIndex(w => w.Query == this.plugin.PlayerState.CurrentWorld.Value.Name);
        }

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
  }
}
