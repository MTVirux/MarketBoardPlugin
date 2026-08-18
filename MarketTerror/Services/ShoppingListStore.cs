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
  /// <remarks>
  /// The same item can sit on the list several times over, once per market it is being shopped for in
  /// and once per way of choosing that market's listings, so nothing here is keyed by item id alone.
  /// </remarks>
  public sealed class ShoppingListStore : IReadOnlyList<ListingEntry>
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly List<ListingEntry> items = new List<ListingEntry>();

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

        if (!item.HasValue)
        {
          // Dropping the entries whose item cannot be looked up is what clears out whatever an older
          // configuration left behind.
          continue;
        }

        var entry = new ListingEntry(item.Value, new ListingScope(stored.AnchorWorld, stored.Level), stored.Kind)
        {
          Count = Math.Max(1, stored.Count),
          Target = stored.Target?.ToListing(),
          Conditions = stored.Conditions?.Clone(),
        };

        entry.Matches.AddRange(stored.Matches.Select(m => m.ToListing()));
        this.items.Add(entry);
      }
    }

    /// <summary>
    /// Gets a number that changes whenever the list does, so views can tell when to rebuild.
    /// </summary>
    public int Revision { get; private set; }

    /// <inheritdoc/>
    public int Count => this.items.Count;

    /// <summary>
    /// Gets the markets the list is shopping in, each named once.
    /// </summary>
    public IReadOnlyList<ListingScope> Scopes => this.items.Select(e => e.Scope).Distinct().ToArray();

    /// <inheritdoc/>
    public ListingEntry this[int index] => this.items[index];

    /// <summary>
    /// Adds an entry that buys the cheapest listing of an item in a scope, or hands back the one
    /// already there.
    /// </summary>
    /// <param name="item">The item to buy.</param>
    /// <param name="scope">The market to buy it in.</param>
    /// <param name="added">False when the entry handed back was already on the list.</param>
    /// <returns>The entry.</returns>
    public ListingEntry AddLowest(Item item, ListingScope scope, out bool added)
    {
      ArgumentNullException.ThrowIfNull(scope);

      var existing = this.FindLowest(item.RowId, scope);

      if (existing != null)
      {
        added = false;
        return existing;
      }

      added = true;

      var entry = new ListingEntry(item, scope, ListingKind.Lowest);
      this.items.Add(entry);
      this.Save();

      return entry;
    }

    /// <summary>
    /// Adds a cheapest listing entry for each of several items in one scope, leaving alone the items
    /// that already have one there.
    /// </summary>
    /// <param name="items">The items to buy.</param>
    /// <param name="scope">The market to buy them in.</param>
    public void AddLowestRange(IEnumerable<Item> items, ListingScope scope)
    {
      ArgumentNullException.ThrowIfNull(items);
      ArgumentNullException.ThrowIfNull(scope);

      var added = false;

      foreach (var item in items)
      {
        if (this.FindLowest(item.RowId, scope) != null)
        {
          continue;
        }

        this.items.Add(new ListingEntry(item, scope, ListingKind.Lowest));
        added = true;
      }

      if (added)
      {
        this.Save();
      }
    }

    /// <summary>
    /// Adds an entry that buys one particular listing.
    /// </summary>
    /// <param name="item">The item to buy.</param>
    /// <param name="scope">The market the listing was seen in.</param>
    /// <param name="listing">The listing to buy.</param>
    /// <returns>The entry.</returns>
    public ListingEntry AddDirect(Item item, ListingScope scope, ResolvedListing listing)
    {
      ArgumentNullException.ThrowIfNull(scope);
      ArgumentNullException.ThrowIfNull(listing);

      var entry = new ListingEntry(item, scope, ListingKind.Direct) { Target = listing };
      entry.Matches.Add(listing);

      this.items.Add(entry);
      this.Save();

      return entry;
    }

    /// <summary>
    /// Adds an entry that buys every listing in a scope a rule holds for.
    /// </summary>
    /// <param name="item">The item to buy.</param>
    /// <param name="scope">The market to buy it in.</param>
    /// <param name="conditions">The rule to buy by, which is copied so the editor's draft is not the stored one.</param>
    /// <returns>The entry, buying nothing until a refresh tells it what the rule caught.</returns>
    public ListingEntry AddConditional(Item item, ListingScope scope, ListingConditions conditions)
    {
      ArgumentNullException.ThrowIfNull(scope);
      ArgumentNullException.ThrowIfNull(conditions);

      var entry = new ListingEntry(item, scope, ListingKind.Conditional) { Conditions = conditions.Clone() };

      this.items.Add(entry);
      this.Save();

      return entry;
    }

    /// <summary>
    /// Removes an entry from the shopping list.
    /// </summary>
    /// <param name="entry">The entry to remove.</param>
    /// <returns>True when the entry was on the list.</returns>
    public bool Remove(ListingEntry entry)
    {
      if (!this.items.Remove(entry))
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
    public int RemoveAll(Predicate<ListingEntry> match)
    {
      var removed = this.items.RemoveAll(match);

      if (removed > 0)
      {
        this.Save();
      }

      return removed;
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
    /// Gets the entry that buys an item's cheapest listings in a scope.
    /// </summary>
    /// <param name="itemId">The row id of the item to look for.</param>
    /// <param name="scope">The market to look in.</param>
    /// <returns>The entry, or null when that item and scope have none.</returns>
    public ListingEntry? FindLowest(uint itemId, ListingScope scope)
    {
      return this.items.Find(e =>
        e.Kind == ListingKind.Lowest && e.SourceItem.RowId == itemId && e.Scope.Equals(scope));
    }

    /// <summary>
    /// Gets every entry shopping in one market.
    /// </summary>
    /// <param name="scope">The market to look in.</param>
    /// <returns>The entries, in the order they were added.</returns>
    public IEnumerable<ListingEntry> InScope(ListingScope scope)
    {
      return this.items.Where(e => e.Scope.Equals(scope));
    }

    /// <summary>
    /// Sets how many of its market's cheapest listings an entry takes.
    /// </summary>
    /// <param name="entry">The entry to set the count on.</param>
    /// <param name="count">The number of listings, held to at least one.</param>
    public void SetCount(ListingEntry entry, int count)
    {
      ArgumentNullException.ThrowIfNull(entry);

      var wanted = Math.Max(1, count);

      if (entry.Count == wanted)
      {
        return;
      }

      entry.Count = wanted;

      if (entry.Matches.Count > wanted)
      {
        // Asking for fewer listings only ever takes listings away, so the entry chooses again out of
        // the ones it already holds instead of waiting on a fetch. Cheapest first, the way a refresh
        // would order them, and the sold out ones go first since they buy nothing.
        var kept = entry.Matches
          .OrderBy(m => m.Gone)
          .ThenBy(m => m.Price)
          .ThenBy(m => m.Total)
          .Take(wanted)
          .ToArray();

        // Trimming is not a repricing, so the listings that stay keep whatever a buy run wrote on
        // them and the entry only says again what the ones left under it say.
        entry.Matches.Clear();
        entry.Matches.AddRange(kept);
        entry.Outcome = BuyOutcome.None;
        entry.FailReason = string.Empty;
        entry.RollUpOutcome();
      }

      this.Save();
    }

    /// <summary>
    /// Replaces the rule a conditional entry buys by.
    /// </summary>
    /// <param name="entry">The entry to set the rule on.</param>
    /// <param name="conditions">The rule, which is copied so the editor's draft is not the stored one.</param>
    public void SetConditions(ListingEntry entry, ListingConditions conditions)
    {
      ArgumentNullException.ThrowIfNull(entry);
      ArgumentNullException.ThrowIfNull(conditions);

      entry.Conditions = conditions.Clone();
      this.Save();
    }

    /// <summary>
    /// Replaces the listings an entry resolves to, and writes the list out.
    /// </summary>
    /// <param name="entry">The entry that was priced.</param>
    /// <param name="matches">The listings it now buys, which can be none.</param>
    public void ApplyMatches(ListingEntry entry, IReadOnlyList<ResolvedListing> matches)
    {
      ArgumentNullException.ThrowIfNull(entry);
      ArgumentNullException.ThrowIfNull(matches);

      entry.SetMatches(matches);
      entry.Refreshing = false;
      this.Save();
    }

    /// <summary>
    /// Marks the given entries as waiting for a new price, so the table can say so until one lands.
    /// </summary>
    /// <param name="entries">The entries being priced again.</param>
    /// <remarks>
    /// Only entries still on the list are marked, since <see cref="ClearRefreshing"/> walks the list
    /// and would never take the flag back off one that had been removed in the meantime.
    /// </remarks>
    public void MarkRefreshing(IEnumerable<ListingEntry> entries)
    {
      ArgumentNullException.ThrowIfNull(entries);

      var asked = new HashSet<ListingEntry>(entries);

      foreach (var entry in this.items.Where(asked.Contains))
      {
        entry.Refreshing = true;
      }
    }

    /// <summary>
    /// Clears the waiting flag set by <see cref="MarkRefreshing"/> from every entry.
    /// </summary>
    public void ClearRefreshing()
    {
      foreach (var entry in this.items)
      {
        entry.Refreshing = false;
      }
    }

    /// <summary>
    /// Forgets the buy outcome of every entry the test matches, so they go back to being uncoloured.
    /// </summary>
    /// <param name="match">The test an entry has to pass to lose its outcome.</param>
    public void ClearOutcomes(Func<ListingEntry, bool> match)
    {
      ArgumentNullException.ThrowIfNull(match);

      foreach (var entry in this.items.Where(match))
      {
        entry.Outcome = BuyOutcome.None;
        entry.FailReason = string.Empty;

        foreach (var listing in entry.Matches)
        {
          listing.Outcome = BuyOutcome.None;
          listing.FailReason = string.Empty;
          listing.Paid = null;
        }
      }
    }

    /// <summary>
    /// Writes the list out after something changed an entry in place.
    /// </summary>
    public void Persist()
    {
      this.Save();
    }

    /// <inheritdoc/>
    public IEnumerator<ListingEntry> GetEnumerator() => this.items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private void Save()
    {
      this.Revision++;

      var stored = this.plugin.Config.ShoppingList;
      stored.Clear();

      foreach (var entry in this.items)
      {
        stored.Add(new StoredEntry(entry));
      }

      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }
  }
}
