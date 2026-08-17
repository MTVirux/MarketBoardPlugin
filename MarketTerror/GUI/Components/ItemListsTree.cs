// <copyright file="ItemListsTree.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using Dalamud.Bindings.ImGui;
  using Lumina.Excel.Sheets;
  using MarketTerror.Models.ItemLists;

  /// <summary>
  /// The item lists the user has made, drawn as one collapsible node each.
  /// </summary>
  public sealed class ItemListsTree
  {
    private readonly MarketBoardContext context;

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
    }

    private bool MatchesSearch(string itemName)
    {
      var search = this.context.SearchString;

      return string.IsNullOrEmpty(search)
        || itemName.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void DrawList(ItemList list, int index)
    {
      var open = ImGui.TreeNode($"{list.Name} ({list.ItemIds.Count})##list{list.Id}");

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
      }

      ImGui.Indent(ImGui.GetTreeNodeToLabelSpacing());
      ImGui.TreePop();
    }
  }
}
