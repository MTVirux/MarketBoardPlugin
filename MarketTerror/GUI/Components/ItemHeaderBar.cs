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
  using FFXIVClientStructs.FFXIV.Client.UI.Misc;
  using MarketTerror.Helpers;

  /// <summary>
  /// The header of the right hand column: the selected item's icon and name, the world combo and the data timestamps.
  /// </summary>
  public sealed class ItemHeaderBar
  {
    private const string ContextMenuId = "itemHeaderContextMenu";

    private readonly MarketBoardContext context;
    private readonly ItemTooltip tooltip;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemHeaderBar"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public ItemHeaderBar(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
      this.tooltip = new ItemTooltip(this.context);
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

        if (ImGui.IsItemHovered())
        {
          this.tooltip.Draw(item);
        }

        ImGui.OpenPopupOnItemClick(ContextMenuId, ImGuiPopupFlags.MouseButtonRight);
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
      ImGui.OpenPopupOnItemClick(ContextMenuId, ImGuiPopupFlags.MouseButtonRight);
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

      this.DrawContextMenu(item.RowId);
    }

    private static unsafe void SearchInGame(uint itemId)
    {
      var itemFinder = ItemFinderModule.Instance();

      if (itemFinder != null)
      {
        itemFinder->SearchForItem(itemId, true);
      }
    }

    private void DrawContextMenu(uint itemId)
    {
      if (!ImGui.BeginPopup(ContextMenuId))
      {
        return;
      }

      if (ImGui.Selectable("Open in Universalis"))
      {
        Utilities.OpenBrowser($"https://universalis.app/market/{itemId}");
      }

      if (ImGui.Selectable("Open in mtvirux.app"))
      {
        Utilities.OpenBrowser($"https://mtvirux.app/item/{itemId}");
      }

      if (ImGui.Selectable("Search in-game"))
      {
        SearchInGame(itemId);
      }

      if (this.context.Config.Favorites.Contains(itemId))
      {
        if (ImGui.Selectable("Remove from the favorites"))
        {
          this.context.Config.Favorites.Remove(itemId);
        }
      }
      else if (ImGui.Selectable("Add to the favorites"))
      {
        this.context.Config.Favorites.Add(itemId);
      }

      ImGui.EndPopup();
    }
  }
}
