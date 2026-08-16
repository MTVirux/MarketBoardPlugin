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
  using MarketTerror.Extensions;

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

    private readonly Dictionary<ItemSearchCategory, List<Item>> sortedCategoriesAndItems;

    private readonly List<ClassJob> classJobs;

    private List<KeyValuePair<ItemSearchCategory, List<Item>>> filtered;

    private string lastSearchString = string.Empty;

    private int lastItemCategory;

    private int lastMinLevel;

    private int lastMaxLevel = 100;

    private ClassJob? lastClassJob;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemCatalog"/> class.
    /// </summary>
    /// <param name="dataManager">The data manager the game sheets are read from.</param>
    /// <param name="log">The plugin log.</param>
    public ItemCatalog(IDataManager dataManager, IPluginLog log)
    {
      ArgumentNullException.ThrowIfNull(dataManager);
      ArgumentNullException.ThrowIfNull(log);

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
      this.filtered = this.sortedCategoriesAndItems
        .Select(kv => new KeyValuePair<ItemSearchCategory, List<Item>>(kv.Key, kv.Value))
        .ToList();
    }

    /// <summary>
    /// Gets the class jobs offered by the class filter, ordered by role.
    /// </summary>
    public IReadOnlyList<ClassJob> ClassJobs => this.classJobs;

    /// <summary>
    /// Gets the categories and items matching the filter last passed to <see cref="ApplyFilter"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<ItemSearchCategory, List<Item>>> FilteredCategories => this.filtered;

    /// <summary>
    /// Recomputes the filtered categories, but only when the filter differs from the one already applied.
    /// </summary>
    /// <param name="searchString">The item name fragment to search for.</param>
    /// <param name="itemCategory">The top level category index, where 0 means all categories.</param>
    /// <param name="minLevel">The minimum equip level.</param>
    /// <param name="maxLevel">The maximum equip level.</param>
    /// <param name="classJob">The class job to filter by, or null for all classes.</param>
    public void ApplyFilter(string searchString, int itemCategory, int minLevel, int maxLevel, ClassJob? classJob)
    {
      if (searchString == this.lastSearchString
        && itemCategory == this.lastItemCategory
        && minLevel == this.lastMinLevel
        && maxLevel == this.lastMaxLevel
        && classJob?.RowId == this.lastClassJob?.RowId)
      {
        return;
      }

      var categories = this.sortedCategoriesAndItems
        .Where(c => itemCategory == 0 || (itemCategory > 0 && c.Key.Category == itemCategory));

      if (!string.IsNullOrEmpty(searchString))
      {
        this.filtered = categories
          .Select(kv => new KeyValuePair<ItemSearchCategory, List<Item>>(
            kv.Key,
            kv.Value
              .Where(i =>
                i.Name.ExtractText().ToUpperInvariant().Contains(searchString.ToUpperInvariant(), StringComparison.InvariantCulture))
              .Where(i => i.LevelEquip >= minLevel && i.LevelEquip <= maxLevel)
              .Where(i => i.ClassJobCategory.Value.HasClass(classJob))
              .ToList()))
          .Where(kv => kv.Value.Count > 0)
          .ToList();
      }
      else
      {
        this.filtered = categories
          .Select(kv => new KeyValuePair<ItemSearchCategory, List<Item>>(
            kv.Key,
            kv.Value
              .Where(i => i.LevelEquip >= minLevel && i.LevelEquip <= maxLevel)
              .Where(i => i.ClassJobCategory.Value.HasClass(classJob))
              .ToList()))
          .Where(kv => kv.Value.Count > 0)
          .ToList();
      }

      this.lastSearchString = searchString;
      this.lastItemCategory = itemCategory;
      this.lastClassJob = classJob;
      this.lastMinLevel = minLevel;
      this.lastMaxLevel = maxLevel;
    }

    /// <summary>
    /// Checks whether an item appears anywhere in the catalogue.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <returns>True when the item is marketable and present in the catalogue.</returns>
    public bool Contains(uint itemId)
    {
      return this.sortedCategoriesAndItems.Any(i => i.Value != null && i.Value.Any(k => k.RowId == itemId));
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

    private Dictionary<ItemSearchCategory, List<Item>> SortCategoriesAndItems(IDataManager dataManager, IPluginLog log)
    {
      var itemSearchCategories = dataManager.GetExcelSheet<ItemSearchCategory>();

      if (itemSearchCategories == null)
      {
        log.Warning("Failed to load item search categories.");
        return new Dictionary<ItemSearchCategory, List<Item>>();
      }

      var sortedCategories = itemSearchCategories.Where(c => c.Category > 0).OrderBy(c => c.Category).ThenBy(c => c.Order);

      var sortedCategoriesDict = new Dictionary<ItemSearchCategory, List<Item>>();

      foreach (var c in sortedCategories)
      {
        if (sortedCategoriesDict.ContainsKey(c))
        {
          continue;
        }

        sortedCategoriesDict.Add(c, this.items.Where(i => i.ItemSearchCategory.RowId == c.RowId).OrderBy(i => ConvertItemNameToSortableFormat(i.Name.ExtractText())).ToList());
      }

      return sortedCategoriesDict;
    }
  }
}
