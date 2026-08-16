// <copyright file="ShoppingListScope.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Lumina.Excel.Sheets;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// The world the shopping list shops from and how wide it prices around it.
  /// </summary>
  /// <remarks>
  /// Kept apart from <see cref="WorldSelection"/> on purpose: the buy list can be priced somewhere
  /// else entirely without moving the market board window's own world combo.
  /// </remarks>
  public sealed class ShoppingListScope
  {
    /// <summary>
    /// Oceania sits on its own, so it is only ever priced when it is asked for by name.
    /// </summary>
    private const string OceaniaRegion = "Oceania";

    private readonly MarketTerrorPlugin plugin;

    private readonly List<WorldEntry> worlds = new List<WorldEntry>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingListScope"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public ShoppingListScope(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    /// <summary>
    /// Gets every public world, ordered by region, then data centre, then name.
    /// </summary>
    public IReadOnlyList<WorldEntry> Worlds
    {
      get
      {
        this.EnsureLoaded();
        return this.worlds;
      }
    }

    /// <summary>
    /// Gets the name of the selected world, or an empty string when none could be resolved yet.
    /// </summary>
    public string SelectedWorld => this.SelectedEntry?.Name ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether a world is selected.
    /// </summary>
    public bool HasSelection => this.SelectedEntry != null;

    /// <summary>
    /// Gets how wide the price queries reach around the selected world.
    /// </summary>
    public MarketScope Scope => this.plugin.Config.ShoppingListScopeLevel;

    /// <summary>
    /// Gets the world, data centre or region names the prices are fetched for, cheapest answer winning.
    /// </summary>
    public IReadOnlyList<string> QueryTargets => this.TargetsFor(this.Scope);

    /// <summary>
    /// Gets the query targets as one label, for tooltips.
    /// </summary>
    public string QueryTargetLabel => string.Join(" and ", this.QueryTargets);

    /// <summary>
    /// Gets the selected world, falling back to the character's home world the first time it is known.
    /// </summary>
    private WorldEntry? SelectedEntry
    {
      get
      {
        this.EnsureLoaded();

        var stored = this.plugin.Config.ShoppingListScopeWorld;
        var entry = this.Find(stored);

        if (entry != null)
        {
          return entry;
        }

        return this.ApplyHomeWorldDefault();
      }
    }

    /// <summary>
    /// Gets the world, data centre or region names a scope would price at, for the selected world.
    /// </summary>
    /// <param name="scope">The scope to resolve.</param>
    /// <returns>The names to query, empty when no world is selected yet.</returns>
    public IReadOnlyList<string> TargetsFor(MarketScope scope)
    {
      var entry = this.SelectedEntry;

      if (entry == null)
      {
        return Array.Empty<string>();
      }

      return scope switch
      {
        MarketScope.DataCentre => new[] { entry.DataCentre },
        MarketScope.Region => new[] { entry.Region },
        MarketScope.RegionWithOceania => entry.Region == OceaniaRegion
          ? new[] { entry.Region }
          : new[] { entry.Region, OceaniaRegion },
        _ => new[] { entry.Name },
      };
    }

    /// <summary>
    /// Selects the world the buy list shops from.
    /// </summary>
    /// <param name="worldName">The world name.</param>
    public void SelectWorld(string worldName)
    {
      if (this.plugin.Config.ShoppingListScopeWorld == worldName)
      {
        return;
      }

      this.plugin.Config.ShoppingListScopeWorld = worldName;
      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }

    /// <summary>
    /// Selects the character's home world, when it is known.
    /// </summary>
    public void SelectHomeWorld()
    {
      this.EnsureLoaded();
      this.ApplyHomeWorldDefault();
    }

    /// <summary>
    /// Sets how wide the price queries reach around the selected world.
    /// </summary>
    /// <param name="scope">The scope to query at.</param>
    public void SelectScope(MarketScope scope)
    {
      if (this.plugin.Config.ShoppingListScopeLevel == scope)
      {
        return;
      }

      this.plugin.Config.ShoppingListScopeLevel = scope;
      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }

    private WorldEntry? Find(string worldName)
    {
      return worldName.Length == 0
        ? null
        : this.worlds.FirstOrDefault(w => string.Equals(w.Name, worldName, StringComparison.Ordinal));
    }

    private WorldEntry? ApplyHomeWorldDefault()
    {
      if (!this.plugin.PlayerState.IsLoaded)
      {
        return null;
      }

      var entry = this.Find(this.plugin.PlayerState.HomeWorld.Value.Name.ExtractText());

      if (entry == null)
      {
        return null;
      }

      this.SelectWorld(entry.Name);

      return entry;
    }

    private void EnsureLoaded()
    {
      if (this.worlds.Count > 0)
      {
        return;
      }

      var entries = this.plugin.DataManager.GetExcelSheet<World>()
        .Where(w => w.IsPublic && w.DataCenter.RowId > 0)
        .Select(w => new WorldEntry(
          w.Name.ExtractText(),
          w.DataCenter.Value.Name.ExtractText(),
          WorldRegions.GetName(w.DataCenter.Value.Region.RowId)))
        .Where(w => w.Name.Length > 0 && w.DataCentre.Length > 0 && w.Region.Length > 0)
        .OrderBy(w => w.Region, StringComparer.Ordinal)
        .ThenBy(w => w.DataCentre, StringComparer.Ordinal)
        .ThenBy(w => w.Name, StringComparer.Ordinal);

      this.worlds.AddRange(entries);
    }
  }
}
