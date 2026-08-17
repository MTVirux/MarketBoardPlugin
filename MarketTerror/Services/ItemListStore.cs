// <copyright file="ItemListStore.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using MarketTerror.Models.ItemLists;

  /// <summary>
  /// The item lists, held in memory and mirrored into the configuration so they survive a reload.
  /// </summary>
  public sealed class ItemListStore : IReadOnlyList<ItemList>
  {
    /// <summary>
    /// What a list is called when it is made without a name.
    /// </summary>
    public const string DefaultName = "New list";

    private readonly MarketTerrorPlugin plugin;

    private readonly List<ItemList> lists = new List<ItemList>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemListStore"/> class, filled with the saved lists.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public ItemListStore(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      this.lists.AddRange(this.plugin.Config.ItemLists);
    }

    /// <summary>
    /// Gets a number that changes whenever the lists do, so views can tell when to rebuild.
    /// </summary>
    public int Revision { get; private set; }

    /// <inheritdoc/>
    public int Count => this.lists.Count;

    /// <inheritdoc/>
    public ItemList this[int index] => this.lists[index];

    /// <summary>
    /// Makes a new, empty list.
    /// </summary>
    /// <param name="name">What to call it, or blank to use <see cref="DefaultName"/>.</param>
    /// <returns>The new list.</returns>
    public ItemList Create(string name)
    {
      var list = new ItemList
      {
        Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim(),
      };

      this.lists.Add(list);
      this.Save();

      return list;
    }

    /// <summary>
    /// Renames a list, unless the new name is blank.
    /// </summary>
    /// <param name="list">The list to rename.</param>
    /// <param name="name">The new name.</param>
    public void Rename(ItemList list, string name)
    {
      ArgumentNullException.ThrowIfNull(list);

      if (string.IsNullOrWhiteSpace(name) || list.Name == name.Trim())
      {
        return;
      }

      list.Name = name.Trim();
      this.Save();
    }

    /// <summary>
    /// Deletes a list and everything on it.
    /// </summary>
    /// <param name="list">The list to delete.</param>
    /// <returns>True when the list was there to delete.</returns>
    public bool Delete(ItemList list)
    {
      if (!this.lists.Remove(list))
      {
        return false;
      }

      this.Save();
      return true;
    }

    /// <summary>
    /// Puts an item on a list, unless it is already on it.
    /// </summary>
    /// <param name="list">The list to add to.</param>
    /// <param name="itemId">The row id of the item.</param>
    /// <returns>True when the item was added.</returns>
    public bool Add(ItemList list, uint itemId)
    {
      ArgumentNullException.ThrowIfNull(list);

      if (list.ItemIds.Contains(itemId))
      {
        return false;
      }

      list.ItemIds.Add(itemId);
      this.Save();

      return true;
    }

    /// <summary>
    /// Puts several items on a list at once, skipping the ones already on it.
    /// </summary>
    /// <param name="list">The list to add to.</param>
    /// <param name="itemIds">The row ids of the items.</param>
    /// <returns>How many were actually added.</returns>
    public int AddRange(ItemList list, IEnumerable<uint> itemIds)
    {
      ArgumentNullException.ThrowIfNull(list);
      ArgumentNullException.ThrowIfNull(itemIds);

      var added = 0;

      foreach (var id in itemIds)
      {
        if (list.ItemIds.Contains(id))
        {
          continue;
        }

        list.ItemIds.Add(id);
        added++;
      }

      if (added > 0)
      {
        this.Save();
      }

      return added;
    }

    /// <summary>
    /// Takes an item off a list.
    /// </summary>
    /// <param name="list">The list to remove from.</param>
    /// <param name="itemId">The row id of the item.</param>
    /// <returns>True when the item was on the list.</returns>
    public bool Remove(ItemList list, uint itemId)
    {
      ArgumentNullException.ThrowIfNull(list);

      if (!list.ItemIds.Remove(itemId))
      {
        return false;
      }

      this.Save();
      return true;
    }

    /// <summary>
    /// Takes several items off a list at once.
    /// </summary>
    /// <param name="list">The list to remove from.</param>
    /// <param name="itemIds">The row ids of the items.</param>
    /// <returns>How many were actually removed.</returns>
    public int RemoveRange(ItemList list, IEnumerable<uint> itemIds)
    {
      ArgumentNullException.ThrowIfNull(list);
      ArgumentNullException.ThrowIfNull(itemIds);

      var removed = 0;

      foreach (var id in itemIds)
      {
        if (list.ItemIds.Remove(id))
        {
          removed++;
        }
      }

      if (removed > 0)
      {
        this.Save();
      }

      return removed;
    }

    /// <summary>
    /// Gets a list by what identifies it.
    /// </summary>
    /// <param name="id">The id of the list to look for.</param>
    /// <returns>The list, or null when there is no list with that id.</returns>
    public ItemList? Find(Guid id)
    {
      return this.lists.Find(l => l.Id == id);
    }

    /// <summary>
    /// Moves a list to a different place in the tree.
    /// </summary>
    /// <param name="from">Where the list is now.</param>
    /// <param name="to">Where it should end up.</param>
    public void MoveList(int from, int to)
    {
      if (!Move(this.lists, from, to))
      {
        return;
      }

      this.Save();
    }

    /// <summary>
    /// Moves an item to a different place within its list.
    /// </summary>
    /// <param name="list">The list holding the item.</param>
    /// <param name="from">Where the item is now.</param>
    /// <param name="to">Where it should end up.</param>
    public void MoveItem(ItemList list, int from, int to)
    {
      ArgumentNullException.ThrowIfNull(list);

      if (!Move(list.ItemIds, from, to))
      {
        return;
      }

      this.Save();
    }

    /// <inheritdoc/>
    public IEnumerator<ItemList> GetEnumerator() => this.lists.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private static bool Move<T>(List<T> items, int from, int to)
    {
      if (from == to || from < 0 || to < 0 || from >= items.Count || to >= items.Count)
      {
        return false;
      }

      var moved = items[from];
      items.RemoveAt(from);
      items.Insert(to, moved);

      return true;
    }

    private void Save()
    {
      this.Revision++;

      var stored = this.plugin.Config.ItemLists;
      stored.Clear();
      stored.AddRange(this.lists);

      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }
  }
}
