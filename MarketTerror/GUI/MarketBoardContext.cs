// <copyright file="MarketBoardContext.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface.ManagedFontAtlas;
  using Lumina.Excel.Sheets;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;
  using MarketTerror.Services;

  /// <summary>
  /// The state and services shared by every component of the market board window.
  /// </summary>
  /// <remarks>
  /// Components talk to each other only through this object; none of them holds a reference to another.
  /// </remarks>
  public sealed class MarketBoardContext
  {
    /// <summary>
    /// The equip level the maximum equip level filter starts at and resets to.
    /// </summary>
    public const int DefaultMaxLevel = 100;

    /// <summary>
    /// The item level the maximum item level filter starts at and resets to.
    /// </summary>
    public const int DefaultMaxItemLevel = 999;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardContext"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="theme">The Terror skin.</param>
    /// <param name="catalog">The item catalogue.</param>
    /// <param name="marketData">The market data provider.</param>
    /// <param name="worlds">The world selection.</param>
    /// <param name="titleFont">The 1.5x font used for headings.</param>
    public MarketBoardContext(
      MarketTerrorPlugin plugin,
      TerrorTheme theme,
      ItemCatalog catalog,
      MarketDataProvider marketData,
      WorldSelection worlds,
      IFontHandle titleFont)
    {
      this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.Theme = theme ?? throw new ArgumentNullException(nameof(theme));
      this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
      this.MarketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
      this.Worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
      this.TitleFont = titleFont ?? throw new ArgumentNullException(nameof(titleFont));
      this.Integrations = new IntegrationStatus(this.Plugin, this.MarketData);
    }

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public MarketTerrorPlugin Plugin { get; }

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public MarketTerrorConfig Config => this.Plugin.Config;

    /// <summary>
    /// Gets the Terror skin.
    /// </summary>
    public TerrorTheme Theme { get; }

    /// <summary>
    /// Gets the item catalogue.
    /// </summary>
    public ItemCatalog Catalog { get; }

    /// <summary>
    /// Gets the market data provider.
    /// </summary>
    public MarketDataProvider MarketData { get; }

    /// <summary>
    /// Gets the world selection.
    /// </summary>
    public WorldSelection Worlds { get; }

    /// <summary>
    /// Gets the state of the optional plugins and services, and their dismissed warnings.
    /// </summary>
    public IntegrationStatus Integrations { get; }

    /// <summary>
    /// Gets the 1.5x font used for headings.
    /// </summary>
    public IFontHandle TitleFont { get; }

    /// <summary>
    /// Gets or sets the current search string.
    /// </summary>
    public string SearchString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected item.
    /// </summary>
    public Item? SelectedItem { get; set; }

    /// <summary>
    /// Gets or sets the class job filter, or null for all classes.
    /// </summary>
    public ClassJob? SelectedClassJob { get; set; }

    /// <summary>
    /// Gets the item search categories the item list is limited to, or an empty set for every category.
    /// </summary>
    public HashSet<uint> SelectedCategories { get; } = new HashSet<uint>();

    /// <summary>
    /// Gets the item rarities the item list is limited to, or an empty set for every rarity.
    /// </summary>
    public HashSet<byte> SelectedRarities { get; } = new HashSet<byte>();

    /// <summary>
    /// Gets or sets the minimum equip level filter.
    /// </summary>
    public int MinLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum equip level filter.
    /// </summary>
    public int MaxLevel { get; set; } = DefaultMaxLevel;

    /// <summary>
    /// Gets or sets the minimum item level filter.
    /// </summary>
    public int MinItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum item level filter.
    /// </summary>
    public int MaxItemLevel { get; set; } = DefaultMaxItemLevel;

    /// <summary>
    /// Gets or sets the unlock state the item list is limited to, or null for every item.
    /// </summary>
    public bool? UnlockFilter { get; set; }

    /// <summary>
    /// Gets a value indicating whether the unlock state can be read for the current character.
    /// </summary>
    public bool CanReadUnlockState => this.Plugin.ClientState.IsLoggedIn;

    /// <summary>
    /// Gets or sets the index of the highlighted listing.
    /// </summary>
    public int SelectedListing { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the highlighted history entry.
    /// </summary>
    public int SelectedHistory { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the open stats section, or -1 when they are all closed.
    /// </summary>
    public int OpenStatsSection { get; set; } = -1;

    /// <summary>
    /// Gets or sets the list shown by the item panel.
    /// </summary>
    public ItemListTab ItemListTab { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the advanced search options are shown.
    /// </summary>
    public bool AdvancedSearchOpen { get; set; }

    /// <summary>
    /// Gets a value indicating whether any advanced search filter is set to something other than its default.
    /// </summary>
    public bool HasActiveFilters =>
      this.SelectedCategories.Count > 0
      || this.SelectedRarities.Count > 0
      || this.SelectedClassJob != null
      || (this.CanReadUnlockState && this.UnlockFilter != null)
      || this.MinLevel != 0
      || this.MaxLevel != DefaultMaxLevel
      || this.MinItemLevel != 0
      || this.MaxItemLevel != DefaultMaxItemLevel;

    /// <summary>
    /// Builds the item filter matching the current advanced search settings.
    /// </summary>
    /// <param name="searchString">The item name fragment to search for.</param>
    /// <returns>The filter to hand to the catalogue.</returns>
    public ItemFilter BuildFilter(string searchString)
    {
      return new ItemFilter(
        searchString,
        this.SelectedCategories,
        this.SelectedRarities,
        this.MinLevel,
        this.MaxLevel,
        this.MinItemLevel,
        this.MaxItemLevel,
        this.SelectedClassJob,
        this.CanReadUnlockState ? this.UnlockFilter : null,
        id => ItemUnlock.IsUnlocked(this.Plugin.ClientState, id));
    }

    /// <summary>
    /// Resets every advanced search filter to its default.
    /// </summary>
    public void ResetFilters()
    {
      this.SelectedCategories.Clear();
      this.SelectedRarities.Clear();
      this.SelectedClassJob = null;
      this.UnlockFilter = null;
      this.MinLevel = 0;
      this.MaxLevel = DefaultMaxLevel;
      this.MinItemLevel = 0;
      this.MaxItemLevel = DefaultMaxItemLevel;
    }

    /// <summary>
    /// Selects an item, refreshes its market data and records it in the search history.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <param name="noHistory">True to leave the search history untouched.</param>
    public void SelectItem(uint itemId, bool noHistory = false)
    {
      this.SelectedItem = this.Catalog.GetItem(itemId);

      this.RefreshMarketData();

      var configChanged = false;

      if (!noHistory)
      {
        this.Config.History.RemoveAll(i => i == itemId);
        this.Config.History.Insert(0, itemId);
        if (this.Config.History.Count > 100)
        {
          this.Config.History.RemoveRange(100, this.Config.History.Count - 100);
        }

        configChanged = true;
      }

#if DEBUG
      if (this.Config.LastOpenedItem != itemId)
      {
        this.Config.LastOpenedItem = itemId;
        configChanged = true;
      }
#endif

      if (configChanged)
      {
        this.Plugin.PluginInterface.SavePluginConfig(this.Config);
      }
    }

    /// <summary>
    /// Drops the cached market data and refetches the selected item.
    /// </summary>
    public void ResetMarketData()
    {
      this.MarketData.ClearCache();
      this.RefreshMarketData();
    }

    /// <summary>
    /// Refetches the market data for the selected item, if there is one.
    /// </summary>
    public void RefreshMarketData()
    {
      if (!this.SelectedItem.HasValue)
      {
        return;
      }

      this.MarketData.Refresh(this.SelectedItem.Value, this.Worlds.QueryTarget, this.Worlds.IncludesOceania);
    }

    /// <summary>
    /// Adds the cheapest current listing of an item to the shopping list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="fallbackToSelectedWorld">
    /// True to fall back to the selected world's name when the listing carries none, false to fall back to an empty string.
    /// </param>
    public void TryAddCheapestToShoppingList(Item item, bool fallbackToSelectedWorld)
    {
      if (!this.Worlds.HasSelection)
      {
        return;
      }

      var entry = SavedItem.FromCheapestListing(
        item,
        this.MarketData.MarketData,
        !this.Config.NoGilSalesTax,
        fallbackToSelectedWorld ? this.Worlds.QueryTarget : string.Empty);

      if (entry != null)
      {
        this.Plugin.ShoppingList.Add(entry);
      }
    }

    /// <summary>
    /// Goes to a world's Market Board and, when enabled, searches an item on the board that ends up open.
    /// </summary>
    /// <param name="worldName">The world to go to, or an empty string when it is unknown.</param>
    /// <param name="item">The item to search for, or null to only travel.</param>
    /// <param name="allowTravel">False to stay put and only fill a board that is already open.</param>
    public void GoToMarketBoard(string worldName, Item? item, bool allowTravel)
    {
      this.GoToMarketBoard(worldName, item, allowTravel, false, null);
    }

    /// <summary>
    /// Goes to a world's Market Board and opens an item's listings so they can be bought.
    /// </summary>
    /// <param name="worldName">The world to go to.</param>
    /// <param name="item">The item whose listings are wanted.</param>
    /// <param name="onSearchFinished">Called with true once the listings are open, false when they never opened.</param>
    /// <remarks>The search and the result opening are forced on, since a buy is useless without the listings.</remarks>
    public void GoToMarketBoardForBuy(string worldName, Item item, Action<bool> onSearchFinished)
    {
      ArgumentNullException.ThrowIfNull(onSearchFinished);

      this.GoToMarketBoard(worldName, item, true, true, onSearchFinished);
    }

    /// <summary>
    /// Copies text to the clipboard and, when enabled, announces it in chat.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    public void CopyToClipboard(string text)
    {
      ImGui.LogToClipboard();
      ImGui.LogText(text);
      ImGui.LogFinish();

      try
      {
        if (this.Config.ClipboardNotificationsEnabled)
        {
          this.Plugin.NotifyClipboardCopied(text);
        }
      }
      catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
      {
        this.Plugin.Log.Warning($"Failed to notify clipboard copied: {ex.Message}");
      }
    }

    /// <summary>
    /// Travels to a world's Market Board and hands the item over to the auto-search.
    /// </summary>
    /// <param name="worldName">The world to go to, or an empty string when it is unknown.</param>
    /// <param name="item">The item to search for, or null to only travel.</param>
    /// <param name="allowTravel">False to stay put and only fill a board that is already open.</param>
    /// <param name="force">True to search and open the result regardless of the auto-search settings.</param>
    /// <param name="onFinished">Called exactly once with how the auto-search ended, or null when nobody is waiting.</param>
    private void GoToMarketBoard(string worldName, Item? item, bool allowTravel, bool force, Action<bool>? onFinished)
    {
      var plugin = this.Plugin;

      var travelEnabled = allowTravel && !string.IsNullOrEmpty(worldName);
      var sameWorld = plugin.PlayerState.IsLoaded
        && string.Equals(worldName, plugin.PlayerState.CurrentWorld.Value.Name.ExtractText(), StringComparison.OrdinalIgnoreCase);

      // Skip travel when we're already at a Market Board in that world.
      var alreadyAtMarketBoard = sameWorld && plugin.GameGui.GetAddonByName("ItemSearch") != nint.Zero;

      // Right world already, and a board within reach: open that one instead of travelling.
      var openedLocalBoard = travelEnabled && sameWorld && !alreadyAtMarketBoard
        && MarketBoardInteraction.TryInteractWithNearbyBoard(plugin.ObjectTable, plugin.DataManager, plugin.Log);

      plugin.Log.Debug($"Market board travel: world \"{worldName}\", sameWorld {sameWorld}, boardOpen {alreadyAtMarketBoard}, usedLocalBoard {openedLocalBoard}");

      var traveled = false;

      if (travelEnabled && !alreadyAtMarketBoard && !openedLocalBoard)
      {
        plugin.CommandManager.ProcessCommand($"/li {worldName} mb");
        traveled = true;
      }

      // Auto-search: queue for after the travel, or fill the open Market Board immediately.
      if ((force || this.Config.AutoSearchOnMarketBoard) && item.HasValue)
      {
        var autoSearchName = item.Value.Name.ExtractText();
        var autoSearchId = item.Value.RowId;

        if (traveled && plugin.IsLifestreamAvailable)
        {
          plugin.AutoSearch.Arm(autoSearchName, autoSearchId, onFinished, force);
        }
        else if (openedLocalBoard)
        {
          plugin.AutoSearch.ArmForLocalBoard(autoSearchName, autoSearchId, onFinished, force);
        }
        else
        {
          plugin.AutoSearch.TryFillNow(autoSearchName, autoSearchId, onFinished, force);
        }

        return;
      }

      onFinished?.Invoke(false);
    }
  }
}
