// <copyright file="ItemFilter.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Dalamud.Utility;
  using Lumina.Excel.Sheets;
  using MarketTerror.Extensions;
  using MarketTerror.Helpers;

  /// <summary>
  /// The criteria an item has to match to appear in the item list.
  /// </summary>
  public sealed class ItemFilter
  {
    private readonly HashSet<uint> categories;

    private readonly HashSet<byte> rarities;

    private readonly Func<Item, UnlockState>? unlockProbe;

    private readonly HashSet<uint>? itemIds;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemFilter"/> class.
    /// </summary>
    /// <param name="searchString">The item name fragment to search for, or an empty string to match every name.</param>
    /// <param name="categories">The item search category row ids to keep, or an empty set to keep every category.</param>
    /// <param name="rarities">The rarities to keep, or an empty set to keep every rarity.</param>
    /// <param name="minLevel">The minimum equip level.</param>
    /// <param name="maxLevel">The maximum equip level.</param>
    /// <param name="minItemLevel">The minimum item level.</param>
    /// <param name="maxItemLevel">The maximum item level.</param>
    /// <param name="classJob">The class job to filter by, or null for all classes.</param>
    /// <param name="unlocked">The unlock state to keep, or null to keep every item.</param>
    /// <param name="unlockProbe">Reads the unlock state of an item, or null to leave the unlock state unfiltered.</param>
    /// <param name="itemIds">The row ids to keep, or null to keep an item whatever list it came from.</param>
    public ItemFilter(
      string searchString,
      IEnumerable<uint> categories,
      IEnumerable<byte> rarities,
      int minLevel,
      int maxLevel,
      int minItemLevel,
      int maxItemLevel,
      ClassJob? classJob,
      bool? unlocked = null,
      Func<Item, UnlockState>? unlockProbe = null,
      IEnumerable<uint>? itemIds = null)
    {
      this.SearchString = searchString ?? string.Empty;
      this.categories = new HashSet<uint>(categories ?? Enumerable.Empty<uint>());
      this.rarities = new HashSet<byte>(rarities ?? Enumerable.Empty<byte>());
      this.MinLevel = minLevel;
      this.MaxLevel = maxLevel;
      this.MinItemLevel = minItemLevel;
      this.MaxItemLevel = maxItemLevel;
      this.ClassJob = classJob;
      this.Unlocked = unlocked;
      this.unlockProbe = unlockProbe;
      this.itemIds = itemIds == null ? null : new HashSet<uint>(itemIds);
    }

    /// <summary>
    /// Gets a filter that keeps the whole catalogue.
    /// </summary>
    public static ItemFilter None { get; } = new ItemFilter(
      string.Empty,
      Array.Empty<uint>(),
      Array.Empty<byte>(),
      0,
      int.MaxValue,
      0,
      int.MaxValue,
      null);

    /// <summary>
    /// Gets the item name fragment to search for.
    /// </summary>
    public string SearchString { get; }

    /// <summary>
    /// Gets the minimum equip level.
    /// </summary>
    public int MinLevel { get; }

    /// <summary>
    /// Gets the maximum equip level.
    /// </summary>
    public int MaxLevel { get; }

    /// <summary>
    /// Gets the minimum item level.
    /// </summary>
    public int MinItemLevel { get; }

    /// <summary>
    /// Gets the maximum item level.
    /// </summary>
    public int MaxItemLevel { get; }

    /// <summary>
    /// Gets the class job to filter by, or null for all classes.
    /// </summary>
    public ClassJob? ClassJob { get; }

    /// <summary>
    /// Gets the unlock state to keep, or null to keep every item.
    /// </summary>
    public bool? Unlocked { get; }

    /// <summary>
    /// Gets the number of items whose unlock state was read while this filter was applied.
    /// </summary>
    public int UnlockProbed { get; private set; }

    /// <summary>
    /// Gets the number of those reads the game could not answer, so they are worth trying again.
    /// </summary>
    public int UnlockUnreadable { get; private set; }

    /// <summary>
    /// Checks whether a category survives the filter.
    /// </summary>
    /// <param name="category">The item search category.</param>
    /// <returns>True when the category is kept.</returns>
    public bool IncludesCategory(ItemSearchCategory category)
    {
      return this.categories.Count == 0 || this.categories.Contains(category.RowId);
    }

    /// <summary>
    /// Checks whether an item survives the filter.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>True when the item is kept.</returns>
    public bool Matches(Item item)
    {
      if (this.itemIds != null && !this.itemIds.Contains(item.RowId))
      {
        return false;
      }

      if (this.SearchString.Length > 0
        && !item.Name.ExtractText().Contains(this.SearchString, StringComparison.InvariantCultureIgnoreCase))
      {
        return false;
      }

      if (item.LevelEquip < this.MinLevel || item.LevelEquip > this.MaxLevel)
      {
        return false;
      }

      if (item.LevelItem.RowId < this.MinItemLevel || item.LevelItem.RowId > this.MaxItemLevel)
      {
        return false;
      }

      if (this.rarities.Count > 0 && !this.rarities.Contains(item.Rarity))
      {
        return false;
      }

      if (!item.ClassJobCategory.Value.HasClass(this.ClassJob))
      {
        return false;
      }

      if (this.Unlocked == null || this.unlockProbe == null)
      {
        return true;
      }

      var state = this.unlockProbe(item);

      this.UnlockProbed++;

      // The item is only hidden for now; the catalogue reads it again while these keep dropping.
      if (state == UnlockState.Unreadable)
      {
        this.UnlockUnreadable++;

        return false;
      }

      // Items that unlock nothing report no state, so they drop out of both unlock states.
      return state == (this.Unlocked.Value ? UnlockState.Unlocked : UnlockState.Locked);
    }

    /// <summary>
    /// Checks whether another filter would produce the same results as this one.
    /// </summary>
    /// <param name="other">The filter to compare against.</param>
    /// <returns>True when both filters are equivalent.</returns>
    public bool SameAs(ItemFilter other)
    {
      return other != null
        && this.SearchString == other.SearchString
        && this.MinLevel == other.MinLevel
        && this.MaxLevel == other.MaxLevel
        && this.MinItemLevel == other.MinItemLevel
        && this.MaxItemLevel == other.MaxItemLevel
        && this.ClassJob?.RowId == other.ClassJob?.RowId
        && this.Unlocked == other.Unlocked
        && this.categories.SetEquals(other.categories)
        && this.rarities.SetEquals(other.rarities)
        && SameIds(this.itemIds, other.itemIds);
    }

    private static bool SameIds(HashSet<uint>? ids, HashSet<uint>? other)
    {
      return ids == null || other == null ? ids == other : ids.SetEquals(other);
    }
  }
}
