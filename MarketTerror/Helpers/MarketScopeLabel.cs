// <copyright file="MarketScopeLabel.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System.Collections.Generic;
  using Dalamud.Game.Text;
  using MarketTerror.Extensions;
  using MarketTerror.Models;

  /// <summary>
  /// Names the markets a scope prices at, the way both world pickers show them.
  /// </summary>
  internal static class MarketScopeLabel
  {
    /// <summary>
    /// Joins the names a scope reaches, marking anything wider than a single world with the
    /// game's cross-world glyph.
    /// </summary>
    /// <param name="scope">The scope being named.</param>
    /// <param name="targets">The world, data centre or region names it prices at.</param>
    /// <returns>The label for a picker entry.</returns>
    public static string For(MarketScope scope, IReadOnlyList<string> targets)
    {
      var names = string.Join(" + ", targets);

      return scope == MarketScope.World ? names : $"{names} {SeIconChar.CrossWorld.ToChar()}";
    }
  }
}
