// <copyright file="ItemHeaderBar.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface.Textures;
  using Dalamud.Utility;

  /// <summary>
  /// The header of the right hand column: the selected item's icon and name, the world combo and the data timestamps.
  /// </summary>
  public sealed class ItemHeaderBar
  {
    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemHeaderBar"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public ItemHeaderBar(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the header bar. The caller has already checked that an item is selected.
    /// </summary>
    public void Draw()
    {
      var scale = ImGui.GetIO().FontGlobalScale;
      var item = this.context.SelectedItem!.Value;
      var itemName = item.Name.ExtractText();

      using var selectedItemIcon = this.context.Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup
      {
        IconId = item.Icon,
      }).GetWrapOrDefault();

      if (selectedItemIcon != null)
      {
        if (ImGui.ImageButton(selectedItemIcon.Handle, new Vector2(40, 40)))
        {
          this.context.CopyToClipboard(itemName);
        }
      }
      else
      {
        ImGui.SetCursorPos(new Vector2(40, 40));
      }

      this.context.TitleFont.Push();
      ImGui.SameLine();
      ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (ImGui.GetFontSize() / 2.0f) + (20 * scale));
      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextBright);
      ImGui.Text(itemName);
      ImGui.PopStyleColor();
      ImGui.SameLine(ImGui.GetContentRegionAvail().X - (250 * scale));
      ImGui.SetCursorPosY(8 * scale);
      this.context.TitleFont.Pop();

      ImGui.BeginGroup();
      ImGui.SetNextItemWidth(250 * scale);

      if (ImGui.BeginCombo("##worldCombo", this.context.Worlds.SelectedDisplayName))
      {
        var worlds = this.context.Worlds.Worlds;

        for (var i = 0; i < worlds.Count; i++)
        {
          var isSelected = this.context.Worlds.SelectedIndex == i;

          if (ImGui.Selectable(worlds[i].Display, isSelected))
          {
            this.context.Worlds.Select(i);
            this.context.ResetMarketData();
          }

          if (isSelected)
          {
            ImGui.SetItemDefaultFocus();
          }
        }

        ImGui.EndCombo();
      }

      var marketData = this.context.MarketData.MarketData;

      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);

      if (marketData != null)
      {
        ImGui.SetNextItemWidth(250 * scale);
        ImGui.Text(
          $"Last update: {DateTimeOffset.FromUnixTimeMilliseconds(marketData.LastUploadTime).LocalDateTime:G}" +
          $"\nLast Fetch : {DateTimeOffset.FromUnixTimeMilliseconds(marketData.FetchTimestamp).LocalDateTime:G}");

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetTextLineHeight() - ImGui.GetTextLineHeightWithSpacing());
      }
      else
      {
        ImGui.SetNextItemWidth(250 * scale);
        ImGui.Text("Fetching data from Universalis...");

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetTextLineHeight() - ImGui.GetTextLineHeightWithSpacing());
      }

      ImGui.PopStyleColor();

      ImGui.EndGroup();
    }
  }
}
