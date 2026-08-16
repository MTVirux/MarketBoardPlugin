// <copyright file="ItemCatalog.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Text.RegularExpressions;
  using Dalamud.Plugin.Services;
  using Dalamud.Utility;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// The searchable catalogue of marketable items, grouped by item search category.
  /// </summary>
  public sealed class ItemCatalog
  {
    private static readonly Dictionary<char, int> RomanNumberMap = new()
    {
      { 'I', 1 },
      { 'V', 5 },
      { 'X', 10 },
    };

    private readonly IEnumerable<Item> items;

    private readonly IPluginLog log;

    private readonly List<KeyValuePair<ItemSearchCategory, List<Item>>> sortedCategoriesAndItems;

    private readonly List<ItemSearchCategory> categories;

    private readonly List<ClassJob> classJobs;

    private List<KeyValuePair<ItemSearchCategory, List<Item>>> filtered;

    private ItemFilter? lastFilter;

    private int lastUnreadable = int.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemCatalog"/> class.
    /// </summary>
    /// <param name="dataManager">The data manager the game sheets are read from.</param>
    /// <param name="log">The plugin log.</param>
    public ItemCatalog(IDataManager dataManager, IPluginLog log)
    {
      ArgumentNullException.ThrowIfNull(dataManager);
      ArgumentNullException.ThrowIfNull(log);

      this.log = log;
      this.items = dataManager.GetExcelSheet<Item>();

      this.classJobs = dataManager.GetExcelSheet<ClassJob>()!
        .Where(cj => cj.RowId != 0)
        .OrderBy(cj =>
        {
          return cj.Role switch
          {
            0 => 3,
            1 => 0,
            2 => 2,
            3 => 2,
            4 => 1,
            _ => 4,
          };
        }).ToList() ?? new List<ClassJob>();

      this.sortedCategoriesAndItems = this.SortCategoriesAndItems(dataManager, log);
      this.categories = this.sortedCategoriesAndItems.Select(kv => kv.Key).ToList();
      this.filtered = this.sortedCategoriesAndItems.ToList();
    }

    /// <summary>
    /// Gets the class jobs offered by the class filter, ordered by role.
    /// </summary>
    public IReadOnlyList<ClassJob> ClassJobs => this.classJobs;

    /// <summary>
    /// Gets every marketable item search category, in the order the market board lists them.
    /// </summary>
    public IReadOnlyList<ItemSearchCategory> Categories => this.categories;

    /// <summary>
    /// Gets the categories and items matching the filter last passed to <see cref="ApplyFilter"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<ItemSearchCategory, List<Item>>> FilteredCategories => this.filtered;

    /// <summary>
    /// Recomputes the filtered categories, but only when the filter differs from the one already applied
    /// or the last pass left unlock states the game could not read.
    /// </summary>
    /// <param name="filter">The criteria an item has to match.</param>
    /// <returns>True when the filtered categories were rebuilt.</returns>
    public bool ApplyFilter(ItemFilter filter)
    {
      ArgumentNullException.ThrowIfNull(filter);

      if (this.lastFilter != null && this.lastFilter.SameAs(filter))
      {
        if (!this.WorthReadingAgain(this.lastFilter))
        {
          return false;
        }
      }
      else
      {
        this.lastUnreadable = int.MaxValue;
      }

      this.filtered = this.sortedCategoriesAndItems
        .Where(kv => filter.IncludesCategory(kv.Key))
        .Select(kv => new KeyValuePair<ItemSearchCategory, List<Item>>(kv.Key, kv.Value.Where(filter.Matches).ToList()))
        .Where(kv => kv.Value.Count > 0)
        .ToList();

      this.lastFilter = filter;

      if (filter.Unlocked != null)
      {
        this.log.Debug(
          $"Collection filter kept {this.filtered.Sum(kv => kv.Value.Count)} items, "
          + $"read {filter.UnlockProbed} unlock states, {filter.UnlockUnreadable} of them unreadable");
      }

      return true;
    }

    /// <summary>
    /// Checks whether an item appears anywhere in the catalogue.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <returns>True when the item is marketable and present in the catalogue.</returns>
    public bool Contains(uint itemId)
    {
      return this.sortedCategoriesAndItems.Any(c => c.Value.Any(i => i.RowId == itemId));
    }

    /// <summary>
    /// Gets an item by its row id.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <returns>The matching item.</returns>
    public Item GetItem(uint itemId)
    {
      return this.items.Single(i => i.RowId == itemId);
    }

    private static string PadNumbers(string input)
    {
      return Regex.Replace(input, "[0-9]+", match => match.Value.PadLeft(10, '0'));
    }

    private static string ConvertItemNameToSortableFormat(string itemName)
    {
      Regex regex = new Regex(@"^[IVX]+$");
      foreach (var word in itemName.Split(' '))
      {
        if (word.Length <= 4 && regex.IsMatch(word))
        {
          int value = 0;
          for (int index = word.Length - 1, lastValue = 0; index >= 0; index--)
          {
            int currentValue = RomanNumberMap[word[index]];
            value += currentValue < lastValue ? -currentValue : currentValue;
            lastValue = currentValue;
          }

          return PadNumbers(itemName.Replace(word, value.ToString(CultureInfo.CurrentCulture), StringComparison.CurrentCulture));
        }
      }

      return itemName;
    }

    /// <summary>
    /// The game pages item rows in as they are asked for, so the first sweep of the catalogue reads back
    /// as unreadable for the rows it has not loaded yet, and those items drop out of the collection filter.
    /// Sweeping again is only worth it while each pass reads more of them than the one before.
    /// </summary>
    /// <param name="applied">The filter already applied.</param>
    /// <returns>True when another pass would find items the last one missed.</returns>
    private bool WorthReadingAgain(ItemFilter applied)
    {
      if (applied.UnlockUnreadable == 0 || applied.UnlockUnreadable >= this.lastUnreadable)
      {
        return false;
      }

      this.lastUnreadable = applied.UnlockUnreadable;

      return true;
    }

    private List<KeyValuePair<ItemSearchCategory, List<Item>>> SortCategoriesAndItems(IDataManager dataManager, IPluginLog log)
    {
      var itemSearchCategories = dataManager.GetExcelSheet<ItemSearchCategory>();

      if (itemSearchCategories == null)
      {
        log.Warning("Failed to load item search categories.");
        return new List<KeyValuePair<ItemSearchCategory, List<Item>>>();
      }

      var itemsByCategory = this.items
        .Where(i => i.ItemSearchCategory.RowId > 0)
        .GroupBy(i => i.ItemSearchCategory.RowId)
        .ToDictionary(g => g.Key, g => g.OrderBy(i => ConvertItemNameToSortableFormat(i.Name.ExtractText())).ToList());

      return itemSearchCategories
        .Where(c => c.Category > 0)
        .OrderBy(c => c.Category)
        .ThenBy(c => c.Order)
        .Select(c => new KeyValuePair<ItemSearchCategory, List<Item>>(
          c,
          itemsByCategory.TryGetValue(c.RowId, out var categoryItems) ? categoryItems : new List<Item>()))
        .ToList();
    }
  }
}
