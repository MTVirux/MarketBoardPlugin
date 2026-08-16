// <copyright file="ShoppingListStore.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
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
          this.items.Add(new SavedItem(item.Value, stored.Price, stored.World, stored.Quantity, stored.Hq)
          {
            Unlisted = stored.Unlisted,
          });
        }
      }
    }

    /// <summary>
    /// Gets a number that changes whenever the list does, so views can tell when to rebuild.
    /// </summary>
    public int Revision { get; private set; }

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
    /// Updates the price and world of the entries whose item is already on the list.
    /// </summary>
    /// <param name="entries">The freshly priced entries.</param>
    public void Replace(IEnumerable<SavedItem> entries)
    {
      ArgumentNullException.ThrowIfNull(entries);

      var changed = false;

      foreach (var entry in entries)
      {
        var existing = this.items.Find(i => i.SourceItem.RowId == entry.SourceItem.RowId);

        if (existing != null)
        {
          existing.Price = entry.Price;
          existing.World = entry.World;
          existing.Quantity = entry.Quantity;
          existing.Hq = entry.Hq;
          existing.Outcome = BuyOutcome.None;
          existing.Unlisted = false;
          changed = true;
        }
      }

      if (changed)
      {
        this.Save();
      }
    }

    /// <summary>
    /// Drops the price and world of the entries whose item found no listings, so their row shows dashes
    /// instead of a price from a scope that is no longer the selected one.
    /// </summary>
    /// <param name="itemIds">The row ids of the items with nothing on sale.</param>
    public void MarkUnlisted(IEnumerable<uint> itemIds)
    {
      ArgumentNullException.ThrowIfNull(itemIds);

      var changed = false;

      foreach (var id in itemIds)
      {
        var existing = this.items.Find(i => i.SourceItem.RowId == id);

        if (existing == null || existing.Unlisted)
        {
          continue;
        }

        existing.Price = 0;
        existing.Quantity = 0;
        existing.Hq = false;
        existing.Outcome = BuyOutcome.None;
        existing.World = string.Empty;
        existing.Unlisted = true;
        changed = true;
      }

      if (changed)
      {
        this.Save();
      }
    }

    /// <summary>
    /// Marks every entry as waiting for a new price, so the table can say so until one lands.
    /// </summary>
    public void MarkRefreshing()
    {
      foreach (var item in this.items)
      {
        item.Refreshing = true;
      }
    }

    /// <summary>
    /// Clears the waiting flag set by <see cref="MarkRefreshing"/> from every entry.
    /// </summary>
    public void ClearRefreshing()
    {
      foreach (var item in this.items)
      {
        item.Refreshing = false;
      }
    }

    /// <summary>
    /// Forgets the buy outcome of every entry, so a new pricing job starts from uncoloured rows.
    /// </summary>
    public void ClearOutcomes()
    {
      foreach (var item in this.items)
      {
        item.Outcome = BuyOutcome.None;
      }
    }

    /// <summary>
    /// Gets the entry for an item.
    /// </summary>
    /// <param name="itemId">The row id of the item to look for.</param>
    /// <returns>The entry, or null when the item is not on the list.</returns>
    public SavedItem? Find(uint itemId)
    {
      return this.items.Find(i => i.SourceItem.RowId == itemId);
    }

    /// <summary>
    /// Empties the shopping list.
    /// </summary>
    public void Clear()
    {
      if (this.items.Count == 0)
      {
        return;
      }

      this.items.Clear();
      this.Save();
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
      this.Revision++;

      var stored = this.plugin.Config.ShoppingList;
      stored.Clear();

      foreach (var item in this.items)
      {
        stored.Add(new StoredItem(item.SourceItem.RowId, item.Price, item.World, item.Unlisted, item.Quantity, item.Hq));
      }

      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }
  }
}
