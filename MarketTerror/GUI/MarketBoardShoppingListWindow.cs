// <copyright file="MarketBoardShoppingListWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Numerics;
  using System.Text;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Game.Text;
  using Dalamud.Interface;
  using Dalamud.Interface.Textures;
  using Dalamud.Interface.Windowing;
  using MarketTerror.Extensions;
  using MarketTerror.GUI.Components;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;
  using MarketTerror.Services;

  /// <summary>
  /// The market board config window.
  /// </summary>
  public class MarketBoardShoppingListWindow : Window, IDisposable
  {
    /// <summary>
    /// What a row shows in place of a price or a world it does not have.
    /// </summary>
    private const string NoValue = "-";

    /// <summary>
    /// What marks a row that was added straight from a listing.
    /// </summary>
    private const string DirectPrefix = "[Direct Listing]";

    /// <summary>
    /// The unscaled width of one of the icon buttons in the action column.
    /// </summary>
    private const float ActionButtonWidth = 32;

    /// <summary>
    /// How many icon buttons a row can show.
    /// </summary>
    private const int ActionButtonCount = 5;

    private const string HqHeader = "HQ";

    private const string PriceHeader = "Price";

    private const string QtyHeader = "Qty";

    private const string TotalHeader = "Total";

    private const string WorldHeader = "World";

    // Not resizable on purpose: that is what keeps every fixed column fitted to its content each frame.
    private const ImGuiTableFlags TableFlags =
      ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit |
      ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate;

    /// <summary>
    /// The unscaled size the window will not go below.
    /// </summary>
    private static readonly Vector2 MinWindowSize = new Vector2(560, 150);

    /// <summary>
    /// What the high quality column draws for a row that only buys high quality.
    /// </summary>
    private static readonly string HqMark = SeIconChar.HighQuality.AsString();

    private readonly TerrorTheme theme;

    private readonly WorldPicker worldPicker = new WorldPicker("shoppingListWorld");

    private readonly List<SavedItem> sortedItems = new List<SavedItem>();

    private readonly ListingPicker picker;

    private IDisposable? themeScope;

    private bool isDisposed;

    private bool forceShown;

    private bool hidden;

    private int lastCount;

    private int sortColumn = -1;

    private int sortedRevision = -1;

    private bool sortAscending = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardShoppingListWindow"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public MarketBoardShoppingListWindow(MarketTerrorPlugin plugin)
      : base("Market Terror Shopping List")
    {
      this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      this.Flags = ImGuiWindowFlags.NoScrollbar;
      this.IsOpen = true;
      this.RespectCloseHotkey = false;
      this.ShowCloseButton = true;
      this.Size = MinWindowSize;
      this.SizeCondition = ImGuiCond.FirstUseEver;

      this.theme = new TerrorTheme(this.Plugin.Config);
      this.picker = new ListingPicker(this.Plugin);
    }

    /// <summary>
    /// Gets a value indicating whether the window is currently on screen.
    /// </summary>
    public bool IsShown =>
      !this.hidden
      && (this.Plugin.ShoppingList.Count > 0 || this.forceShown || this.Plugin.ShoppingListBulkAdd.IsRunning || this.Plugin.ShoppingListBuyer.IsRunning);

    private MarketTerrorPlugin Plugin { get; init; }

    /// <summary>
    /// Shows the window, or hides it when it is already shown.
    /// </summary>
    /// <remarks>The window is always open; what it draws is decided by <see cref="DrawConditions"/>.</remarks>
    public void ToggleShown()
    {
      this.hidden = this.IsShown;
      this.forceShown = !this.hidden;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public override void OnClose()
    {
      this.hidden = true;
      this.forceShown = false;

      // The window stays open so a newly added item can bring it back.
      this.IsOpen = true;
    }

    /// <inheritdoc/>
    public override void PreDraw()
    {
      // The action buttons grow with the font scale, so the width the window may not go below grows with it too.
      var scale = ImGui.GetIO().FontGlobalScale;

      this.SizeConstraints = new WindowSizeConstraints
      {
        MinimumSize = MinWindowSize * scale,
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
      };

      this.themeScope = this.theme.Push();
    }

    /// <inheritdoc/>
    public override void PostDraw()
    {
      this.themeScope?.Dispose();
      this.themeScope = null;
    }

    /// <inheritdoc/>
    public override bool DrawConditions()
    {
      var count = this.Plugin.ShoppingList.Count;

      // A newly added item brings the window back even after it was hidden.
      if (count > this.lastCount)
      {
        this.hidden = false;
      }

      this.lastCount = count;

      return this.IsShown;
    }

    /// <inheritdoc/>
    public override void Draw()
    {
      Utilities.RainbowText("EXPERIMENTAL");
      ImGui.SameLine(0, 0);
      ImGui.TextDisabled(" - use at your own risk");

      this.DrawBulkAddProgress();
      this.DrawBuyProgress();

      if (this.Plugin.ShoppingList.Count == 0)
      {
        this.DrawScopePickers(false);
        ImGui.Separator();

        if (!this.Plugin.ShoppingListBulkAdd.IsRunning)
        {
          ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
          ImGui.TextWrapped("Your buy list is empty. Add items from the item list right click menu.");
          ImGui.PopStyleColor();
        }

        return;
      }

      this.DrawActionBar();

      // Drawn before the table so a frame the table cannot open does not take the popup down with it.
      this.picker.Draw(this.theme);

      var bought = this.Plugin.ShoppingList.Count(WasBought);
      var failed = this.Plugin.ShoppingList.Count(BuyFailed);

      // The footer keeps its own row pinned under the table, so it stays put while the list scrolls.
      var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;

      if (bought + failed > 0)
      {
        footerHeight += ImGui.GetFrameHeightWithSpacing();
      }

      if (!ImGui.BeginTable("shoppingList", 7, TableFlags | ImGuiTableFlags.ScrollY, new Vector2(0, -footerHeight)))
      {
        return;
      }

      ImGui.TableSetupColumn(HqHeader, ImGuiTableColumnFlags.WidthFixed);
      ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn(PriceHeader, ImGuiTableColumnFlags.WidthFixed);
      ImGui.TableSetupColumn(QtyHeader, ImGuiTableColumnFlags.WidthFixed);
      ImGui.TableSetupColumn(TotalHeader, ImGuiTableColumnFlags.WidthFixed);
      ImGui.TableSetupColumn(WorldHeader, ImGuiTableColumnFlags.WidthFixed);
      ImGui.TableSetupColumn(
        "Action",
        ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed,
        ActionColumnWidth());
      ImGui.TableSetupScrollFreeze(0, 1);

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.TableHeadersRow();
      ImGui.PopStyleColor();

      this.UpdateSort();

      var hqWidth = ColumnWidth(HqHeader, new[] { HqMark });
      var priceWidth = ColumnWidth(PriceHeader, this.sortedItems.Select(this.PriceText));
      var qtyWidth = ColumnWidth(QtyHeader, this.sortedItems.Select(QtyText));
      var totalWidth = ColumnWidth(TotalHeader, this.sortedItems.Select(this.TotalText));
      var worldWidth = ColumnWidth(WorldHeader, this.sortedItems.Select(WorldText));

      List<SavedItem> todel = new List<SavedItem>();
      SavedItem? convert = null;

      int k = 0;
      foreach (var item in this.sortedItems)
      {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);

        if (item.Hq)
        {
          ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextBright);
          Centered(HqMark, hqWidth);
          ImGui.PopStyleColor();
          Utilities.HoverTooltip("This row only buys high quality.");
        }

        ImGui.TableSetColumnIndex(1);

        this.DrawItemIcon(item);

        var nameColor = item.Outcome switch
        {
          BuyOutcome.Bought => this.theme.BuySuccess,
          BuyOutcome.BoughtCheaper => this.theme.BuyBargain,

          // A row that only partly bought still wants looking at, so it reads like one that did not.
          BuyOutcome.Failed or BuyOutcome.PartlyBought => this.theme.BuyFailed,
          _ => item.Unlisted ? this.theme.TextDim : this.theme.Text,
        };

        var name = item.SourceItem.Name.ExtractText();

        ImGui.PushStyleColor(ImGuiCol.Text, nameColor);
        ImGui.Text(item.IsDirect ? $"{DirectPrefix} {name}" : name);
        ImGui.PopStyleColor();

        if (ImGui.BeginPopupContextItem($"shoplistName{k}"))
        {
          if (ImGui.Selectable("Copy name to clipboard"))
          {
            this.Plugin.MarketBoardContext.CopyToClipboard(name);
          }

          if (item.IsDirect && ImGui.Selectable("Convert to item listing"))
          {
            convert = item;
          }

          this.Plugin.MarketBoardContext.DrawListsMenu(item.SourceItem.RowId);

          ImGui.EndPopup();
        }

        ImGui.TableSetColumnIndex(2);
        ImGui.PushStyleColor(ImGuiCol.Text, item.Refreshing || item.Unlisted ? this.theme.TextDim : this.theme.GilText);
        RightAligned(this.PriceText(item), priceWidth);
        ImGui.PopStyleColor();
        this.PickTooltip(item);

        ImGui.TableSetColumnIndex(3);
        ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
        RightAligned(QtyText(item), qtyWidth);
        ImGui.PopStyleColor();
        this.PickTooltip(item);

        ImGui.TableSetColumnIndex(4);
        ImGui.PushStyleColor(ImGuiCol.Text, item.Refreshing || item.Unlisted ? this.theme.TextDim : this.theme.GilText);
        RightAligned(this.TotalText(item), totalWidth);
        ImGui.PopStyleColor();
        this.PickTooltip(item);

        ImGui.TableSetColumnIndex(5);
        RightAligned(WorldText(item), worldWidth);
        this.PickTooltip(item);

        var buttonSize = new Vector2(ActionButtonWidth * ImGui.GetIO().FontGlobalScale, 1.5f * ImGui.GetItemRectSize().Y);

        ImGui.TableSetColumnIndex(6);

        var idle = !this.Plugin.ShoppingListBuyer.IsRunning && !this.Plugin.ShoppingListBulkAdd.IsRunning;

        ImGui.BeginDisabled(item.IsDirect || !idle || !this.Plugin.ShoppingListScope.HasSelection);
        ImGui.PushFont(UiBuilder.IconFont);
        var refresh = ImGui.Button($"{(char)FontAwesomeIcon.SyncAlt}##shoplistrefresh" + k, buttonSize);
        ImGui.PopFont();
        ImGui.EndDisabled();
        Utilities.HoverTooltip(
          item.IsDirect ? "This is a direct listing and can't be refreshed" : "Price this item again.",
          ImGuiHoveredFlags.AllowWhenDisabled);

        ImGui.SameLine();

        var canBuy = this.Plugin.ShoppingListBuyer.CanBuy(item, out var buyBlockedReason) && idle;

        ImGui.BeginDisabled(!canBuy);
        ImGui.PushFont(UiBuilder.IconFont);
        var buy = ImGui.Button($"{(char)FontAwesomeIcon.ShoppingCart}##shoplistbuy" + k, buttonSize);
        ImGui.PopFont();
        ImGui.EndDisabled();
        Utilities.HoverTooltip(
          buyBlockedReason.Length > 0
            ? buyBlockedReason
            : this.BuyTooltip(item),
          ImGuiHoveredFlags.AllowWhenDisabled);

        ImGui.SameLine();

        // Picks are kept honest by the scope refresh, which a direct listing opts out of.
        ImGui.BeginDisabled(item.IsDirect || !idle || !this.Plugin.ShoppingListScope.HasSelection);
        ImGui.PushStyleColor(ImGuiCol.Text, item.HasPicks ? this.theme.BuyBargain : this.theme.Text);
        ImGui.PushFont(UiBuilder.IconFont);
        var pick = ImGui.Button($"{(char)FontAwesomeIcon.ListUl}##shoplistpick" + k, buttonSize);
        ImGui.PopFont();
        ImGui.PopStyleColor();
        ImGui.EndDisabled();
        Utilities.HoverTooltip(
          this.PickTooltipText(item),
          ImGuiHoveredFlags.AllowWhenDisabled);

        ImGui.SameLine();

        var travel = false;

        // Nothing to travel to when the scope had no listings, but the gap keeps the bin where it was.
        if (item.Unlisted)
        {
          ImGui.Dummy(buttonSize);
        }
        else
        {
          ImGui.BeginDisabled(string.IsNullOrEmpty(item.World));
          ImGui.PushFont(UiBuilder.IconFont);
          travel = ImGui.Button($"{(char)FontAwesomeIcon.Walking}##shoplistgo" + k, buttonSize);
          ImGui.PopFont();
          ImGui.EndDisabled();
          Utilities.HoverTooltip($"Go to the market board on {item.World}.");
        }

        ImGui.SameLine();

        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{(char)FontAwesomeIcon.TrashAlt}##shoplist" + k, buttonSize))
        {
          todel.Add(item);
        }

        ImGui.PopFont();
        Utilities.HoverTooltip("Remove from the list.");

        if (travel)
        {
          this.Plugin.MarketBoardContext.GoToMarketBoard(item.World, item.SourceItem, true);
        }

        if (buy)
        {
          this.Plugin.ShoppingListBuyer.BuyOne(item);
        }

        if (pick)
        {
          this.picker.Open(item);
        }

        if (refresh)
        {
          this.Plugin.ShoppingListBulkAdd.StartRefresh(
            new[] { item.SourceItem },
            this.Plugin.ShoppingListScope.QueryTargets,
            item.SourceItem.Name.ExtractText());
        }

        k += 1;
      }

      ImGui.EndTable();

      ImGui.Separator();

      if (bought + failed > 0)
      {
        this.DrawOutcomeBar(bought, failed);
      }

      this.DrawFooter();

      foreach (var item in todel)
      {
        this.Plugin.ShoppingList.Remove(item);
      }

      if (convert != null)
      {
        this.Plugin.ShoppingList.ConvertToItemListing(convert);
        this.Plugin.ShoppingListBulkAdd.StartRefresh(
          new[] { convert.SourceItem },
          this.Plugin.ShoppingListScope.QueryTargets,
          convert.SourceItem.Name.ExtractText());
      }
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">A value indicating whether we are disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
      if (this.isDisposed)
      {
        return;
      }

      if (disposing)
      {
        this.themeScope?.Dispose();
        this.themeScope = null;
        this.picker.Dispose();
      }

      this.isDisposed = true;
    }

    /// <summary>
    /// Sums up the last pricing job, or an empty string while none has run.
    /// </summary>
    /// <param name="stats">The stats of the last finished job.</param>
    /// <returns>The summary text.</returns>
    private static string QuerySummary(QueryStats? stats)
    {
      if (stats == null || stats.Items == 0)
      {
        return string.Empty;
      }

      var items = stats.Items == 1 ? "1 item" : $"{stats.Items.ToString("N0", CultureInfo.CurrentCulture)} items";
      var queries = stats.Queries == 1 ? "1 query" : $"{stats.Queries.ToString("N0", CultureInfo.CurrentCulture)} queries";
      var milliseconds = stats.Milliseconds.ToString("N0", CultureInfo.CurrentCulture);

      return $"{items} over {queries} @ {stats.Scope} in {milliseconds} ms";
    }

    /// <summary>
    /// Works out the width the action column needs to hold every button on a row.
    /// </summary>
    /// <returns>The width, in pixels.</returns>
    private static float ActionColumnWidth()
    {
      var style = ImGui.GetStyle();

      return (ActionButtonCount * ActionButtonWidth * ImGui.GetIO().FontGlobalScale)
        + ((ActionButtonCount - 1) * style.ItemSpacing.X)
        + (2 * style.CellPadding.X);
    }

    /// <summary>
    /// Checks whether a row was bought by the last buy run.
    /// </summary>
    /// <param name="item">The row to check.</param>
    /// <returns>True when the row was bought.</returns>
    private static bool WasBought(SavedItem item)
    {
      return item.Outcome is BuyOutcome.Bought or BuyOutcome.BoughtCheaper;
    }

    /// <summary>
    /// Checks whether a row was tried by the last buy run and did not come away with everything.
    /// </summary>
    /// <param name="item">The row to check.</param>
    /// <returns>True when the row was not bought, or only partly bought.</returns>
    private static bool BuyFailed(SavedItem item)
    {
      return item.Outcome is BuyOutcome.Failed or BuyOutcome.PartlyBought;
    }

    /// <summary>
    /// Counts a row's picked listings, saying how many of them are still on sale.
    /// </summary>
    /// <param name="item">The row to count.</param>
    /// <returns>The count as it reads in a tooltip.</returns>
    private static string PickCountText(SavedItem item)
    {
      var total = item.Picks.Count;
      var live = item.LivePicks.Count();

      var listings = total == 1 ? "1 listing" : $"{total} listings";

      return live == total ? listings : $"{listings}, {total - live} gone";
    }

    /// <summary>
    /// Formats how many of a row's item the listing behind it holds.
    /// </summary>
    /// <param name="item">The row to format.</param>
    /// <returns>The quantity as it reads in the table.</returns>
    private static string QtyText(SavedItem item)
    {
      return item.Unlisted || item.Quantity <= 0
        ? NoValue
        : item.Quantity.ToString("N0", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats which world a row's listing sits on.
    /// </summary>
    /// <param name="item">The row to format.</param>
    /// <returns>The world as it reads in the table.</returns>
    private static string WorldText(SavedItem item)
    {
      return item.Unlisted ? NoValue : item.World;
    }

    /// <summary>
    /// Measures how wide a column has to be to hold its heading and all of its values.
    /// </summary>
    /// <param name="header">The column heading.</param>
    /// <param name="values">The values the column draws.</param>
    /// <returns>The width, in pixels.</returns>
    private static float ColumnWidth(string header, IEnumerable<string> values)
    {
      // The heading also keeps room for the sort arrow, which is what the column ends up as wide as.
      var widest = ImGui.CalcTextSize(header).X + ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.X;

      foreach (var value in values)
      {
        widest = Math.Max(widest, ImGui.CalcTextSize(value).X);
      }

      return widest;
    }

    /// <summary>
    /// Draws text in the middle of its column, under the heading.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="width">The width the column was measured at.</param>
    private static void Centered(string text, float width)
    {
      var edge = Math.Min(ImGui.GetContentRegionAvail().X, width);
      var padding = (edge - ImGui.CalcTextSize(text).X) / 2.0f;

      if (padding > 0)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
      }

      ImGui.Text(text);
    }

    /// <summary>
    /// Draws text pushed to the right edge of its column.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="width">The width the column was measured at.</param>
    private static void RightAligned(string text, float width)
    {
      // Padding out to the full cell would count as content and stop the column from ever fitting its values.
      var edge = Math.Min(ImGui.GetContentRegionAvail().X, width);
      var padding = edge - ImGui.CalcTextSize(text).X;

      if (padding > 0)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
      }

      ImGui.Text(text);
    }

    /// <summary>
    /// Draws the world and scope combos.
    /// </summary>
    /// <param name="sameLine">
    /// True to right-align them at the end of the action bar, false to give them a row of their own.
    /// </param>
    private void DrawScopePickers(bool sameLine)
    {
      var scope = this.Plugin.ShoppingListScope;
      var scale = ImGui.GetIO().FontGlobalScale;
      var showWorld = this.Plugin.Config.WorldOverridesEnabled;
      var spacing = showWorld ? ImGui.GetStyle().ItemSpacing.X : 0.0f;

      if (sameLine)
      {
        ImGui.SameLine();
      }

      var available = ImGui.GetContentRegionAvail().X;

      // The scope combo names the world, data centre or region it prices at, so it has to fit that.
      var scopeWidth = Math.Max(
        140 * scale,
        ImGui.CalcTextSize(scope.SelectedDisplayName).X + ImGui.GetFrameHeight() + (ImGui.GetStyle().FramePadding.X * 2));

      var worldWidth = 0.0f;

      if (showWorld)
      {
        worldWidth = sameLine ? 150 * scale : available - scopeWidth - spacing;
      }
      else if (!sameLine)
      {
        scopeWidth = available;
      }

      if (scopeWidth + worldWidth + spacing > available)
      {
        // Share what is left rather than spilling out of the window.
        var share = Math.Max(available - spacing, 80 * scale);
        scopeWidth = showWorld ? share * 0.5f : share;
        worldWidth = share - scopeWidth;
      }

      var padding = available - worldWidth - scopeWidth - spacing;
      if (padding > 0)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
      }

      ImGui.BeginDisabled(this.Plugin.ShoppingListBulkAdd.IsRunning);

      if (showWorld)
      {
        ImGui.SetNextItemWidth(worldWidth);
        this.worldPicker.Draw(scope, this.theme);

        ImGui.SameLine();
      }

      ImGui.SetNextItemWidth(scopeWidth);
      ScopePicker.Draw("##shoppingListScope", scope);

      ImGui.EndDisabled();
    }

    private void DrawActionBar()
    {
      var scope = this.Plugin.ShoppingListScope;
      var busy = this.Plugin.ShoppingListBulkAdd.IsRunning;

      ImGui.BeginDisabled(busy || this.Plugin.ShoppingListBuyer.IsRunning || !scope.HasSelection);

      if (ImGui.Button("Refresh all"))
      {
        this.Plugin.ShoppingListBulkAdd.StartRefresh(
          this.Plugin.ShoppingList.Where(i => !i.IsDirect).Select(i => i.SourceItem).ToArray(),
          scope.QueryTargets);
      }

      ImGui.EndDisabled();
      Utilities.HoverTooltip("Price every item on the list again.");

      ImGui.SameLine();

      var buyer = this.Plugin.ShoppingListBuyer;
      var loggedIn = this.Plugin.ClientState.IsLoggedIn;

      ImGui.BeginDisabled(busy || buyer.IsRunning || !this.Plugin.Config.ShoppingListBuyEnabled || !loggedIn);

      if (ImGui.Button("Buy all"))
      {
        buyer.BuyAll(this.Plugin.ShoppingList.ToArray());
      }

      ImGui.EndDisabled();
      Utilities.HoverTooltip(
        loggedIn
          ? "Buy every row still listed at or below its price, closest worlds first."
          : "Log in to a character to buy.",
        ImGuiHoveredFlags.AllowWhenDisabled);

      ImGui.SameLine();

      if (ImGui.Button("Copy"))
      {
        ImGui.OpenPopup("shoppingListCopy");
      }

      Utilities.HoverTooltip("Copy the list to the clipboard.");
      this.DrawCopyPopup();

      ImGui.SameLine();

      ImGui.BeginDisabled(busy);

      if (ImGui.Button("Clear"))
      {
        this.Plugin.ShoppingList.Clear();
      }

      ImGui.EndDisabled();
      Utilities.HoverTooltip("Remove every item from the list.");

      this.DrawScopePickers(true);

      ImGui.Separator();
    }

    /// <summary>
    /// Draws what to do with the rows a buy run bought or could not buy, pinned above the total.
    /// </summary>
    /// <param name="bought">How many rows were bought.</param>
    /// <param name="failed">How many rows were not.</param>
    private void DrawOutcomeBar(int bought, int failed)
    {
      var list = this.Plugin.ShoppingList;
      var scope = this.Plugin.ShoppingListScope;
      var busy = this.Plugin.ShoppingListBulkAdd.IsRunning || this.Plugin.ShoppingListBuyer.IsRunning;

      ImGui.BeginDisabled(busy);

      if (bought > 0)
      {
        if (ImGui.Button("Clear successful"))
        {
          list.RemoveAll(WasBought);
        }

        Utilities.HoverTooltip("Remove every row that was bought from the list.");

        ImGui.SameLine();

        if (ImGui.Button("Reset successful"))
        {
          list.ClearOutcomes(list.Where(WasBought).Select(i => i.SourceItem.RowId).ToArray());
        }

        Utilities.HoverTooltip("Take the colour off the rows that were bought, leaving them on the list.");
      }

      if (failed > 0)
      {
        if (bought > 0)
        {
          ImGui.SameLine();
        }

        ImGui.BeginDisabled(!scope.HasSelection);

        if (ImGui.Button("Refresh failed"))
        {
          this.Plugin.ShoppingListBulkAdd.StartRefresh(
            list.Where(i => BuyFailed(i) && !i.IsDirect).Select(i => i.SourceItem).ToArray(),
            scope.QueryTargets,
            "the rows that did not buy");
        }

        ImGui.EndDisabled();
        Utilities.HoverTooltip("Price every row that did not buy again.");

        ImGui.SameLine();

        if (ImGui.Button("Reset failed"))
        {
          list.ClearOutcomes(list.Where(BuyFailed).Select(i => i.SourceItem.RowId).ToArray());
        }

        Utilities.HoverTooltip("Take the colour off the rows that did not buy.");
      }

      ImGui.EndDisabled();
    }

    /// <summary>
    /// Draws the item's game icon at text height and leaves the cursor on the same line as the name.
    /// </summary>
    /// <param name="item">The row to draw the icon of.</param>
    private void DrawItemIcon(SavedItem item)
    {
      var size = new Vector2(ImGui.GetTextLineHeight());

      using var icon = this.Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup
      {
        IconId = item.SourceItem.Icon,
        ItemHq = item.Hq,
      }).GetWrapOrDefault();

      if (icon == null)
      {
        // The name still lines up with the rows that did get an icon.
        ImGui.Dummy(size);
      }
      else
      {
        ImGui.Image(icon.Handle, size);
      }

      ImGui.SameLine();
    }

    /// <summary>
    /// Spells out a row's picked listings while the cursor is over one of its figures.
    /// </summary>
    /// <param name="item">The row being hovered.</param>
    private void PickTooltip(SavedItem item)
    {
      if (!item.HasPicks || !ImGui.IsItemHovered())
      {
        return;
      }

      ImGui.BeginTooltip();

      foreach (var pick in item.Picks.OrderBy(p => p.Price))
      {
        var price = pick.Price.ToString("N0", CultureInfo.CurrentCulture);

        // The row only marks itself high quality when every pick is, so each one says for itself.
        var quality = pick.Hq ? $" {SeIconChar.HighQuality.AsString()}" : string.Empty;
        var line = $"{pick.Quantity}{quality} @ {price} on {pick.World} ({pick.RetainerName})";

        ImGui.PushStyleColor(ImGuiCol.Text, pick.Gone ? this.theme.TextDim : this.theme.Text);
        ImGui.Text(pick.Gone ? $"{line} - gone" : line);
        ImGui.PopStyleColor();
      }

      ImGui.EndTooltip();
    }

    /// <summary>
    /// Says what the pick button on a row would do.
    /// </summary>
    /// <param name="item">The row the button belongs to.</param>
    /// <returns>The tooltip text.</returns>
    private string PickTooltipText(SavedItem item)
    {
      if (item.IsDirect)
      {
        return "This is a direct listing, so it already buys exactly one listing.";
      }

      if (!this.Plugin.ShoppingListScope.HasSelection)
      {
        return "Pick a scope first so the listings to choose from can be fetched.";
      }

      return item.HasPicks
        ? $"Choose which listings to buy. {PickCountText(item)} chosen."
        : "Choose which listings to buy, instead of just the cheapest one.";
    }

    /// <summary>
    /// Says what the buy button on a row would do.
    /// </summary>
    /// <param name="item">The row the button belongs to.</param>
    /// <returns>The tooltip text.</returns>
    private string BuyTooltip(SavedItem item)
    {
      if (!item.HasPicks)
      {
        return $"Buy this listing on {item.World} if it is still there at {this.PriceText(item)} or less.";
      }

      var live = item.LivePicks.Count();
      var listings = live == 1 ? "the 1 listing" : $"the {live} listings";

      return $"Buy {listings} picked for this row, for {this.TotalText(item)} in all.";
    }

    private string PriceText(SavedItem item)
    {
      if (item.Refreshing)
      {
        return "Refreshing";
      }

      if (item.Unlisted)
      {
        return NoValue;
      }

      return this.Plugin.Config.PriceIconShown
        ? item.Price.ToString("C", this.Plugin.NumberFormatInfo)
        : item.Price.ToString("N0", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats the gil the whole listing behind a row costs.
    /// </summary>
    /// <param name="item">The row to format.</param>
    /// <returns>The text to draw in the total cell.</returns>
    private string TotalText(SavedItem item)
    {
      if (item.Refreshing)
      {
        return "Refreshing";
      }

      if (item.Unlisted || item.Quantity <= 0)
      {
        return NoValue;
      }

      return this.Plugin.Config.PriceIconShown
        ? item.Total.ToString("C", this.Plugin.NumberFormatInfo)
        : item.Total.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void DrawFooter()
    {
      var total = this.Plugin.ShoppingList.Sum(i => i.Total);
      var text = "Total Cost: " + (this.Plugin.Config.PriceIconShown
        ? total.ToString("C", this.Plugin.NumberFormatInfo)
        : total.ToString("N0", CultureInfo.CurrentCulture));

      var totalWidth = ImGui.CalcTextSize(text).X;
      var summary = QuerySummary(this.Plugin.Config.ShoppingListLastQuery);

      ImGui.AlignTextToFramePadding();

      // The total keeps the row to itself rather than being pushed off it when both do not fit.
      if (summary.Length > 0 &&
          ImGui.CalcTextSize(summary).X + ImGui.GetStyle().ItemSpacing.X + totalWidth <= ImGui.GetContentRegionAvail().X)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
        ImGui.Text(summary);
        ImGui.PopStyleColor();
        ImGui.SameLine();
      }

      var padding = ImGui.GetContentRegionAvail().X - totalWidth;
      if (padding > 0)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
      }

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.GilText);
      ImGui.Text(text);
      ImGui.PopStyleColor();
    }

    private void DrawCopyPopup()
    {
      if (!ImGui.BeginPopup("shoppingListCopy"))
      {
        return;
      }

      var config = this.Plugin.Config;

      this.DrawCopyOption("Item name", config.ShoppingListCopyName, v => config.ShoppingListCopyName = v);
      this.DrawCopyOption("Price", config.ShoppingListCopyPrice, v => config.ShoppingListCopyPrice = v);
      this.DrawCopyOption("World", config.ShoppingListCopyWorld, v => config.ShoppingListCopyWorld = v);

      ImGui.Separator();

      ImGui.BeginDisabled(!config.ShoppingListCopyName && !config.ShoppingListCopyPrice && !config.ShoppingListCopyWorld);

      if (ImGui.Button("Copy to clipboard"))
      {
        this.CopyList();
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndDisabled();
      ImGui.EndPopup();
    }

    private void DrawCopyOption(string label, bool value, Action<bool> setter)
    {
      var current = value;

      if (ImGui.Checkbox(label, ref current))
      {
        setter(current);
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }
    }

    private void CopyList()
    {
      var config = this.Plugin.Config;

      // The rows are copied in the order they are shown, unless the sorted view is out of date.
      IReadOnlyList<SavedItem> rows = this.sortedItems.Count == this.Plugin.ShoppingList.Count
        ? this.sortedItems
        : this.Plugin.ShoppingList;

      var builder = new StringBuilder();

      foreach (var item in rows)
      {
        var parts = new List<string>();

        if (config.ShoppingListCopyName)
        {
          parts.Add(item.SourceItem.Name.ExtractText());
        }

        if (config.ShoppingListCopyPrice)
        {
          parts.Add(item.Unlisted ? NoValue : item.Price.ToString("N0", CultureInfo.CurrentCulture));
        }

        if (config.ShoppingListCopyWorld)
        {
          parts.Add(item.Unlisted ? NoValue : item.World);
        }

        builder.AppendLine(string.Join(" - ", parts));
      }

      var text = builder.ToString().TrimEnd();

      if (text.Length == 0)
      {
        return;
      }

      ImGui.SetClipboardText(text);

      if (config.ClipboardNotificationsEnabled)
      {
        this.Plugin.NotifyClipboardCopied($"{rows.Count} shopping list rows");
      }
    }

    private void UpdateSort()
    {
      var specs = ImGui.TableGetSortSpecs();

      if (specs.IsNull)
      {
        return;
      }

      var column = -1;
      var ascending = true;

      if (specs.SpecsCount > 0)
      {
        var spec = specs.Specs;
        column = spec.ColumnIndex;
        ascending = spec.SortDirection != ImGuiSortDirection.Descending;
      }

      specs.SpecsDirty = false;

      if (column == this.sortColumn && ascending == this.sortAscending && this.sortedRevision == this.Plugin.ShoppingList.Revision)
      {
        return;
      }

      this.sortColumn = column;
      this.sortAscending = ascending;
      this.sortedRevision = this.Plugin.ShoppingList.Revision;

      this.sortedItems.Clear();
      this.sortedItems.AddRange(this.SortItems());
    }

    private IEnumerable<SavedItem> SortItems()
    {
      var items = this.Plugin.ShoppingList;

      switch (this.sortColumn)
      {
        case 0:
          return this.sortAscending
            ? items.OrderBy(i => i.Hq)
            : items.OrderByDescending(i => i.Hq);
        case 1:
          return this.sortAscending
            ? items.OrderBy(i => i.SourceItem.Name.ExtractText(), StringComparer.CurrentCultureIgnoreCase)
            : items.OrderByDescending(i => i.SourceItem.Name.ExtractText(), StringComparer.CurrentCultureIgnoreCase);
        case 2:
          return this.sortAscending
            ? items.OrderBy(i => i.Price)
            : items.OrderByDescending(i => i.Price);
        case 3:
          return this.sortAscending
            ? items.OrderBy(i => i.Quantity)
            : items.OrderByDescending(i => i.Quantity);
        case 4:
          return this.sortAscending
            ? items.OrderBy(i => i.Total)
            : items.OrderByDescending(i => i.Total);
        case 5:
          return this.sortAscending
            ? items.OrderBy(i => i.World, StringComparer.CurrentCultureIgnoreCase)
            : items.OrderByDescending(i => i.World, StringComparer.CurrentCultureIgnoreCase);
        default:
          return items;
      }
    }

    private void DrawBulkAddProgress()
    {
      var bulkAdd = this.Plugin.ShoppingListBulkAdd;

      if (!bulkAdd.IsRunning)
      {
        return;
      }

      var processed = bulkAdd.Counted;
      var total = bulkAdd.Total;

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.TextWrapped($"Pricing {bulkAdd.CategoryName}...");
      ImGui.PopStyleColor();

      var cancelWidth = ImGui.CalcTextSize("Cancel").X + (ImGui.GetStyle().FramePadding.X * 2);
      var barWidth = ImGui.GetContentRegionAvail().X - cancelWidth - ImGui.GetStyle().ItemSpacing.X;

      ImGui.ProgressBar(
        total > 0 ? processed / (float)total : 0f,
        new Vector2(barWidth, 0),
        $"{processed} / {total}");

      ImGui.SameLine();

      if (ImGui.Button("Cancel"))
      {
        bulkAdd.Cancel();
      }

      ImGui.Separator();
    }

    /// <summary>
    /// Draws how far a buy run has got, and the button that stops it.
    /// </summary>
    private void DrawBuyProgress()
    {
      var buyer = this.Plugin.ShoppingListBuyer;

      if (!buyer.IsRunning)
      {
        return;
      }

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.TextWrapped($"Buying {buyer.CurrentItemName}...");
      ImGui.PopStyleColor();

      var cancelWidth = ImGui.CalcTextSize("Cancel").X + (ImGui.GetStyle().FramePadding.X * 2);
      var barWidth = ImGui.GetContentRegionAvail().X - cancelWidth - ImGui.GetStyle().ItemSpacing.X;

      ImGui.ProgressBar(
        buyer.Total > 0 ? buyer.Done / (float)buyer.Total : 0f,
        new Vector2(barWidth, 0),
        $"{buyer.Done} / {buyer.Total}");

      ImGui.SameLine();

      if (ImGui.Button("Cancel##buyRun"))
      {
        buyer.Cancel();
      }

      ImGui.Separator();
    }
  }
}
