// <copyright file="ShoppingListStore.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using Lumina.Excel.Sheets;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// The shopping list, held in memory and mirrored into the configuration so it survives a reload.
  /// </summary>
  public sealed class ShoppingListStore : IReadOnlyList<SavedItem>
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly List<SavedItem> items = new List<SavedItem>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingListStore"/> class, filled with the saved entries.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public ShoppingListStore(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      var sheet = this.plugin.DataManager.Excel.GetSheet<Item>();

      foreach (var stored in this.plugin.Config.ShoppingList)
      {
        var item = sheet.GetRowOrDefault(stored.ItemId);

        if (item.HasValue)
        {
          this.items.Add(new SavedItem(item.Value, stored.Price, stored.World));
        }
      }
    }

    /// <inheritdoc/>
    public int Count => this.items.Count;

    /// <inheritdoc/>
    public SavedItem this[int index] => this.items[index];

    /// <summary>
    /// Adds an entry to the shopping list.
    /// </summary>
    /// <param name="item">The entry to add.</param>
    public void Add(SavedItem item)
    {
      this.items.Add(item);
      this.Save();
    }

    /// <summary>
    /// Adds several entries to the shopping list at once.
    /// </summary>
    /// <param name="entries">The entries to add.</param>
    public void AddRange(IEnumerable<SavedItem> entries)
    {
      ArgumentNullException.ThrowIfNull(entries);

      var added = this.items.Count;
      this.items.AddRange(entries);

      if (this.items.Count != added)
      {
        this.Save();
      }
    }

    /// <summary>
    /// Removes an entry from the shopping list.
    /// </summary>
    /// <param name="item">The entry to remove.</param>
    /// <returns>True when the entry was on the list.</returns>
    public bool Remove(SavedItem item)
    {
      if (!this.items.Remove(item))
      {
        return false;
      }

      this.Save();
      return true;
    }

    /// <summary>
    /// Removes every entry matching a condition.
    /// </summary>
    /// <param name="match">The condition an entry has to match to be removed.</param>
    /// <returns>The number of entries removed.</returns>
    public int RemoveAll(Predicate<SavedItem> match)
    {
      var removed = this.items.RemoveAll(match);

      if (removed > 0)
      {
        this.Save();
      }

      return removed;
    }

    /// <inheritdoc/>
    public IEnumerator<SavedItem> GetEnumerator() => this.items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private void Save()
    {
      var stored = this.plugin.Config.ShoppingList;
      stored.Clear();

      foreach (var item in this.items)
      {
        stored.Add(new StoredItem(item.SourceItem.RowId, item.Price, item.World));
      }

      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }
  }
}
