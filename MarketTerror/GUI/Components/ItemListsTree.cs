// <copyright file="ItemListsTree.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Text;
  using Dalamud.Bindings.ImGui;
  using Lumina.Excel.Sheets;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Models.ItemLists;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// The item lists the user has made, drawn as one collapsible node each.
  /// </summary>
  public sealed class ItemListsTree
  {
    /// <summary>
    /// What the box confirming a delete is registered under.
    /// </summary>
    private const string DeletePopupId = "Delete list##deleteItemList";

    /// <summary>
    /// The drag payload marking a list being moved among the other lists.
    /// </summary>
    private const string ListPayload = "MTLIST";

    /// <summary>
    /// The drag payload marking an item being moved within its list.
    /// </summary>
    private const string ItemPayload = "MTLISTITEM";

    private readonly MarketBoardContext context;

    // Where the drag started. The payload itself carries nothing; it only says which kind of drag this is.
    private int draggedList = -1;

    private Guid draggedItemList = Guid.Empty;

    private int draggedItem = -1;

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

      // The node keeps its own indent off, so the items below can be lined up by hand.
      var open = ImGui.TreeNodeEx(
        $"{list.Name} ({list.ItemIds.Count})##list{list.Id}",
        ImGuiTreeNodeFlags.NoTreePushOnOpen);

      // Both bind to the node while it is still the last item, so a closed list keeps them.
      // The drag goes first: an open menu draws its own items, which would take the binding.
      this.DrawListDragDrop(list, index);
      this.DrawListMenu(list);

      if (!open)
      {
        return;
      }

      // A selectable draws its name right where the cursor is, so push the items in to where
      // the list's own name starts.
      ImGui.Indent(ImGui.GetTreeNodeToLabelSpacing());

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

        this.DrawItemDragDrop(list, i, itemName);

        if (ImGui.BeginPopupContextItem($"listItemMenu{list.Id}item{id}"))
        {
          if (ImGui.Selectable("Remove from this list"))
          {
            this.context.Plugin.ItemLists.Remove(list, id);
          }

          if (ImGui.Selectable("Add to the shopping list"))
          {
            this.context.TryAddCheapestToShoppingList(item.Value);
          }

          this.context.DrawListsMenu(id);

          ImGui.EndPopup();
        }
      }

      ImGui.Unindent(ImGui.GetTreeNodeToLabelSpacing());
    }

    /// <summary>
    /// Lets a list be dragged to a different place among the others.
    /// </summary>
    /// <param name="list">The list the node belongs to.</param>
    /// <param name="index">Where the list sits now.</param>
    private void DrawListDragDrop(ItemList list, int index)
    {
      if (ImGui.BeginDragDropSource())
      {
        this.draggedList = index;
        ImGui.SetDragDropPayload(ListPayload, ReadOnlySpan<byte>.Empty);
        ImGui.Text(list.Name);
        ImGui.EndDragDropSource();
      }

      if (!ImGui.BeginDragDropTarget())
      {
        return;
      }

      if (!ImGui.AcceptDragDropPayload(ListPayload).IsNull && this.draggedList >= 0)
      {
        this.context.Plugin.ItemLists.MoveList(this.draggedList, index);
        this.draggedList = -1;
      }

      ImGui.EndDragDropTarget();
    }

    /// <summary>
    /// Lets an item be dragged to a different place within its own list.
    /// </summary>
    /// <param name="list">The list the item is on.</param>
    /// <param name="index">Where the item sits now.</param>
    /// <param name="itemName">The item's name, shown under the cursor while it is dragged.</param>
    private void DrawItemDragDrop(ItemList list, int index, string itemName)
    {
      // A search hides rows, so the drop would land against a position the user cannot see.
      if (!string.IsNullOrEmpty(this.context.SearchString))
      {
        return;
      }

      if (ImGui.BeginDragDropSource())
      {
        this.draggedItemList = list.Id;
        this.draggedItem = index;
        ImGui.SetDragDropPayload(ItemPayload, ReadOnlySpan<byte>.Empty);
        ImGui.Text(itemName);
        ImGui.EndDragDropSource();
      }

      if (!ImGui.BeginDragDropTarget())
      {
        return;
      }

      // Within the same list only, so a stray drop cannot quietly move an item out of one.
      if (!ImGui.AcceptDragDropPayload(ItemPayload).IsNull
        && this.draggedItemList == list.Id
        && this.draggedItem >= 0)
      {
        this.context.Plugin.ItemLists.MoveItem(list, this.draggedItem, index);
        this.draggedItem = -1;
      }

      ImGui.EndDragDropTarget();
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

      ImGui.Separator();

      // Ids rather than names, so a list survives being moved to a client in another language.
      if (ImGui.Selectable("Copy item IDs"))
      {
        this.CopyIds(list);
      }

      if (ImGui.Selectable("Paste item IDs"))
      {
        this.PasteIds(list);
      }

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

      ModalDim.Mark();

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
    /// Puts a list's item ids on the clipboard, one to a line.
    /// </summary>
    /// <param name="list">The list to copy.</param>
    private void CopyIds(ItemList list)
    {
      if (list.ItemIds.Count == 0)
      {
        return;
      }

      var builder = new StringBuilder();

      foreach (var id in list.ItemIds)
      {
        builder.AppendLine(id.ToString(CultureInfo.InvariantCulture));
      }

      ImGui.SetClipboardText(builder.ToString().TrimEnd());

      if (this.context.Config.ClipboardNotificationsEnabled)
      {
        this.context.Plugin.NotifyClipboardCopied($"{list.ItemIds.Count} item IDs from {list.Name}");
      }
    }

    /// <summary>
    /// Adds the item ids on the clipboard to a list, dropping the ones the game does not know.
    /// </summary>
    /// <param name="list">The list to paste into.</param>
    private void PasteIds(ItemList list)
    {
      var text = ImGui.GetClipboardText();

      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      var sheet = this.context.Plugin.DataManager.Excel.GetSheet<Item>();
      var valid = new List<uint>();
      var skipped = 0;

      foreach (var line in text.Split('\n'))
      {
        var trimmed = line.Trim();

        if (trimmed.Length == 0)
        {
          continue;
        }

        if (!uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
          || !sheet.GetRowOrDefault(id).HasValue)
        {
          skipped++;
          continue;
        }

        valid.Add(id);
      }

      var added = this.context.Plugin.ItemLists.AddRange(list, valid);

      // The ones that parsed but were already on the list were not added either.
      skipped += valid.Count - added;

      if (this.context.Config.ClipboardNotificationsEnabled)
      {
        this.context.Plugin.NotifyClipboard($"Pasted {added} items into {list.Name}, skipped {skipped}.");
      }
    }

    /// <summary>
    /// Draws the entries that hand a whole list to the shopping list, or take it back off.
    /// </summary>
    /// <param name="list">The list the menu belongs to.</param>
    private void DrawListShoppingEntries(ItemList list)
    {
      var plugin = this.context.Plugin;
      var bulkAdd = plugin.ShoppingListBulkAdd;
      var picker = plugin.ShoppingListScope;
      var sheet = plugin.DataManager.Excel.GetSheet<Item>();

      // The menu only ever acts on the market the buy list is pointing at, so what is already on the
      // list somewhere else is none of its business.
      var scope = picker.ToListingScope();

      var listed = new HashSet<uint>();
      var priced = new HashSet<uint>();

      foreach (var entry in plugin.ShoppingList)
      {
        if (!entry.Scope.Equals(scope))
        {
          continue;
        }

        listed.Add(entry.SourceItem.RowId);

        // A direct or conditional entry buys particular listings, so it does not stand in for the
        // item being on the list at its cheapest.
        if (entry.Kind == ListingKind.Lowest)
        {
          priced.Add(entry.SourceItem.RowId);
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
        var busy = bulkAdd.IsRunning || !picker.HasSelection;

        ImGui.BeginDisabled(busy);

        if (ImGui.Selectable("Add all to the shopping list"))
        {
          bulkAdd.Start(list.Name, missing, scope);
        }

        ImGui.EndDisabled();
      }

      if (anyListed && ImGui.Selectable("Remove all from the shopping list"))
      {
        var ids = new HashSet<uint>(list.ItemIds);

        // Only this market's entries go, since that is the only one the entry above adds into.
        plugin.ShoppingList.RemoveAll(s => ids.Contains(s.SourceItem.RowId) && s.Scope.Equals(scope));
      }
    }
  }
}
