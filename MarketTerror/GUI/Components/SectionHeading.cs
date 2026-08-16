// <copyright file="SectionHeading.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;

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
      var color = ImGui.GetColorU32(ImGuiCol.Text);

      if (collapsed)
      {
        ImGui.PopStyleColor();
      }

      if (ImGui.IsItemHovered())
      {
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
      }

      DrawArrow(collapsed, color);

      return clicked;
    }

    /// <summary>
    /// Draws the arrow reporting the section's state, centred on the right edge of the heading.
    /// </summary>
    /// <param name="collapsed">True when the section's table is hidden.</param>
    /// <param name="color">The colour of the heading text.</param>
    private static void DrawArrow(bool collapsed, uint color)
    {
      var min = ImGui.GetItemRectMin();
      var size = ImGui.GetItemRectSize();
      var arrow = $"{(char)(collapsed ? FontAwesomeIcon.ChevronRight : FontAwesomeIcon.ChevronDown)}";

      ImGui.PushFont(UiBuilder.IconFont);

      var arrowSize = ImGui.CalcTextSize(arrow);

      ImGui.GetWindowDrawList().AddText(
        new Vector2(
          min.X + size.X - arrowSize.X - ImGui.GetStyle().FramePadding.X,
          min.Y + ((size.Y - arrowSize.Y) / 2.0f)),
        color,
        arrow);

      ImGui.PopFont();
    }
  }
}
