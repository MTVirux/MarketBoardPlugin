// <copyright file="ItemListsTree.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using Dalamud.Bindings.ImGui;
  using Lumina.Excel.Sheets;
  using MarketTerror.Models.ItemLists;

  /// <summary>
  /// The item lists the user has made, drawn as one collapsible node each.
  /// </summary>
  public sealed class ItemListsTree
  {
    /// <summary>
    /// What the box confirming a delete is registered under.
    /// </summary>
    private const string DeletePopupId = "Delete list##deleteItemList";

    private readonly MarketBoardContext context;

    // The list being renamed in place, or empty when none is.
    private Guid renaming = Guid.Empty;

    private string renameText = string.Empty;

    // The list waiting on the confirmation box, or empty when none is.
    private Guid deleting = Guid.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemListsTree"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public ItemListsTree(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws every list.
    /// </summary>
    public void Draw()
    {
      var store = this.context.Plugin.ItemLists;

      if (store.Count == 0)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);
        ImGui.TextWrapped("No lists yet. Right click an item and pick Lists to make one.");
        ImGui.PopStyleColor();

        return;
      }

      for (var i = 0; i < store.Count; i++)
      {
        this.DrawList(store[i], i);
      }

      this.DrawDeleteConfirm();
    }

    private bool MatchesSearch(string itemName)
    {
      var search = this.context.SearchString;

      return string.IsNullOrEmpty(search)
        || itemName.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void DrawList(ItemList list, int index)
    {
      if (this.renaming == list.Id)
      {
        this.DrawRenameBox(list);
        return;
      }

      var open = ImGui.TreeNode($"{list.Name} ({list.ItemIds.Count})##list{list.Id}");

      // Bound while the node is still the last item, so a closed list has its menu too.
      this.DrawListMenu(list);

      if (!open)
      {
        return;
      }

      ImGui.Unindent(ImGui.GetTreeNodeToLabelSpacing());

      var sheet = this.context.Plugin.DataManager.Excel.GetSheet<Item>();

      // A snapshot, because the item menu can take a row off the list while this is drawing it.
      var ids = list.ItemIds.ToArray();

      for (var i = 0; i < ids.Length; i++)
      {
        var id = ids[i];
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

        if (ImGui.Selectable($"{itemName}##list{list.Id}item{id}", this.context.SelectedItem?.RowId == id))
        {
          this.context.SelectItem(id, true);
        }

        if (ImGui.BeginPopupContextItem($"listItemMenu{list.Id}item{id}"))
        {
          if (ImGui.Selectable("Remove from this list"))
          {
            this.context.Plugin.ItemLists.Remove(list, id);
          }

          if (ImGui.Selectable("Add to the shopping list"))
          {
            this.context.TryAddCheapestToShoppingList(item.Value, false);
          }

          this.context.DrawListsMenu(id);

          ImGui.EndPopup();
        }
      }

      ImGui.Indent(ImGui.GetTreeNodeToLabelSpacing());
      ImGui.TreePop();
    }

    /// <summary>
    /// Draws the box that renames a list, in place of its node.
    /// </summary>
    /// <param name="list">The list being renamed.</param>
    private void DrawRenameBox(ItemList list)
    {
      ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

      if (!ImGui.IsAnyItemActive())
      {
        ImGui.SetKeyboardFocusHere();
      }

      var text = this.renameText;
      var committed = ImGui.InputText($"##rename{list.Id}", ref text, 64, ImGuiInputTextFlags.EnterReturnsTrue);
      this.renameText = text;

      if (committed)
      {
        this.context.Plugin.ItemLists.Rename(list, this.renameText);
        this.renaming = Guid.Empty;
        return;
      }

      // Clicking away leaves the name as it was.
      if (ImGui.IsItemDeactivated())
      {
        this.renaming = Guid.Empty;
      }
    }

    private void DrawListMenu(ItemList list)
    {
      if (!ImGui.BeginPopupContextItem($"listMenu{list.Id}"))
      {
        return;
      }

      if (ImGui.Selectable("Rename"))
      {
        this.renaming = list.Id;
        this.renameText = list.Name;
      }

      if (ImGui.Selectable("Delete"))
      {
        this.deleting = list.Id;
      }

      ImGui.Separator();

      this.DrawListShoppingEntries(list);

      ImGui.EndPopup();
    }

    private void DrawDeleteConfirm()
    {
      if (this.deleting == Guid.Empty)
      {
        return;
      }

      var store = this.context.Plugin.ItemLists;
      var list = store.Find(this.deleting);

      if (list == null)
      {
        this.deleting = Guid.Empty;
        return;
      }

      ImGui.OpenPopup(DeletePopupId);

      if (!ImGui.BeginPopupModal(DeletePopupId, ImGuiWindowFlags.AlwaysAutoResize))
      {
        return;
      }

      var count = list.ItemIds.Count == 1 ? "1 item" : $"{list.ItemIds.Count} items";
      ImGui.Text($"Delete \"{list.Name}\" and its {count}?");

      ImGui.Separator();

      if (ImGui.Button("Delete"))
      {
        store.Delete(list);
        this.deleting = Guid.Empty;
        ImGui.CloseCurrentPopup();
      }

      ImGui.SameLine();

      if (ImGui.Button("Cancel"))
      {
        this.deleting = Guid.Empty;
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndPopup();
    }

    /// <summary>
    /// Draws the entries that hand a whole list to the shopping list, or take it back off.
    /// </summary>
    /// <param name="list">The list the menu belongs to.</param>
    private void DrawListShoppingEntries(ItemList list)
    {
      var plugin = this.context.Plugin;
      var bulkAdd = plugin.ShoppingListBulkAdd;
      var scope = plugin.ShoppingListScope;
      var sheet = plugin.DataManager.Excel.GetSheet<Item>();

      var listed = new HashSet<uint>();
      var priced = new HashSet<uint>();

      foreach (var saved in plugin.ShoppingList)
      {
        listed.Add(saved.SourceItem.RowId);

        // A row added straight from a listing was never priced, so it does not stand in for one.
        if (!saved.IsDirect)
        {
          priced.Add(saved.SourceItem.RowId);
        }
      }

      var missing = new List<Item>();
      var anyListed = false;

      foreach (var id in list.ItemIds)
      {
        var item = sheet.GetRowOrDefault(id);

        if (!item.HasValue)
        {
          continue;
        }

        if (listed.Contains(id))
        {
          anyListed = true;
        }

        if (!priced.Contains(id))
        {
          missing.Add(item.Value);
        }
      }

      if (missing.Count > 0)
      {
        // The prices come from Universalis a chunk at a time, so only one job can run at once.
        var busy = bulkAdd.IsRunning || !scope.HasSelection;

        ImGui.BeginDisabled(busy);

        if (ImGui.Selectable("Add all to the shopping list"))
        {
          bulkAdd.Start(list.Name, missing, scope.QueryTargets);
        }

        ImGui.EndDisabled();
      }

      if (anyListed && ImGui.Selectable("Remove all from the shopping list"))
      {
        var ids = new HashSet<uint>(list.ItemIds);

        plugin.ShoppingList.RemoveAll(s => ids.Contains(s.SourceItem.RowId));
      }
    }
  }
}
