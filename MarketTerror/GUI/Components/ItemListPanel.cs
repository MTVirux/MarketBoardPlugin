// <copyright file="ItemListPanel.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// The scrollable item list: the whole catalogue, the search results, the favourites or the history.
  /// </summary>
  public sealed class ItemListPanel
  {
    private readonly MarketBoardContext context;

    private bool wasSearchTabHidden = true;

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
      this.DrawTabs();

      // The hover progress bar sits below the list, so only leave room for it when it is drawn.
      var reservedHeight = this.context.Config.WatchForHovered ? -ImGui.GetFrameHeightWithSpacing() : 0;

      // Each tab gets its own child so the collapsed categories and the scroll position
      // never carry over from one tab to another.
      ImGui.BeginChild(
        $"itemTree{this.context.ItemListTab}",
        new Vector2(0, reservedHeight),
        false,
        ImGuiWindowFlags.HorizontalScrollbar);
      var itemTextSize = ImGui.CalcTextSize(string.Empty);

      if (this.context.ItemListTab == ItemListTab.Favorites)
      {
        this.DrawFavorites();
      }
      else if (this.context.ItemListTab == ItemListTab.History)
      {
        this.DrawHistory();
      }
      else
      {
        var searching = this.context.ItemListTab == ItemListTab.Search;

        this.context.Catalog.ApplyFilter(
          this.context.BuildFilter(searching ? this.context.SearchString : string.Empty));

        this.DrawCategoryTree(itemTextSize, searching);
      }

      ImGui.EndChild();
    }

    private static bool DrawTab(FontAwesomeIcon icon, string id, string tooltip, ImGuiTabItemFlags flags)
    {
      ImGui.PushFont(UiBuilder.IconFont);
      var open = ImGui.BeginTabItem($"{(char)icon}##{id}", flags);
      ImGui.PopFont();

      var hovered = ImGui.IsItemHovered();

      if (open)
      {
        ImGui.EndTabItem();
      }

      if (hovered)
      {
        ImGui.SetTooltip(tooltip);
      }

      return open;
    }

    private void DrawTabs()
    {
      var searching = !string.IsNullOrEmpty(this.context.SearchString) || this.context.HasActiveFilters;
      var hasFavorites = this.context.Config.Favorites.Count > 0;
      var hasHistory = this.context.Config.History.Count > 0;

      if ((!searching && this.context.ItemListTab == ItemListTab.Search)
        || (!hasFavorites && this.context.ItemListTab == ItemListTab.Favorites)
        || (!hasHistory && this.context.ItemListTab == ItemListTab.History))
      {
        this.context.ItemListTab = ItemListTab.All;
      }

      if (ImGui.BeginTabBar("itemListTabs", ImGuiTabBarFlags.Reorderable))
      {
        if (DrawTab(FontAwesomeIcon.List, "allTab", "All items", ImGuiTabItemFlags.None))
        {
          this.context.ItemListTab = ItemListTab.All;
        }

        // Selecting the tab as it appears saves a click when the user starts typing or sets a filter.
        if (searching && DrawTab(
          FontAwesomeIcon.Search,
          "searchTab",
          "Search results",
          this.wasSearchTabHidden ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
        {
          this.context.ItemListTab = ItemListTab.Search;
        }

        if (hasFavorites && DrawTab(FontAwesomeIcon.Star, "favoritesTab", "Favorites", ImGuiTabItemFlags.None))
        {
          this.context.ItemListTab = ItemListTab.Favorites;
        }

        if (hasHistory && DrawTab(FontAwesomeIcon.History, "historyTab", "Recently viewed", ImGuiTabItemFlags.None))
        {
          this.context.ItemListTab = ItemListTab.History;
        }

        ImGui.EndTabBar();
      }

      this.wasSearchTabHidden = !searching;
    }

    private void DrawHistory()
    {
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

    private void DrawCategoryTree(Vector2 itemTextSize, bool searching)
    {
      foreach (var category in this.context.Catalog.FilteredCategories)
      {
        if (searching)
        {
          ImGui.SetNextItemOpen(true, ImGuiCond.Always);
        }

        var categoryName = category.Key.Name.ExtractText();

        // The popup has to be bound while the tree node is still the last item, open or not.
        var categoryOpen = ImGui.TreeNode(categoryName + "##cat" + category.Key.RowId);

        this.DrawCategoryContextMenu(categoryName, category.Key.RowId, category.Value);

        if (categoryOpen)
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

            if (ImGui.BeginPopupContextItem("itemContextMenu" + categoryName + i))
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

            ImGui.OpenPopupOnItemClick("itemContextMenu" + categoryName + i, ImGuiPopupFlags.MouseButtonRight);
          }

          ImGui.Indent(ImGui.GetTreeNodeToLabelSpacing());
          ImGui.TreePop();
        }
      }
    }

    private void DrawCategoryContextMenu(string categoryName, uint categoryId, List<Item> items)
    {
      var popupId = "catContextMenu" + categoryId;

      if (ImGui.BeginPopupContextItem(popupId))
      {
        // Only the open category pays for these, and a category can hold thousands of items.
        var favorites = this.context.Config.Favorites.ToHashSet();
        var buyList = this.context.Plugin.ShoppingList;
        var listed = buyList.Select(s => s.SourceItem.RowId).ToHashSet();

        var missingFavorites = items.Where(i => !favorites.Contains(i.RowId)).ToArray();
        var missingFromBuyList = items.Where(i => !listed.Contains(i.RowId)).ToArray();

        if (missingFavorites.Length > 0 && ImGui.Selectable("Add all to the favorites"))
        {
          foreach (var item in missingFavorites)
          {
            this.context.Config.Favorites.Add(item.RowId);
          }

          this.context.Plugin.PluginInterface.SavePluginConfig(this.context.Config);
        }

        if (missingFavorites.Length < items.Count && ImGui.Selectable("Remove all from the favorites"))
        {
          foreach (var item in items)
          {
            this.context.Config.Favorites.Remove(item.RowId);
          }

          this.context.Plugin.PluginInterface.SavePluginConfig(this.context.Config);
        }

        this.DrawCategoryBuyListEntries(categoryName, items, missingFromBuyList);

        ImGui.EndPopup();
      }

      ImGui.OpenPopupOnItemClick(popupId, ImGuiPopupFlags.MouseButtonRight);
    }

    private void DrawCategoryBuyListEntries(string categoryName, List<Item> items, Item[] missing)
    {
      var bulkAdd = this.context.Plugin.ShoppingListBulkAdd;
      var buyList = this.context.Plugin.ShoppingList;

      // The buy list window's own scope, so adding and refreshing price against the same place.
      var scope = this.context.Plugin.ShoppingListScope;

      if (missing.Length > 0)
      {
        // The prices come from Universalis a chunk at a time, so only one category can be added at once.
        var busy = bulkAdd.IsRunning || !scope.HasSelection;

        if (busy)
        {
          ImGui.BeginDisabled();
        }

        if (ImGui.Selectable("Add all to the shopping list"))
        {
          bulkAdd.Start(categoryName, missing, scope.QueryTarget);
        }

        if (busy)
        {
          ImGui.EndDisabled();
        }
      }

      if (missing.Length < items.Count && ImGui.Selectable("Remove all from the shopping list"))
      {
        var ids = items.Select(i => i.RowId).ToHashSet();

        buyList.RemoveAll(s => ids.Contains(s.SourceItem.RowId));
      }
    }
  }
}
