// <copyright file="UnlockState.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  /// <summary>
  /// What the game had to say about an item's unlock state.
  /// </summary>
  public enum UnlockState
  {
    /// <summary>
    /// The item unlocks nothing, so it has no state to read.
    /// </summary>
    NotUnlockable,

    /// <summary>
    /// The character has already unlocked the item.
    /// </summary>
    Unlocked,

    /// <summary>
    /// The character has not unlocked the item yet.
    /// </summary>
    Locked,

    /// <summary>
    /// The game could not be read this time, so the state is only unknown for now.
    /// </summary>
    Unreadable,
  }
}
