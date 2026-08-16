// <copyright file="WorldCatalogue.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Lumina.Excel.Sheets;
  using MarketTerror.Helpers;

  /// <summary>
  /// Every world the market board can be priced at, shared by the pickers in both windows.
  /// </summary>
  public sealed class WorldCatalogue
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly List<WorldEntry> worlds = new List<WorldEntry>();

    private readonly Dictionary<string, WorldEntry> byName = new Dictionary<string, WorldEntry>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldCatalogue"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public WorldCatalogue(MarketTerrorPlugin plugin)
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
    /// Looks a world up by name.
    /// </summary>
    /// <param name="worldName">The world name.</param>
    /// <returns>The entry, or null when the name is empty or unknown.</returns>
    public WorldEntry? Find(string worldName)
    {
      if (string.IsNullOrEmpty(worldName))
      {
        return null;
      }

      this.EnsureLoaded();

      return this.byName.TryGetValue(worldName, out var entry) ? entry : null;
    }

    /// <summary>
    /// Lists the worlds of one data centre, in name order.
    /// </summary>
    /// <param name="dataCentre">The data centre name.</param>
    /// <returns>The worlds sitting on it.</returns>
    public IEnumerable<WorldEntry> InDataCentre(string dataCentre)
    {
      this.EnsureLoaded();

      return this.worlds.Where(w => string.Equals(w.DataCentre, dataCentre, StringComparison.Ordinal));
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

      foreach (var world in this.worlds)
      {
        this.byName[world.Name] = world;
      }
    }
  }
}
