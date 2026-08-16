// <copyright file="SectionHeading.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using Dalamud.Bindings.ImGui;

  /// <summary>
  /// The heading of a market data section, which hides and shows its table when clicked.
  /// </summary>
  public static class SectionHeading
  {
    /// <summary>
    /// Draws the heading of a section.
    /// </summary>
    /// <param name="context">The state and services shared by every component.</param>
    /// <param name="id">The ImGui id of the heading.</param>
    /// <param name="text">The heading text.</param>
    /// <param name="collapsed">True when the section's table is hidden.</param>
    /// <returns>True when the heading was clicked this frame.</returns>
    public static bool Draw(MarketBoardContext context, string id, string text, bool collapsed)
    {
      ArgumentNullException.ThrowIfNull(context);

      using var font = context.TitleFont.Push();

      if (collapsed)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, context.Theme.TextDim);
      }

      var clicked = ImGui.Selectable($"{text}##{id}", false);

      if (collapsed)
      {
        ImGui.PopStyleColor();
      }

      if (ImGui.IsItemHovered())
      {
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
      }

      return clicked;
    }
  }
}
