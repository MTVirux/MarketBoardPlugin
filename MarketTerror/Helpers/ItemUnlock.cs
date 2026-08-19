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
    private const long NotLoaded = 3;

    // Only unlocked items are remembered: the character can unlock something at any time, but never
    // loses it, so a locked item has to go back to the game every time it is asked about.
    private static readonly HashSet<uint> Known = new HashSet<uint>();

    /// <summary>
    /// Drops every remembered unlock state, so the next read goes back to the game.
    /// </summary>
    public static void Forget()
    {
      Known.Clear();
    }

    /// <summary>
    /// Reads the unlock state of an item for the character that is currently logged in.
    /// </summary>
    /// <param name="playerState">The player state.</param>
    /// <param name="item">The item to check.</param>
    /// <returns>The state the game reported, telling a missing state apart from a failed read.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A broken game signature must not take the tooltip down every frame")]
    public static unsafe UnlockState Read(IPlayerState playerState, Item item)
    {
      ArgumentNullException.ThrowIfNull(playerState);

      // Only items that trigger an action can unlock anything, so everything else answers without a game read.
      if (item.ItemAction.RowId == 0)
      {
        return UnlockState.NotUnlockable;
      }

      if (Known.Contains(item.RowId))
      {
        return UnlockState.Unlocked;
      }

      // The unlock tables are empty until the character has loaded, and every item reads as locked until then.
      if (!playerState.IsLoaded)
      {
        return UnlockState.Unreadable;
      }

      try
      {
        var uiState = UIState.Instance();

        // The game pages item rows in as they are asked for, so a row can be missing on the first sweep.
        var row = ExdModule.GetItemRowById(item.RowId);

        if (uiState == null || row == null)
        {
          return UnlockState.Unreadable;
        }

        var state = uiState->IsItemActionUnlocked(row) switch
        {
          Unlocked => UnlockState.Unlocked,
          Locked => UnlockState.Locked,

          // The game has not filled in what this item unlocks yet, so asking again later can answer.
          NotLoaded => UnlockState.Unreadable,
          _ => UnlockState.NotUnlockable,
        };

        if (state == UnlockState.Unlocked)
        {
          Known.Add(item.RowId);
        }

        return state;
      }
      catch (Exception)
      {
        return UnlockState.Unreadable;
      }
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
    public static bool? IsUnlocked(IPlayerState playerState, Item item)
    {
      return Read(playerState, item) switch
      {
        UnlockState.Unlocked => true,
        UnlockState.Locked => false,
        _ => null,
      };
    }
  }
}
