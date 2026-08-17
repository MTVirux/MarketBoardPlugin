// <copyright file="WorldSelection.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using Lumina.Excel.Sheets;
  using Lumina.Extensions;
  using MarketTerror.Models;

  /// <summary>
  /// The world a board prices at, and how wide it reaches around it.
  /// </summary>
  /// <remarks>
  /// Where the choice is stored is passed in, so the main window can keep using its config fields
  /// while each detached window writes to its own entry.
  /// </remarks>
  public sealed class WorldSelection : MarketScopeSelection
  {
    private readonly Func<string> getWorld;

    private readonly Action<string> setWorld;

    private readonly Func<MarketScope> getScope;

    private readonly Action<MarketScope> setScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldSelection"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="getWorld">Reads the stored anchor world.</param>
    /// <param name="setWorld">Writes the stored anchor world.</param>
    /// <param name="getScope">Reads the stored reach.</param>
    /// <param name="setScope">Writes the stored reach.</param>
    public WorldSelection(
      MarketTerrorPlugin plugin,
      Func<string> getWorld,
      Action<string> setWorld,
      Func<MarketScope> getScope,
      Action<MarketScope> setScope)
      : base(plugin)
    {
      this.getWorld = getWorld ?? throw new ArgumentNullException(nameof(getWorld));
      this.setWorld = setWorld ?? throw new ArgumentNullException(nameof(setWorld));
      this.getScope = getScope ?? throw new ArgumentNullException(nameof(getScope));
      this.setScope = setScope ?? throw new ArgumentNullException(nameof(setScope));
    }

    /// <inheritdoc/>
    protected override string StoredWorld
    {
      get => this.getWorld();
      set => this.setWorld(value);
    }

    /// <inheritdoc/>
    protected override MarketScope StoredScope
    {
      get => this.getScope();
      set => this.setScope(value);
    }

    /// <summary>
    /// Builds a selection backed by the main window's config fields.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <returns>The main window's world selection.</returns>
    public static WorldSelection ForMainWindow(MarketTerrorPlugin plugin)
    {
      ArgumentNullException.ThrowIfNull(plugin);

      return new WorldSelection(
        plugin,
        () => plugin.Config.MarketBoardScopeWorld,
        value => plugin.Config.MarketBoardScopeWorld = value,
        () => plugin.Config.MarketBoardScope,
        value => plugin.Config.MarketBoardScope = value);
    }

    /// <summary>
    /// Builds a selection backed by a detached window's stored entry.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="state">The detached window's stored entry.</param>
    /// <returns>The detached window's world selection.</returns>
    public static WorldSelection ForDetachedBoard(MarketTerrorPlugin plugin, DetachedBoardState state)
    {
      ArgumentNullException.ThrowIfNull(plugin);
      ArgumentNullException.ThrowIfNull(state);

      return new WorldSelection(
        plugin,
        () => state.ScopeWorld,
        value => state.ScopeWorld = value,
        () => state.Scope,
        value => state.Scope = value);
    }

    /// <summary>
    /// Resolves the data centre name a world belongs to.
    /// </summary>
    /// <param name="worldId">The world row id.</param>
    /// <returns>The data centre name, or an empty string when the world is unknown.</returns>
    public string GetDataCenterName(int worldId)
    {
      var world = this.Plugin.DataManager.GetExcelSheet<World>().FirstOrNull(w => w.RowId == worldId);

      if (world != null)
      {
        return world.Value.DataCenter.Value.Name.ExtractText();
      }

      return string.Empty;
    }
  }
}
