// <copyright file="WorldSelection.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using Lumina.Excel.Sheets;
  using Lumina.Extensions;
  using MarketTerror.Models;

  /// <summary>
  /// The world the market board window prices at, and how wide it reaches around it.
  /// </summary>
  public sealed class WorldSelection : MarketScopeSelection
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldSelection"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public WorldSelection(MarketTerrorPlugin plugin)
      : base(plugin)
    {
    }

    /// <inheritdoc/>
    protected override string StoredWorld
    {
      get => this.Plugin.Config.MarketBoardScopeWorld;
      set => this.Plugin.Config.MarketBoardScopeWorld = value;
    }

    /// <inheritdoc/>
    protected override MarketScope StoredScope
    {
      get => this.Plugin.Config.MarketBoardScope;
      set => this.Plugin.Config.MarketBoardScope = value;
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
