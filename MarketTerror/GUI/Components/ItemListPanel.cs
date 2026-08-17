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
  using MarketTerror.Services;

  /// <summary>
  /// The scrollable item list: the whole catalogue, the search results, the favourites or the history.
  /// </summary>
  public sealed class ItemListPanel
  {
    /// <summary>How far a tab has to be dragged off the bar before it comes away, in bar heights.</summary>
    private const float TearOffThreshold = 1.5f;

    private static readonly (ItemListTab Tab, FontAwesomeIcon Icon, string Id, string Tooltip)[] TabDescriptors =
    {
      (ItemListTab.All, FontAwesomeIcon.List, "allTab", "All items"),
      (ItemListTab.Search, FontAwesomeIcon.Search, "searchTab", "Search results"),
      (ItemListTab.Favorites, FontAwesomeIcon.Star, "favoritesTab", "Favorites"),
      (ItemListTab.History, FontAwesomeIcon.History, "historyTab", "Recently viewed"),
    };

    private readonly MarketBoardContext context;

    private readonly MarketBoard board;

    private bool wasSearchTabHidden = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemListPanel"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    /// <param name="board">The board this list belongs to.</param>
    public ItemListPanel(MarketBoardContext context, MarketBoard board)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
      this.board = board ?? throw new ArgumentNullException(nameof(board));
    }

    /// <summary>
    /// Gets a value indicating whether the search box and the advanced filters narrow the catalogue
    /// this board is showing right now.
    /// </summary>
    /// <remarks>
    /// The main window routes typing to its search tab while it still has one, so its all items list
    /// stays the whole catalogue there. Every other board narrows the list it is showing in place.
    /// </remarks>
    private bool NarrowsCurrentList
    {
      get
      {
        if (string.IsNullOrEmpty(this.context.SearchString) && !this.context.HasActiveFilters)
        {
          return false;
        }

        return this.context.ItemListTab == ItemListTab.Search
          || !this.board.IsMainBoard
          || !this.board.Tabs.Contains(ItemListTab.Search);
      }
    }

    /// <summary>
    /// Draws the item list.
    /// </summary>
    public void Draw()
    {
      if (!this.DrawTabs())
      {
        return;
      }

      // The hover progress bar sits below the list, so only leave room for it when it is drawn.
      var reservedHeight = this.board.DrawUnderList != null && this.context.Config.WatchForHovered
        ? -ImGui.GetFrameHeightWithSpacing()
        : 0;

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
        var narrowing = this.NarrowsCurrentList;

        var rebuilt = this.context.CatalogView.ApplyFilter(
          narrowing ? this.context.BuildFilter(this.context.SearchString) : ItemFilter.None);

        this.DrawCategoryTree(itemTextSize, narrowing, rebuilt);
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

    /// <summary>
    /// Draws the tab bar, or the placeholder standing in for it when every list is in its own window.
    /// </summary>
    /// <returns>True when a list belongs under it, false when this board has none left to show.</returns>
    private bool DrawTabs()
    {
      if (!this.board.IsMainBoard)
      {
        // A detached board is locked to the one list it was torn off with; the search box narrows it in place.
        if (this.board.Tabs.Count > 0)
        {
          this.context.ItemListTab = this.board.Tabs[0];
        }

        return true;
      }

      var searching = !string.IsNullOrEmpty(this.context.SearchString) || this.context.HasActiveFilters;

      if (this.board.Tabs.Count == 0)
      {
        this.DrawEmptyBar();
        this.wasSearchTabHidden = !searching;
        return false;
      }

      if (!this.board.Tabs.Contains(this.context.ItemListTab)
        || !this.IsAvailable(this.context.ItemListTab, searching))
      {
        this.context.ItemListTab = this.FallbackTab(searching);
      }

      // A tab bar is not an ImGui item, so its rect has to be measured rather than queried afterwards.
      var barOrigin = ImGui.GetCursorScreenPos();
      var barWidth = ImGui.GetContentRegionAvail().X;

      if (ImGui.BeginTabBar("itemListTabs", ImGuiTabBarFlags.Reorderable))
      {
        foreach (var descriptor in TabDescriptors)
        {
          if (!this.board.Tabs.Contains(descriptor.Tab) || !this.IsAvailable(descriptor.Tab, searching))
          {
            continue;
          }

          // Selecting the search tab as it appears saves a click when the user starts typing.
          var flags = descriptor.Tab == ItemListTab.Search && this.wasSearchTabHidden
            ? ImGuiTabItemFlags.SetSelected
            : ImGuiTabItemFlags.None;

          if (DrawTab(descriptor.Icon, descriptor.Id, descriptor.Tooltip, flags))
          {
            this.context.ItemListTab = descriptor.Tab;
          }

          this.CheckTearOff(descriptor.Tab);
        }

        ImGui.EndTabBar();
      }

      this.board.TabBarScreenRect = (
        barOrigin,
        new Vector2(barOrigin.X + barWidth, barOrigin.Y + ImGui.GetFrameHeight()));
      this.wasSearchTabHidden = !searching;

      return true;
    }

    private ItemListTab FallbackTab(bool searching)
    {
      foreach (var tab in this.board.Tabs)
      {
        if (this.IsAvailable(tab, searching))
        {
          return tab;
        }
      }

      return this.board.Tabs.Count > 0 ? this.board.Tabs[0] : this.context.ItemListTab;
    }

    private bool IsAvailable(ItemListTab tab, bool searching)
    {
      return tab switch
      {
        ItemListTab.Search => searching,
        ItemListTab.Favorites => this.context.Config.Favorites.Count > 0,
        ItemListTab.History => this.context.Config.History.Count > 0,
        _ => true,
      };
    }

    private bool MatchesSearch(string itemName)
    {
      var search = this.context.SearchString;

      return string.IsNullOrEmpty(search)
        || itemName.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void DrawEmptyBar()
    {
      var height = ImGui.GetFrameHeight();
      var min = ImGui.GetCursorScreenPos();
      var max = new Vector2(min.X + ImGui.GetContentRegionAvail().X, min.Y + height);

      ImGui.Dummy(new Vector2(0, height));
      ImGui.GetWindowDrawList().AddRect(min, max, this.context.Theme.Border);

      var label = "Every list is in its own window";
      var labelSize = ImGui.CalcTextSize(label);
      ImGui.GetWindowDrawList().AddText(
        new Vector2(min.X + ((max.X - min.X - labelSize.X) / 2.0f), min.Y + ((height - labelSize.Y) / 2.0f)),
        this.context.Theme.Border,
        label);

      this.board.TabBarScreenRect = (min, max);
    }

    private void CheckTearOff(ItemListTab tab)
    {
      if (!ImGui.IsItemActive() || this.board.TearOffRequest != null)
      {
        return;
      }

      // Vertical only, so this never fights the bar's own horizontal reordering drag.
      var drag = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left);

      if (Math.Abs(drag.Y) > ImGui.GetFrameHeight() * TearOffThreshold)
      {
        this.board.TearOffRequest = tab;
        ImGui.ResetMouseDragDelta(ImGuiMouseButton.Left);
      }
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

        if (!this.MatchesSearch(itemName))
        {
          continue;
        }

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

        if (!this.MatchesSearch(itemName))
        {
          continue;
        }

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

    private void DrawCategoryTree(Vector2 itemTextSize, bool searching, bool resultsChanged)
    {
      foreach (var category in this.context.CatalogView.FilteredCategories)
      {
        if (searching)
        {
          // A fresh set of results opens up, but a category the user closed stays closed until then.
          ImGui.SetNextItemOpen(true, resultsChanged ? ImGuiCond.Always : ImGuiCond.Once);
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

              // The loop's own step would otherwise skip the first item back inside the region.
              i--;
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

        // A row added straight from a listing was never priced, so it does not stand in for one.
        var priced = buyList.Where(s => !s.IsDirect).Select(s => s.SourceItem.RowId).ToHashSet();

        var missingFavorites = items.Where(i => !favorites.Contains(i.RowId)).ToArray();
        var missingFromBuyList = items.Where(i => !priced.Contains(i.RowId)).ToArray();

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

        this.DrawCategoryBuyListEntries(categoryName, items, missingFromBuyList, items.Any(i => listed.Contains(i.RowId)));

        ImGui.EndPopup();
      }

      ImGui.OpenPopupOnItemClick(popupId, ImGuiPopupFlags.MouseButtonRight);
    }

    private void DrawCategoryBuyListEntries(string categoryName, List<Item> items, Item[] missing, bool anyListed)
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
          bulkAdd.Start(categoryName, missing, scope.QueryTargets);
        }

        if (busy)
        {
          ImGui.EndDisabled();
        }
      }

      if (anyListed && ImGui.Selectable("Remove all from the shopping list"))
      {
        var ids = items.Select(i => i.RowId).ToHashSet();

        buyList.RemoveAll(s => ids.Contains(s.SourceItem.RowId));
      }
    }
  }
}
