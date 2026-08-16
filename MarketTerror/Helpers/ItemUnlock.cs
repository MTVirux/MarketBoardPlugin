// <copyright file="ItemUnlock.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Diagnostics.CodeAnalysis;
  using Dalamud.Plugin.Services;
  using FFXIVClientStructs.FFXIV.Client.Game.UI;
  using FFXIVClientStructs.FFXIV.Component.Exd;

  /// <summary>
  /// Reads whether the current character has unlocked a one-time item such as a minion, a mount,
  /// an orchestrion roll or a Triple Triad card.
  /// </summary>
  internal static class ItemUnlock
  {
    private const long Unlocked = 1;
    private const long Locked = 2;

    /// <summary>
    /// Checks the unlock state of an item for the character that is currently logged in.
    /// </summary>
    /// <param name="clientState">The client state.</param>
    /// <param name="itemId">The item to check.</param>
    /// <returns>
    /// True when the character has already unlocked the item, false when it has not, and null
    /// when nobody is logged in or the item is not the kind of item that unlocks anything.
    /// </returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A broken game signature must not take the tooltip down every frame")]
    public static unsafe bool? IsUnlocked(IClientState clientState, uint itemId)
    {
      ArgumentNullException.ThrowIfNull(clientState);

      if (!clientState.IsLoggedIn)
      {
        return null;
      }

      try
      {
        var uiState = UIState.Instance();
        var row = ExdModule.GetItemRowById(itemId);

        if (uiState == null || row == null)
        {
          return null;
        }

        return uiState->IsItemActionUnlocked(row) switch
        {
          Unlocked => true,
          Locked => false,
          _ => null,
        };
      }
      catch (Exception)
      {
        return null;
      }
    }
  }
}
