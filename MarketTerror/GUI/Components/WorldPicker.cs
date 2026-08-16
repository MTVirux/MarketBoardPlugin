// <copyright file="WorldPicker.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using Dalamud.Bindings.ImGui;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Helpers;
  using MarketTerror.Services;

  /// <summary>
  /// The searchable combo that picks the world a scope is anchored to.
  /// </summary>
  public sealed class WorldPicker
  {
    private readonly string comboId;

    private readonly string filterId;

    private string filter = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldPicker"/> class.
    /// </summary>
    /// <param name="id">The ImGui id the combo is drawn under.</param>
    public WorldPicker(string id)
    {
      this.comboId = $"##{id}";
      this.filterId = $"##{id}Filter";
    }

    /// <summary>
    /// Draws the picker. The caller has already set the item width.
    /// </summary>
    /// <param name="selection">The scope the picker anchors.</param>
    /// <param name="theme">The theme the group headings are dimmed with.</param>
    /// <param name="onChanged">Called when the pick moved the anchor, or null when nothing needs telling.</param>
    public void Draw(MarketScopeSelection selection, TerrorTheme theme, Action? onChanged = null)
    {
      ArgumentNullException.ThrowIfNull(selection);
      ArgumentNullException.ThrowIfNull(theme);

      var previous = selection.SelectedWorld;

      var open = ImGui.BeginCombo(this.comboId, previous.Length > 0 ? previous : "Pick a world");
      var resetToDefault = ImGui.IsItemClicked(ImGuiMouseButton.Right);

      if (open)
      {
        this.DrawEntries(selection, theme, previous);
        ImGui.EndCombo();
      }

      if (resetToDefault)
      {
        selection.SelectDefaultWorld();
      }

      var current = selection.DefaultWorld;

      Utilities.HoverTooltip(current.Length > 0 && current != previous
        ? $"Your world\nRight-click to go back to {current}."
        : "Your world");

      if (onChanged != null && selection.SelectedWorld != previous)
      {
        onChanged();
      }
    }

    private void DrawEntries(MarketScopeSelection selection, TerrorTheme theme, string selected)
    {
      if (ImGui.IsWindowAppearing())
      {
        this.filter = string.Empty;
        ImGui.SetKeyboardFocusHere();
      }

      ImGui.SetNextItemWidth(-1);
      ImGui.InputTextWithHint(this.filterId, "Search worlds", ref this.filter, 64);
      ImGui.Separator();

      var needle = this.filter.Trim();
      var lastGroup = string.Empty;
      var matches = 0;

      foreach (var world in selection.Worlds)
      {
        var group = $"{world.Region} - {world.DataCentre}";

        if (needle.Length > 0 &&
            world.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 &&
            group.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
        {
          continue;
        }

        matches++;

        if (group != lastGroup)
        {
          lastGroup = group;
          ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
          ImGui.Text(group);
          ImGui.PopStyleColor();
        }

        var isSelected = world.Name == selected;

        if (ImGui.Selectable(world.Name, isSelected))
        {
          selection.SelectWorld(world.Name);
        }

        if (isSelected)
        {
          ImGui.SetItemDefaultFocus();
        }
      }

      if (matches == 0)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
        ImGui.Text("No worlds match.");
        ImGui.PopStyleColor();
      }
    }
  }
}
