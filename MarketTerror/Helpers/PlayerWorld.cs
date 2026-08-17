// <copyright file="PlayerWorld.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using Dalamud.Plugin.Services;

  /// <summary>
  /// The world the character that is currently logged in sits on.
  /// </summary>
  internal static class PlayerWorld
  {
    /// <summary>
    /// Reads the name of the world the character is on.
    /// </summary>
    /// <param name="playerState">The player state.</param>
    /// <returns>The world name, or an empty string when it is not known.</returns>
    /// <remarks>
    /// Swapping characters leaves the player state loaded for a moment while its world no longer
    /// points at a row, and reading that row outright throws, so it is read as a nullable instead.
    /// </remarks>
    public static string CurrentName(IPlayerState playerState)
    {
      if (!playerState.IsLoaded)
      {
        return string.Empty;
      }

      return playerState.CurrentWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
    }
  }
}
