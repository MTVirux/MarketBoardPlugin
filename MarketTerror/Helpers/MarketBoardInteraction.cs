// <copyright file="MarketBoardInteraction.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Linq;
  using System.Numerics;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.Game.Control;
  using Lumina.Excel.Sheets;

  using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

  /// <summary>
  /// Interacts with an in-world Market Board the player is already standing next to.
  /// </summary>
  internal static class MarketBoardInteraction
  {
    /// <summary>
    /// Kept below the game's own interaction range so an interaction we start always goes through.
    /// </summary>
    private const float InteractRangeYalms = 5f;

    /// <summary>
    /// EObj rows of Market Boards, used both directly and to resolve the board's name in the client's language.
    /// </summary>
    private static readonly uint[] KnownBoardIds = [2000073, 2000402, 2000440, 2000442, 2010285];

    private static HashSet<string>? boardNames;

    /// <summary>
    /// Opens the closest Market Board within interaction range.
    /// </summary>
    /// <param name="objectTable">The object table.</param>
    /// <param name="dataManager">The data manager.</param>
    /// <param name="log">The plugin log.</param>
    /// <returns>True if an interaction was started.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed interaction must fall back to travel, never propagate")]
    public static unsafe bool TryInteractWithNearbyBoard(IObjectTable objectTable, IDataManager dataManager, IPluginLog log)
    {
      ArgumentNullException.ThrowIfNull(objectTable);
      ArgumentNullException.ThrowIfNull(dataManager);
      ArgumentNullException.ThrowIfNull(log);

      try
      {
        var player = objectTable.LocalPlayer;
        if (player == null)
        {
          return false;
        }

        var names = GetBoardNames(dataManager, log);
        var boards = objectTable
          .Where(o => KnownBoardIds.Contains(o.BaseId) || names.Contains(o.Name.TextValue))
          .Select(o => (Object: o, Distance: Vector3.Distance(player.Position, o.Position) - o.HitboxRadius))
          .OrderBy(o => o.Distance)
          .ToList();

        if (boards.Count == 0)
        {
          log.Debug("No Market Board found in the object table; falling back to travel");
          return false;
        }

        var closest = boards[0];
        if (closest.Distance > InteractRangeYalms)
        {
          log.Debug($"Closest Market Board is {closest.Distance:F1} yalms away (limit {InteractRangeYalms}); falling back to travel");
          return false;
        }

        TargetSystem.Instance()->InteractWithObject((CSGameObject*)closest.Object.Address, false);
        log.Debug($"Interacted with the Market Board {closest.Distance:F1} yalms away instead of travelling");
        return true;
      }
      catch (Exception ex)
      {
        log.Error(ex, "Failed to interact with a nearby Market Board");
        return false;
      }
    }

    private static HashSet<string> GetBoardNames(IDataManager dataManager, IPluginLog log)
    {
      if (boardNames != null)
      {
        return boardNames;
      }

      var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var sheet = dataManager.Excel.GetSheet<EObjName>();

      foreach (var id in KnownBoardIds)
      {
        var name = sheet?.GetRowOrDefault(id)?.Singular.ExtractText();
        if (!string.IsNullOrWhiteSpace(name))
        {
          names.Add(name);
        }
      }

      log.Debug($"Market Board object names: {string.Join(", ", names)}");
      boardNames = names;
      return names;
    }
  }
}
