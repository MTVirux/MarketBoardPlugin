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
    /// Gets every marketable category with its items, sorted the way the market board lists them.
    /// </summary>
    public IReadOnlyList<KeyValuePair<ItemSearchCategory, List<Item>>> SortedCategoriesAndItems =>
      this.sortedCategoriesAndItems;

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
