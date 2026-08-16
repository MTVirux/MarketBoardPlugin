// <copyright file="MarketScopeSelection.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Dalamud.Game.Text;
  using MarketTerror.Extensions;
  using MarketTerror.Helpers;
  using MarketTerror.Models;

  /// <summary>
  /// A world to price around and how wide to reach from it, as the two pickers a window shows.
  /// </summary>
  /// <remarks>
  /// The world picker overrides what the scope picker is anchored to: the scope entries always name
  /// the picked world's data centre and region, and list that data centre's worlds underneath.
  /// </remarks>
  public abstract class MarketScopeSelection
  {
    private readonly List<ScopeOption> options = new List<ScopeOption>();

    private string currentWorld = string.Empty;

    private string builtAnchor = string.Empty;

    private string builtMarker = string.Empty;

    private bool built;

    private int selectedIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketScopeSelection"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    protected MarketScopeSelection(MarketTerrorPlugin plugin)
    {
      this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    /// <summary>
    /// Gets every world the picker can be pointed at.
    /// </summary>
    public IReadOnlyList<WorldEntry> Worlds => this.Plugin.WorldCatalogue.Worlds;

    /// <summary>
    /// Gets the entries of the scope picker, widest reach first.
    /// </summary>
    public IReadOnlyList<ScopeOption> Options
    {
      get
      {
        this.EnsureOptions();
        return this.options;
      }
    }

    /// <summary>
    /// Gets the index of the selected scope entry, or -1 when nothing is selected yet.
    /// </summary>
    public int SelectedIndex
    {
      get
      {
        this.EnsureOptions();
        return this.selectedIndex;
      }
    }

    /// <summary>
    /// Gets the selected scope entry, or null when nothing is selected yet.
    /// </summary>
    public ScopeOption? Selected
    {
      get
      {
        var index = this.SelectedIndex;
        return index >= 0 ? this.options[index] : null;
      }
    }

    /// <summary>
    /// Gets a value indicating whether a scope is selected.
    /// </summary>
    public bool HasSelection => this.SelectedIndex >= 0;

    /// <summary>
    /// Gets the name of the world the scopes are anchored to, or an empty string while none is known.
    /// </summary>
    public string SelectedWorld => this.SelectedEntry?.Name ?? string.Empty;

    /// <summary>
    /// Gets the label of the selected scope entry.
    /// </summary>
    public string SelectedDisplayName => this.Selected?.Display ?? string.Empty;

    /// <summary>
    /// Gets how wide the selected entry reaches.
    /// </summary>
    public MarketScope Scope => this.Selected?.Scope ?? MarketScope.World;

    /// <summary>
    /// Gets the world, data centre or region names that market data is queried for.
    /// </summary>
    public IReadOnlyList<string> QueryTargets => this.Selected?.Targets ?? Array.Empty<string>();

    /// <summary>
    /// Gets the name a single Universalis call is made against.
    /// </summary>
    public string QueryTarget => this.Selected?.Query ?? string.Empty;

    /// <summary>
    /// Gets the query targets as one label, for tooltips.
    /// </summary>
    public string QueryTargetLabel => string.Join(" and ", this.QueryTargets);

    /// <summary>
    /// Gets a value indicating whether the selection spans more than one world, so listings carry their own world name.
    /// </summary>
    public bool IsMultiWorld => this.Scope != MarketScope.World;

    /// <summary>
    /// Gets a value indicating whether the selection spans more than one data centre.
    /// </summary>
    public bool IsRegionWide => this.Scope is MarketScope.Region or MarketScope.RegionWithOceania;

    /// <summary>
    /// Gets a value indicating whether the Oceania data centre is priced alongside the selection.
    /// </summary>
    public bool IncludesOceania => this.Scope == MarketScope.RegionWithOceania;

    /// <summary>
    /// Gets the world the character is on, which the picker falls back to and is marked with.
    /// </summary>
    /// <remarks>
    /// The last world seen is kept while nobody is logged in, so the marker does not blink away
    /// across a world visit or a zone change.
    /// </remarks>
    public string DefaultWorld
    {
      get
      {
        if (this.Plugin.PlayerState.IsLoaded)
        {
          var world = this.Plugin.PlayerState.CurrentWorld.Value.Name.ExtractText();

          if (world.Length > 0)
          {
            this.currentWorld = world;
          }
        }

        if (this.currentWorld.Length > 0)
        {
          return this.currentWorld;
        }

#if DEBUG
        // Nothing is logged in, so give the picker something to draw.
        var worlds = this.Worlds;

        return worlds.Count > 0 ? worlds[0].Name : string.Empty;
#else
        return string.Empty;
#endif
      }
    }

    /// <summary>
    /// Gets or sets the stored name of the anchor world.
    /// </summary>
    protected abstract string StoredWorld { get; set; }

    /// <summary>
    /// Gets or sets the stored reach.
    /// </summary>
    protected abstract MarketScope StoredScope { get; set; }

    /// <summary>
    /// Gets the anchor world, falling back to <see cref="DefaultWorld"/> the first time it is known.
    /// </summary>
    protected WorldEntry? SelectedEntry =>
      this.Plugin.WorldCatalogue.Find(this.StoredWorld) ?? this.ApplyDefaultWorld();

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    protected MarketTerrorPlugin Plugin { get; }

    /// <summary>
    /// Anchors the scopes on another world.
    /// </summary>
    /// <param name="worldName">The world name.</param>
    public void SelectWorld(string worldName)
    {
      if (string.Equals(this.StoredWorld, worldName, StringComparison.Ordinal) ||
          this.Plugin.WorldCatalogue.Find(worldName) == null)
      {
        return;
      }

      this.StoredWorld = worldName;
      this.Save();
    }

    /// <summary>
    /// Anchors the scopes back on <see cref="DefaultWorld"/>, when it is known.
    /// </summary>
    public void SelectDefaultWorld()
    {
      this.ApplyDefaultWorld();
    }

    /// <summary>
    /// Selects a scope entry, moving the anchor along with it when the entry names a single world.
    /// </summary>
    /// <param name="index">The index into <see cref="Options"/>.</param>
    public void Select(int index)
    {
      this.EnsureOptions();

      if (index < 0 || index >= this.options.Count)
      {
        return;
      }

      var option = this.options[index];
      this.selectedIndex = index;

      var changed = this.StoredScope != option.Scope;
      this.StoredScope = option.Scope;

      if (option.Scope == MarketScope.World &&
          !string.Equals(this.StoredWorld, option.Query, StringComparison.Ordinal))
      {
        this.StoredWorld = option.Query;
        changed = true;
      }

      if (changed)
      {
        this.Save();
      }
    }

    /// <summary>
    /// Forces the scope entries to be rebuilt, after something they are named for has moved.
    /// </summary>
    protected void InvalidateOptions()
    {
      this.built = false;
    }

    private static IEnumerable<ScopeOption> BuildOptions(WorldCatalogue catalogue, WorldEntry anchor, string marked)
    {
      // An Oceania world already reaches Oceania at plain region scope, so there is nothing to add on.
      if (anchor.Region != WorldRegions.Oceania)
      {
        yield return new ScopeOption(MarketScope.RegionWithOceania, new[] { anchor.Region, WorldRegions.Oceania });
      }

      yield return new ScopeOption(MarketScope.Region, new[] { anchor.Region });
      yield return new ScopeOption(MarketScope.DataCentre, new[] { anchor.DataCentre });

      foreach (var world in catalogue.InDataCentre(anchor.DataCentre))
      {
        var display = string.Equals(world.Name, marked, StringComparison.Ordinal)
          ? $"{world.Name} {SeIconChar.Hyadelyn.ToChar()}"
          : null;

        yield return new ScopeOption(MarketScope.World, new[] { world.Name }, display);
      }
    }

    private void EnsureOptions()
    {
      var anchor = this.SelectedEntry;
      var anchorName = anchor?.Name ?? string.Empty;
      var marker = this.DefaultWorld;

      if (this.built &&
          string.Equals(this.builtAnchor, anchorName, StringComparison.Ordinal) &&
          string.Equals(this.builtMarker, marker, StringComparison.Ordinal))
      {
        return;
      }

      this.built = true;
      this.builtAnchor = anchorName;
      this.builtMarker = marker;
      this.options.Clear();

      if (anchor != null)
      {
        this.options.AddRange(BuildOptions(this.Plugin.WorldCatalogue, anchor, marker));
      }

      this.selectedIndex = this.RestoreSelection(anchor);
    }

    private int RestoreSelection(WorldEntry? anchor)
    {
      if (anchor == null)
      {
        return -1;
      }

      var stored = this.StoredScope;

      // An Oceania character is not offered the "+ Oceania" entry, so it settles for the plain region.
      if (stored == MarketScope.RegionWithOceania && !this.options.Any(o => o.Scope == stored))
      {
        stored = MarketScope.Region;
      }

      var index = this.IndexOf(stored);

      return index >= 0 ? index : this.IndexOf(MarketScope.World);
    }

    private int IndexOf(MarketScope scope)
    {
      if (scope != MarketScope.World)
      {
        return this.options.FindIndex(o => o.Scope == scope);
      }

      var anchor = this.StoredWorld;

      return this.options.FindIndex(o =>
        o.Scope == MarketScope.World && string.Equals(o.Query, anchor, StringComparison.Ordinal));
    }

    private WorldEntry? ApplyDefaultWorld()
    {
      var entry = this.Plugin.WorldCatalogue.Find(this.DefaultWorld);

      if (entry == null || string.Equals(this.StoredWorld, entry.Name, StringComparison.Ordinal))
      {
        return entry;
      }

      this.StoredWorld = entry.Name;
      this.Save();

      return entry;
    }

    private void Save()
    {
      this.InvalidateOptions();
      this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
    }
  }
}
