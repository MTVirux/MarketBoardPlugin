// <copyright file="TableHeading.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;

  /// <summary>
  /// The title above a market data table.
  /// </summary>
  public static class TableHeading
  {
    /// <summary>
    /// Draws the title on a band that runs down into the table's own header row.
    /// </summary>
    /// <param name="context">The state and services shared by every component.</param>
    /// <param name="text">The title to draw.</param>
    public static void Draw(MarketBoardContext context, string text)
    {
      ArgumentNullException.ThrowIfNull(context);

      var start = ImGui.GetCursorScreenPos();
      var width = ImGui.GetContentRegionAvail().X;
      var spacing = ImGui.GetStyle().ItemSpacing.Y;

      context.TitleFont.Push();

      ImGui.GetWindowDrawList().AddRectFilled(
        start,
        start + new Vector2(width, ImGui.GetTextLineHeight() + spacing),
        context.Theme.HeaderBg);

      ImGui.Text(text);
      context.TitleFont.Pop();
    }
  }
}
