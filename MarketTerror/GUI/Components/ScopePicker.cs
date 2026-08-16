// <copyright file="ScopePicker.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using Dalamud.Bindings.ImGui;
  using MarketTerror.Helpers;
  using MarketTerror.Services;

  /// <summary>
  /// The combo that picks how far a search reaches around the picked world.
  /// </summary>
  public static class ScopePicker
  {
    /// <summary>
    /// Draws the picker. The caller has already set the item width.
    /// </summary>
    /// <param name="id">The ImGui id the combo is drawn under.</param>
    /// <param name="selection">The scope being picked.</param>
    /// <param name="onChanged">Called when another entry was picked, or null when nothing needs telling.</param>
    public static void Draw(string id, MarketScopeSelection selection, Action? onChanged = null)
    {
      ArgumentNullException.ThrowIfNull(selection);

      var options = selection.Options;
      var current = selection.SelectedIndex;
      var picked = -1;

      if (ImGui.BeginCombo(id, selection.SelectedDisplayName))
      {
        for (var i = 0; i < options.Count; i++)
        {
          var isSelected = current == i;

          if (ImGui.Selectable(options[i].Display, isSelected))
          {
            picked = i;
          }

          if (isSelected)
          {
            ImGui.SetItemDefaultFocus();
          }
        }

        ImGui.EndCombo();
      }

      var target = selection.QueryTargetLabel;

      Utilities.HoverTooltip(target.Length > 0
        ? $"How far the searches reach. Prices come from {target}."
        : "How far the searches reach around the picked world.");

      if (picked < 0 || picked == current)
      {
        return;
      }

      selection.Select(picked);
      onChanged?.Invoke();
    }
  }
}
