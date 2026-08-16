// <copyright file="MarketTerrorConfig.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using Dalamud.Configuration;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// Configuration for MarketTerrorPlugin.
  /// </summary>
  public class MarketTerrorConfig : IPluginConfiguration
  {
    /// <summary>
    /// The number of ms an item is cached for before it is fetched again.
    /// </summary>
    public const int DefaultItemRefreshTimeout = 2000;

    /// <summary>
    /// The version this build writes.
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Gets or sets the version of the config file.
    /// </summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Gets or sets a value indicating whether cross data center was selected.
    /// </summary>
    public bool CrossDataCenter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether cross world was selected.
    /// </summary>
    public bool CrossWorld { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether 'Watch for hovered item' is enabled.
    /// </summary>
    public bool WatchForHovered { get; set; } = true;

    /// <summary>
    /// Gets the list of previously viewed items.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "RemoveAll and RemoveRange required")]
    public List<uint> History { get; } = new List<uint>();

    /// <summary>
    /// Gets or sets a value indicating whether the 'Search with Market Board Plugin' is added to game context menus.
    /// </summary>
    public bool ContextMenuIntegration { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether 'PriceIconShown' is enabled.
    /// </summary>
    public bool PriceIconShown { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether 'NoGilSalesTax' is enabled.
    /// </summary>
    public bool NoGilSalesTax { get; set; }

    /// <summary>
    ///  Gets or sets a value indicating the number of ms an item can be cached.
    /// </summary>
    public int ItemRefreshTimeout { get; set; } = DefaultItemRefreshTimeout;

    /// <summary>
    ///  Gets or sets a value indicating whether the recent history menu is disabled or not.
    /// </summary>
    public bool RecentHistoryDisabled { get; set; }

    /// <summary>
    ///  Gets or sets the share of the Market Data tab given to the current listings, the rest going to recent history.
    /// </summary>
    public float MarketDataSplitRatio { get; set; } = 0.5f;

    /// <summary>
    ///  Gets or sets the unscaled width of the search and item list column.
    /// </summary>
    public float ItemListColumnWidth { get; set; } = 267.0f;

    /// <summary>
    /// Gets the favorite items.
    /// </summary>
    public ICollection<uint> Favorites { get; } = new List<uint>();

    /// <summary>
    /// Gets the saved shopping list, so it survives a plugin reload.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Rewritten wholesale whenever the shopping list changes")]
    public List<StoredItem> ShoppingList { get; } = new List<StoredItem>();

    /// <summary>
    /// Gets or sets the number of listings to retrieve.
    /// </summary>
    public int ListingCount { get; set; } = 50;

    /// <summary>
    /// Gets or sets the number of historical entries to retrieve.
    /// </summary>
    public int HistoryCount { get; set; } = 50;

    /// <summary>
    /// Gets or sets a value indicating whether clipboard notifications are enabled.
    /// </summary>
    public bool ClipboardNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether clicking a listing copies the item name to the clipboard.
    /// </summary>
    public bool CopyItemNameOnListingClick { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether clicking on a listing automatically teleports to that world (requires Lifestream plugin).
    /// </summary>
    public bool AutoTeleportToWorld { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Market Board search is automatically filled and run after auto-teleporting to a listing's world (requires Lifestream plugin).
    /// </summary>
    public bool AutoSearchOnMarketBoard { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the matching result is automatically opened once the Market Board auto-search returns.
    /// </summary>
    public bool AutoOpenSearchResult { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Oceania DC should be included in the Cross-DC filter.
    /// </summary>
    public bool IncludeOceaniaDC { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the main window should open on plugin start in debug builds.
    /// </summary>
    public bool OpenOnStart { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the item that was open last is reselected on plugin start in debug builds.
    /// </summary>
    public bool RememberLastItem { get; set; } = true;

    /// <summary>
    /// Gets or sets the row id of the item that was selected last, or 0 when there is none.
    /// </summary>
    /// <remarks>Only written by debug builds.</remarks>
    public uint LastOpenedItem { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Terror skin is applied to the plugin's windows.
    /// </summary>
    public bool TerrorSkinEnabled { get; set; } = true;

    /// <summary>
    /// Gets the names of the integrations whose warnings the user has dismissed.
    /// </summary>
    /// <remarks>A name is dropped as soon as its integration works again, so a later failure warns afresh.</remarks>
    public ICollection<string> DismissedIntegrationWarnings { get; } = new List<string>();

    /// <summary>
    /// Gets the Terror skin colours that have been changed from their defaults, keyed by colour name.
    /// </summary>
    /// <remarks>Colours left at their default are not stored, so shipped defaults can change freely.</remarks>
    public Dictionary<string, uint> ThemeOverrides { get; } = new Dictionary<string, uint>();
  }
}
