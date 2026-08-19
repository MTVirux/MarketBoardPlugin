// <copyright file="ItemUnlockDebug.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

#if DEBUG

namespace MarketTerror.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Globalization;
  using System.Linq;
  using Dalamud.Plugin.Services;
  using Dalamud.Utility;
  using FFXIVClientStructs.FFXIV.Client.Game.UI;
  using FFXIVClientStructs.FFXIV.Component.Exd;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// Dumps everything the game will say about an item's unlock state, so a wrong answer can be
  /// traced to the reading the game disagrees on.
  /// </summary>
  internal static class ItemUnlockDebug
  {
    private const string Command = "unlockdebug ";

    /// <summary>
    /// Runs the dump when the command arguments ask for it.
    /// </summary>
    /// <param name="dataManager">The data manager the item sheet is read from.</param>
    /// <param name="playerState">The player state.</param>
    /// <param name="log">The plugin log.</param>
    /// <param name="arguments">The arguments the open command was given.</param>
    /// <returns>True when the arguments were the dump command and nothing else should run.</returns>
    public static bool TryHandle(IDataManager dataManager, IPlayerState playerState, IPluginLog log, string arguments)
    {
      if (arguments == null || !arguments.StartsWith(Command, StringComparison.OrdinalIgnoreCase))
      {
        return false;
      }

      Dump(dataManager, playerState, log, arguments[Command.Length..].Trim());

      return true;
    }

    /// <summary>
    /// Logs the raw unlock readings for every item whose name contains the fragment.
    /// </summary>
    /// <param name="dataManager">The data manager the item sheet is read from.</param>
    /// <param name="playerState">The player state.</param>
    /// <param name="log">The plugin log.</param>
    /// <param name="fragment">The item name fragment to look for.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A diagnostic must never take the plugin down")]
    public static void Dump(IDataManager dataManager, IPlayerState playerState, IPluginLog log, string fragment)
    {
      ArgumentNullException.ThrowIfNull(dataManager);
      ArgumentNullException.ThrowIfNull(playerState);
      ArgumentNullException.ThrowIfNull(log);

      var matches = dataManager.GetExcelSheet<Item>()
        .Where(i => i.Name.ExtractText().Contains(fragment, StringComparison.OrdinalIgnoreCase))
        .Take(20)
        .ToList();

      log.Information($"[unlockdebug] \"{fragment}\": {matches.Count} matches, PlayerState.IsLoaded={playerState.IsLoaded}");

      foreach (var item in matches)
      {
        try
        {
          log.Information(Describe(item));
        }
        catch (Exception ex)
        {
          log.Information($"[unlockdebug] {item.RowId} threw {ex.GetType().Name}: {ex.Message}");
        }
      }
    }

    private static unsafe string Describe(Item item)
    {
      var name = item.Name.ExtractText();
      var action = item.ItemAction.ValueNullable;
      var data = action == null
        ? new List<ushort>()
        : action.Value.Data.Take(4).ToList();

      var line = $"[unlockdebug] {item.RowId} \"{name}\" actionRow={item.ItemAction.RowId} type={action?.Action}"
        + $" data=[{string.Join(",", data.Select(d => d.ToString(CultureInfo.InvariantCulture)))}]";

      var uiState = UIState.Instance();
      var playerState = PlayerState.Instance();
      var row = ExdModule.GetItemRowById(item.RowId);

      line += $" row={(row == null ? "null" : "ok")}";

      if (uiState != null && row != null)
      {
        line += $" raw={uiState->IsItemActionUnlocked(row)}";
      }

      if (data.Count == 0 || uiState == null || playerState == null)
      {
        return line;
      }

      uint id = data[0];

      // Every per-kind reading for the same id, so the one the aggregate call disagrees with stands out.
      line += $" mount={playerState->IsMountUnlocked(id)}"
        + $" minion={uiState->IsCompanionUnlocked(id)}"
        + $" roll={playerState->IsOrchestrionRollUnlocked(id)}"
        + $" ornament={playerState->IsOrnamentUnlocked(id)}"
        + $" card={uiState->IsTripleTriadCardUnlocked((ushort)id)}"
        + $" link={uiState->IsUnlockLinkUnlocked(id)}";

      return line;
    }
  }
}

#endif
