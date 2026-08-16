// <copyright file="ItemListPanel.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Linq;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// The scrollable item list: the search history, the favourites or the category tree.
  /// </summary>
  public sealed class ItemListPanel
  {
    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemListPanel"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public ItemListPanel(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the item list.
    /// </summary>
    public void Draw()
    {
      ImGui.BeginChild("itemTree", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), false, ImGuiWindowFlags.HorizontalScrollbar);
      var itemTextSize = ImGui.CalcTextSize(string.Empty);

      if (this.context.SearchHistoryOpen)
      {
        this.DrawHistory();
      }
      else if (this.context.FavoritesOpen)
      {
        this.DrawFavorites();
      }
      else
      {
        this.DrawCategoryTree(itemTextSize);
      }

      ImGui.EndChild();
    }

    private void DrawHeading(string label)
    {
      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);
      ImGui.Text(label);
      ImGui.PopStyleColor();
    }

    private void DrawHistory()
    {
      this.DrawHeading("History");
      ImGui.Separator();
      var sheet = this.context.Plugin.DataManager.Excel.GetSheet<Item>();
      foreach (var id in this.context.Config.History.ToArray())
      {
        var item = sheet.GetRowOrDefault(id);
        if (!item.HasValue)
        {
          continue;
        }

        var itemName = item.Value.Name.ExtractText();

        if (ImGui.Selectable($"{itemName}", this.context.SelectedItem?.RowId == id))
        {
          this.context.SelectItem(id, true);
        }

        if (ImGui.BeginPopupContextItem($"historyItemContextMenu{id}"))
        {
          if (this.context.SelectedItem?.RowId != item.Value.RowId)
          {
            this.context.SelectItem(item.Value.RowId);
          }

          if (ImGui.Selectable("Add to the shopping list"))
          {
            this.context.TryAddCheapestToShoppingList(item.Value, false);
          }

          if (ImGui.Selectable("Add to the favorites"))
          {
            this.context.Config.Favorites.Add(item.Value.RowId);
          }

          if (ImGui.Selectable("Remove from history"))
          {
            this.context.Config.History.Remove(item.Value.RowId);
          }

          ImGui.EndPopup();
        }

        ImGui.OpenPopupOnItemClick($"historyItemContextMenu{id}", ImGuiPopupFlags.MouseButtonRight);
      }
    }

    private void DrawFavorites()
    {
      this.DrawHeading("Favorites");
      ImGui.Separator();
      var sheet = this.context.Plugin.DataManager.Excel.GetSheet<Item>();
      foreach (var id in this.context.Config.Favorites.ToArray())
      {
        var item = sheet.GetRowOrDefault(id);
        if (!item.HasValue)
        {
          continue;
        }

        var itemName = item.Value.Name.ExtractText();

        if (ImGui.Selectable($"{itemName}", this.context.SelectedItem?.RowId == id))
        {
          this.context.SelectItem(id, true);
        }

        if (ImGui.BeginPopupContextItem($"itemContextMenu{itemName}"))
        {
          if (ImGui.Selectable("Remove from the favorites"))
          {
            this.context.Config.Favorites.Remove(item.Value.RowId);
          }

          ImGui.EndPopup();
        }

        ImGui.OpenPopupOnItemClick($"itemContextMenu{itemName}", ImGuiPopupFlags.MouseButtonRight);
      }
    }

    private void DrawCategoryTree(Vector2 itemTextSize)
    {
      foreach (var category in this.context.Catalog.FilteredCategories)
      {
        if (ImGui.TreeNode(category.Key.Name.ExtractText() + "##cat" + category.Key.RowId))
        {
          ImGui.Unindent(ImGui.GetTreeNodeToLabelSpacing());

          for (var i = 0; i < category.Value.Count; i++)
          {
            if (ImGui.GetCursorPosY() < ImGui.GetScrollY() - itemTextSize.Y)
            {
              // Don't draw items above the scroll region.
              var y = ImGui.GetCursorPosY();
              var sy = ImGui.GetScrollY() - itemTextSize.Y;
              var spacing = itemTextSize.Y + ImGui.GetStyle().ItemSpacing.Y;
              var c = category.Value.Count;
              while (i < c && y < sy)
              {
                y += spacing;
                i++;
              }

              ImGui.SetCursorPosY(y);
              continue;
            }

            if (ImGui.GetCursorPosY() > ImGui.GetScrollY() + ImGui.GetWindowHeight())
            {
              // Don't draw item names below the scroll region
              var remainingItems = category.Value.Count - i;
              var remainingItemsHeight = itemTextSize.Y * remainingItems;
              var remainingGapHeight = ImGui.GetStyle().ItemSpacing.Y * (remainingItems - 1);
              ImGui.Dummy(new Vector2(1, remainingItemsHeight + remainingGapHeight));
              break;
            }

            var item = category.Value[i];
            var nodeFlags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            if (item.RowId == this.context.SelectedItem?.RowId)
            {
              nodeFlags |= ImGuiTreeNodeFlags.Selected;
            }

            ImGui.TreeNodeEx(item.Name.ExtractText() + "##item" + item.RowId, nodeFlags);

            if (ImGui.IsItemClicked())
            {
              this.context.SelectItem(item.RowId);
            }

            if (ImGui.BeginPopupContextItem("itemContextMenu" + category.Key.Name.ExtractText() + i))
            {
              if (this.context.SelectedItem != null && this.context.SelectedItem.Value.RowId != item.RowId)
              {
                this.context.SelectItem(item.RowId);
              }

              if (ImGui.Selectable("Add to the shopping list"))
              {
                this.context.TryAddCheapestToShoppingList(item, true);
              }

              if (ImGui.Selectable("Add to the favorites"))
              {
                this.context.Config.Favorites.Add(item.RowId);
              }

              ImGui.EndPopup();
            }

            ImGui.OpenPopupOnItemClick("itemContextMenu" + category.Key.Name.ExtractText() + i, ImGuiPopupFlags.MouseButtonRight);
          }

          ImGui.Indent(ImGui.GetTreeNodeToLabelSpacing());
          ImGui.TreePop();
        }
      }
    }
  }
}
