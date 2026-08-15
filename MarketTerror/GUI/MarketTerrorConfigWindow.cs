// <copyright file="MarketTerrorConfigWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface.Windowing;
  using MarketTerror.Helpers;

  /// <summary>
  /// The market board config window.
  /// </summary>
  public class MarketTerrorConfigWindow : Window
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="MarketTerrorConfigWindow"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public MarketTerrorConfigWindow(MarketTerrorPlugin plugin)
      : base("Market Terror Config")
    {
      this.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize;
      this.Size = new Vector2(0, 0);

      this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    private MarketTerrorPlugin Plugin { get; init; }

    /// <inheritdoc/>
    public override void Draw()
    {
      // General
      ImGui.Text("General");
      ImGui.Separator();
      this.Checkbox("Context menu integration", "Toggles whether context menu integration is enabled", this.Plugin.Config.ContextMenuIntegration, (v) => this.Plugin.Config.ContextMenuIntegration = v);
      this.Checkbox("Gil Icon Shown", "Toggles whether the Gil icon is shown", this.Plugin.Config.PriceIconShown, (v) => this.Plugin.Config.PriceIconShown = v);

      // Pricing / behavior
      this.Checkbox("No Gil Sales Tax", "Toggles whether the Gil Sales Tax is included", this.Plugin.Config.NoGilSalesTax, (v) =>
      {
        this.Plugin.Config.NoGilSalesTax = v;
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
        this.Plugin.ResetMarketData();
      });

      ImGui.NewLine();

      // History
      ImGui.Text("History");
      ImGui.Separator();
      this.Checkbox("Disable Recent History", "Toggles whether the recent history is disabled", this.Plugin.Config.RecentHistoryDisabled, (v) => this.Plugin.Config.RecentHistoryDisabled = v);
      this.Checkbox("Watch for hovered item", "Automatically select the item hovered in any of the in-game inventory window after 1 second.", this.Plugin.Config.WatchForHovered, (v) => this.Plugin.Config.WatchForHovered = v);

      ImGui.NewLine();

      // Teleport / integration
      ImGui.Text("Teleport / Integration");
      ImGui.Separator();

      // Auto-teleport to world setting (only enabled if Lifestream is installed)
      var lifestreamInstalled = this.Plugin.IsLifestreamInstalled;
      if (!lifestreamInstalled)
      {
        ImGui.BeginDisabled();
      }

      this.Checkbox("Auto-teleport to world", lifestreamInstalled ? "Automatically teleport to the listing's world when clicked (requires Lifestream plugin)" : "Automatically teleport to the listing's world when clicked (Lifestream plugin not installed)", this.Plugin.Config.AutoTeleportToWorld, (v) => this.Plugin.Config.AutoTeleportToWorld = v);

      if (!lifestreamInstalled)
      {
        ImGui.EndDisabled();
      }

      this.Checkbox("Auto-search item on Market Board", "When clicking a listing, automatically type the item's name into the Market Board search when applicable.", this.Plugin.Config.AutoSearchOnMarketBoard, (v) => this.Plugin.Config.AutoSearchOnMarketBoard = v);

      ImGui.Indent();
      if (!this.Plugin.Config.AutoSearchOnMarketBoard)
      {
        ImGui.BeginDisabled();
      }

      this.Checkbox("Open the matching result", "Once the auto-search returns, select the searched item in the results so its listings open.", this.Plugin.Config.AutoOpenSearchResult, (v) => this.Plugin.Config.AutoOpenSearchResult = v);

      if (!this.Plugin.Config.AutoSearchOnMarketBoard)
      {
        ImGui.EndDisabled();
      }

      ImGui.Unindent();

      ImGui.NewLine();

      // Clipboard
      ImGui.Text("Clipboard");
      ImGui.Separator();
      this.Checkbox("Clipboard notifications", "Show a chat message when something is copied to the clipboard", this.Plugin.Config.ClipboardNotificationsEnabled, (v) => this.Plugin.Config.ClipboardNotificationsEnabled = v);
      this.Checkbox("Copy item name on listing click", "Copy the selected item's name to the clipboard when clicking a current listing", this.Plugin.Config.CopyItemNameOnListingClick, (v) => this.Plugin.Config.CopyItemNameOnListingClick = v);

      ImGui.NewLine();

      // Appearance
      ImGui.Text("Others");
      ImGui.Separator();
      this.Checkbox("Hide SeaOfTerror Repo button", "Toggles whether the SeaOfTerror Repo button should be hidden", this.Plugin.Config.KofiHidden, (v) => this.Plugin.Config.KofiHidden = v);

      this.Checkbox("Include Oceania DC", "Toggles whether the Oceania DC should be included in the Cross-DC filter", this.Plugin.Config.IncludeOceaniaDC, (v) =>
      {
        this.Plugin.Config.IncludeOceaniaDC = v;
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
        this.Plugin.ResetMarketData();
      });

#if DEBUG
      ImGui.NewLine();

      // Debug
      ImGui.Text("Debug");
      ImGui.Separator();
      this.Checkbox("Open window on start", "Toggles whether the main window opens automatically on plugin start in debug builds", this.Plugin.Config.OpenOnStart, (v) => this.Plugin.Config.OpenOnStart = v);
#endif

      var itemRefreshTimeout = this.Plugin.Config.ItemRefreshTimeout;
      ImGui.Text("Item buffer Timeout (ms) :");
      ImGui.InputInt("###refreshTimeout", ref itemRefreshTimeout);
      if (this.Plugin.Config.ItemRefreshTimeout != itemRefreshTimeout)
      {
        this.Plugin.Config.ItemRefreshTimeout = itemRefreshTimeout;
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }

      var listingCount = this.Plugin.Config.ListingCount;
      ImGui.Text("Listing count :");
      ImGui.InputInt("###listingCount", ref listingCount);
      if (this.Plugin.Config.ListingCount != listingCount)
      {
        this.Plugin.Config.ListingCount = listingCount;
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }

      var historyCount = this.Plugin.Config.HistoryCount;
      ImGui.Text("History count :");
      ImGui.InputInt("###historyCount", ref historyCount);
      if (this.Plugin.Config.HistoryCount != historyCount)
      {
        this.Plugin.Config.HistoryCount = historyCount;
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }
    }

    private void Checkbox(string label, string description, bool oldValue, Action<bool> setter)
    {
      if (Utilities.Checkbox(label, description, oldValue, setter))
      {
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }
    }
  }
}
