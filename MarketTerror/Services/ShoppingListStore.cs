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
          var entry = new SavedItem(item.Value, stored.Price, stored.World, stored.Quantity, stored.Hq)
          {
            Unlisted = stored.Unlisted,
            IsDirect = stored.IsDirect,
            Limit = stored.Limit?.Clone(),
          };

          entry.Picks.AddRange(stored.Picks.Select(p => p.ToPick()));
          this.items.Add(entry);
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
        var existing = this.FindRefreshable(entry.SourceItem.RowId);

        if (existing == null || existing.HasPicks || existing.IsLimited)
        {
          // A picked row is priced by its picks, which the pick refresh updates on their own, and a
          // limited one by whatever its rule sweeps up - never by the cheapest listing in the scope.
          continue;
        }

        existing.Price = entry.Price;
        existing.World = entry.World;
        existing.Quantity = entry.Quantity;
        existing.Hq = entry.Hq;
        existing.Outcome = BuyOutcome.None;
        existing.Unlisted = false;
        changed = true;
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
        var existing = this.FindRefreshable(id);

        if (existing == null || existing.Unlisted)
        {
          continue;
        }

        existing.Outcome = BuyOutcome.None;
        existing.Unlisted = true;
        changed = true;

        if (existing.HasPicks)
        {
          // Nothing of this item is on sale in the scope, so none of the picked listings can still be.
          foreach (var pick in existing.Picks)
          {
            pick.Gone = true;
          }

          continue;
        }

        // The quality stays, since it is what the row goes looking for the next time it is priced.
        existing.Price = 0;
        existing.Quantity = 0;
        existing.World = string.Empty;
      }

      if (changed)
      {
        this.Save();
      }
    }

    /// <summary>
    /// Marks the given entries as waiting for a new price, so the table can say so until one lands.
    /// </summary>
    /// <param name="itemIds">The row ids of the items being priced again.</param>
    public void MarkRefreshing(IEnumerable<uint> itemIds)
    {
      ArgumentNullException.ThrowIfNull(itemIds);

      foreach (var id in itemIds)
      {
        var existing = this.FindRefreshable(id);

        if (existing != null)
        {
          existing.Refreshing = true;
        }
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
    /// Forgets the buy outcome of every row the test matches, so they go back to being uncoloured.
    /// </summary>
    /// <param name="match">The test a row has to pass to lose its outcome.</param>
    public void ClearOutcomes(Func<SavedItem, bool> match)
    {
      ArgumentNullException.ThrowIfNull(match);

      foreach (var row in this.items.Where(match))
      {
        row.Outcome = BuyOutcome.None;
        row.FailReason = string.Empty;

        foreach (var pick in row.Picks)
        {
          pick.Outcome = BuyOutcome.None;
          pick.FailReason = string.Empty;
          pick.Paid = null;
        }
      }
    }

    /// <summary>
    /// Gets the entry a pricing job is allowed to touch for an item.
    /// </summary>
    /// <param name="itemId">The row id of the item to look for.</param>
    /// <returns>The entry, or null when the item has no ordinary row on the list.</returns>
    public SavedItem? Find(uint itemId)
    {
      return this.FindRefreshable(itemId);
    }

    /// <summary>
    /// Turns a row added straight from a listing into an ordinary one, so refreshes price it again.
    /// </summary>
    /// <param name="item">The row to convert.</param>
    public void ConvertToItemListing(SavedItem item)
    {
      ArgumentNullException.ThrowIfNull(item);

      if (!item.IsDirect)
      {
        return;
      }

      item.IsDirect = false;
      this.Save();
    }

    /// <summary>
    /// Replaces the listings a row has been told to buy, and writes the list out.
    /// </summary>
    /// <param name="item">The row to set the picks on.</param>
    /// <param name="picks">The picks, or an empty list to go back to standing for one listing.</param>
    public void SetPicks(SavedItem item, IEnumerable<PickedListing> picks)
    {
      ArgumentNullException.ThrowIfNull(item);

      item.SetPicks(picks);
      this.Save();
    }

    /// <summary>
    /// Replaces the standing rule that picks a row's listings, along with the listings it chose.
    /// </summary>
    /// <param name="item">The row to set the rule on.</param>
    /// <param name="limit">The rule, or null to go back to picking the listings by hand.</param>
    /// <param name="picks">The listings the rule chose, or the ones ticked by hand when there is no rule.</param>
    public void SetLimit(SavedItem item, ListingLimit? limit, IEnumerable<PickedListing> picks)
    {
      ArgumentNullException.ThrowIfNull(item);

      item.Limit = limit;

      if (limit == null)
      {
        item.SetPicks(picks);
      }
      else
      {
        item.SetSweptPicks(picks);
      }

      this.Save();
    }

    /// <summary>
    /// Writes the list out after something changed a row in place.
    /// </summary>
    public void Persist()
    {
      this.Save();
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

    /// <summary>
    /// Gets the entry for an item, skipping the rows added straight from a listing.
    /// </summary>
    /// <param name="itemId">The row id of the item to look for.</param>
    /// <returns>The entry, or null when the item has no ordinary row on the list.</returns>
    private SavedItem? FindRefreshable(uint itemId)
    {
      return this.items.Find(i => i.SourceItem.RowId == itemId && !i.IsDirect);
    }

    private void Save()
    {
      this.Revision++;

      var stored = this.plugin.Config.ShoppingList;
      stored.Clear();

      foreach (var item in this.items)
      {
        // A picked row's price, world, stack size and quality are read off its picks, so these four
        // are its summary rather than a listing. Nothing reads them back while picks are on the row,
        // and taking the last pick off rebuilds them from the picks that were there.
        var row = new StoredItem(item.SourceItem.RowId, item.Price, item.World, item.Unlisted, item.Quantity, item.Hq, item.IsDirect)
        {
          Limit = item.Limit?.Clone(),
        };
        row.Picks.AddRange(item.Picks.Select(p => new StoredPick(p)));
        stored.Add(row);
      }

      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }
  }
}
