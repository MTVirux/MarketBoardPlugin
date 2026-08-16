// <copyright file="ItemUnlock.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.Game.UI;
  using FFXIVClientStructs.FFXIV.Component.Exd;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// Reads whether the current character has unlocked a one-time item such as a minion, a mount,
  /// an orchestrion roll or a Triple Triad card.
  /// </summary>
  internal static class ItemUnlock
  {
    private const long Unlocked = 1;
    private const long Locked = 2;

    private static readonly Dictionary<uint, bool> Known = new Dictionary<uint, bool>();

    /// <summary>
    /// Drops every remembered unlock state, so the next read goes back to the game.
    /// </summary>
    public static void Forget()
    {
      Known.Clear();
    }

    /// <summary>
    /// Checks the unlock state of an item for the character that is currently logged in.
    /// </summary>
    /// <param name="playerState">The player state.</param>
    /// <param name="item">The item to check.</param>
    /// <returns>
    /// True when the character has already unlocked the item, false when it has not, and null
    /// when the item is not the kind of item that unlocks anything or the game could not be read.
    /// </returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A broken game signature must not take the tooltip down every frame")]
    public static unsafe bool? IsUnlocked(IPlayerState playerState, Item item)
    {
      ArgumentNullException.ThrowIfNull(playerState);

      // Only items that trigger an action can unlock anything, so everything else answers without a game read.
      if (item.ItemAction.RowId == 0)
      {
        return null;
      }

      if (Known.TryGetValue(item.RowId, out var known))
      {
        return known;
      }

      // The unlock tables are empty until the character has loaded, and every item reads as locked until then.
      if (!playerState.IsLoaded)
      {
        return null;
      }

      try
      {
        var uiState = UIState.Instance();
        var row = ExdModule.GetItemRowById(item.RowId);

        if (uiState == null || row == null)
        {
          return null;
        }

        var state = uiState->IsItemActionUnlocked(row) switch
        {
          Unlocked => (bool?)true,
          Locked => false,
          _ => null,
        };

        // A failed read must not be remembered, or one bad frame hides the item for the whole session.
        if (state != null)
        {
          Known[item.RowId] = state.Value;
        }

        return state;
      }
      catch (Exception)
      {
        return null;
      }
    }
  }
}
