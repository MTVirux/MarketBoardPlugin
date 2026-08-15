// <copyright file="MarketBoardInteraction.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Linq;
  using System.Numerics;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.Game.Control;

  using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

  /// <summary>
  /// Interacts with an in-world Market Board the player is already standing next to.
  /// </summary>
  internal static class MarketBoardInteraction
  {
    /// <summary>
    /// Kept below the game's own interaction range so an interaction we start always goes through.
    /// </summary>
    private const float InteractRangeYalms = 4.5f;

    private static readonly HashSet<uint> MarketBoardBaseIds = new()
    {
      2000073, 2000402, 2000440, 2000442, 2010285,
    };

    /// <summary>
    /// Opens the closest Market Board within interaction range.
    /// </summary>
    /// <param name="objectTable">The object table.</param>
    /// <param name="log">The plugin log.</param>
    /// <returns>True if an interaction was started.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed interaction must fall back to travel, never propagate")]
    public static unsafe bool TryInteractWithNearbyBoard(IObjectTable objectTable, IPluginLog log)
    {
      ArgumentNullException.ThrowIfNull(objectTable);
      ArgumentNullException.ThrowIfNull(log);

      try
      {
        var player = objectTable.LocalPlayer;
        if (player == null)
        {
          return false;
        }

        var board = objectTable.EventObjects
          .Where(o => MarketBoardBaseIds.Contains(o.BaseId) && o.IsTargetable)
          .Select(o => (Object: o, Distance: Vector3.Distance(player.Position, o.Position) - o.HitboxRadius))
          .Where(o => o.Distance <= InteractRangeYalms)
          .OrderBy(o => o.Distance)
          .Select(o => o.Object)
          .FirstOrDefault();

        if (board == null)
        {
          return false;
        }

        TargetSystem.Instance()->InteractWithObject((CSGameObject*)board.Address, false);
        log.Debug("Interacted with the Market Board next to us instead of travelling");
        return true;
      }
      catch (Exception ex)
      {
        log.Error(ex, "Failed to interact with a nearby Market Board");
        return false;
      }
    }
  }
}
