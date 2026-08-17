// <copyright file="CatalogView.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Dalamud.Plugin.Services;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// One board's view of the shared catalogue: the categories and items its own filter keeps.
  /// </summary>
  /// <remarks>
  /// The sorted catalogue costs too much to build per window, so it stays shared and each board
  /// keeps its filter here. Two boards filtering differently then never rebuild each other's results.
  /// </remarks>
  public sealed class CatalogView
  {
    private readonly ItemCatalog catalog;

    private readonly IPluginLog log;

    private List<KeyValuePair<ItemSearchCategory, List<Item>>> filtered;

    private ItemFilter? lastFilter;

    private int lastUnreadable = int.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogView"/> class.
    /// </summary>
    /// <param name="catalog">The shared item catalogue.</param>
    /// <param name="log">The plugin log.</param>
    public CatalogView(ItemCatalog catalog, IPluginLog log)
    {
      this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
      this.log = log ?? throw new ArgumentNullException(nameof(log));
      this.filtered = this.catalog.SortedCategoriesAndItems.ToList();
    }

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

      this.filtered = this.catalog.SortedCategoriesAndItems
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
  }
}
