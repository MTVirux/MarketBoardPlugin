// <copyright file="MarketBoard.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Collections.Generic;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using MarketTerror.GUI.Components;

  /// <summary>
  /// The item list, item details and market data for one board - the main window's or a detached one's.
  /// </summary>
  /// <remarks>
  /// This type owns the components it composes; the drawing itself lives in the components under
  /// <see cref="MarketTerror.GUI.Components"/>.
  /// </remarks>
  public sealed class MarketBoard : IDisposable
  {
    private readonly BoardServices services;

    private readonly MarketBoardContext context;

    private readonly ItemSearchPanel searchPanel;

    private readonly ItemListPanel itemListPanel;

    private readonly ItemHeaderBar headerBar;

    private readonly ListingsTable listingsTable;

    private readonly HistoryTable historyTable;

    private readonly StatsPanel statsPanel;

    private readonly LinksPopup linksPopup;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoard"/> class.
    /// </summary>
    /// <param name="services">The services shared by every board.</param>
    /// <param name="context">The state shared by every component of this board.</param>
    /// <param name="isMainBoard">True when this is the main window's board.</param>
    public MarketBoard(BoardServices services, MarketBoardContext context, bool isMainBoard)
    {
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.context = context ?? throw new ArgumentNullException(nameof(context));
      this.IsMainBoard = isMainBoard;

      this.searchPanel = new ItemSearchPanel(this.context);
      this.itemListPanel = new ItemListPanel(this.context, this);
      this.headerBar = new ItemHeaderBar(this.context);
      this.listingsTable = new ListingsTable(this.context);
      this.historyTable = new HistoryTable(this.context);
      this.statsPanel = new StatsPanel(this.context);
      this.linksPopup = new LinksPopup(this.context);
    }

    /// <summary>
    /// Gets the state and services shared by every component of this board.
    /// </summary>
    public MarketBoardContext Context => this.context;

    /// <summary>
    /// Gets the item lists this board's tab bar shows.
    /// </summary>
    public IList<ItemListTab> Tabs { get; } = new List<ItemListTab>();

    /// <summary>
    /// Gets or sets where the item list tab bar sat on screen this frame.
    /// </summary>
    public (Vector2 Min, Vector2 Max) TabBarScreenRect { get; set; }

    /// <summary>
    /// Gets or sets the tab that was dragged off the bar this frame, or null when none was.
    /// </summary>
    public ItemListTab? TearOffRequest { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is the main window's board.
    /// </summary>
    public bool IsMainBoard { get; }

    /// <summary>
    /// Gets or sets the manager the tear-off requests are handed to, or null when there is none.
    /// </summary>
    public BoardManager? Manager { get; set; }

    /// <summary>
    /// Gets or sets what to draw under the item list, or null for nothing.
    /// </summary>
    /// <remarks>The main window uses this for the hovered item progress bar; detached boards leave it null.</remarks>
    public Action? DrawUnderList { get; set; }

    /// <summary>
    /// Draws the board.
    /// </summary>
    public void Draw()
    {
      this.context.ApplyPendingSelection();

      // Outside every child and popup, so the box a menu asked for is not scoped to one of them.
      this.context.DrawNewListModal();

      var scale = ImGui.GetIO().FontGlobalScale;

      var splitterWidth = ImGui.GetTextLineHeight() * 0.5f;
      var minColumnWidth = 150.0f * scale;
      var maxColumnWidth = Math.Max(minColumnWidth, ImGui.GetContentRegionAvail().X - splitterWidth - (200.0f * scale));
      var columnWidth = Math.Clamp(this.context.Config.ItemListColumnWidth * scale, minColumnWidth, maxColumnWidth);

      ImGui.BeginChild("itemListColumn", new Vector2(columnWidth, 0), true);

      this.searchPanel.Draw();

      ImGui.Separator();

      this.itemListPanel.Draw();

      this.DrawUnderList?.Invoke();

      ImGui.EndChild();
      ImGui.SameLine(0.0f, 0.0f);

      var columnDrag = this.DrawSplitter(
        "itemListSplitter",
        new Vector2(splitterWidth, ImGui.GetContentRegionAvail().Y),
        true,
        scale,
        false);

      if (columnDrag != 0.0f)
      {
        this.context.Config.ItemListColumnWidth =
          Math.Clamp(columnWidth + columnDrag, minColumnWidth, maxColumnWidth) / scale;
      }

      if (ImGui.IsItemDeactivated())
      {
        this.context.Plugin.PluginInterface.SavePluginConfig(this.context.Config);
      }

      ImGui.SameLine(0.0f, 0.0f);
      ImGui.BeginChild("tabColumn", new Vector2(0, 0), true, ImGuiWindowFlags.NoScrollbar);

      if (this.context.SelectedItem?.RowId > 0)
      {
        this.headerBar.Draw();

        if (ImGui.BeginTabBar("tabBar"))
        {
          if (ImGui.BeginTabItem("Market Data##marketDataTab"))
          {
            this.DrawMarketData(scale);
            ImGui.EndTabItem();
          }

          if (ImGui.BeginTabItem("Stats##statsTab"))
          {
            this.statsPanel.Draw();
            ImGui.EndTabItem();
          }

          ImGui.EndTabBar();
        }
      }

      ImGui.EndChild();

      this.linksPopup.Draw();

      if (this.TearOffRequest is { } tornOff)
      {
        this.TearOffRequest = null;
        this.Manager?.Detach(tornOff);
      }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.context.Dispose();
      this.isDisposed = true;
    }

    /// <summary>
    /// Asks for this board's links popup to be opened on the next draw.
    /// </summary>
    public void OpenLinksPopup()
    {
      this.linksPopup.Open();
    }

    /// <summary>
    /// Draws the listings and history sections, split by a bar the user can drag to resize them.
    /// </summary>
    /// <param name="scale">The current UI scale.</param>
    private void DrawMarketData(float scale)
    {
      var spacing = ImGui.GetStyle().ItemSpacing.Y;
      var available = ImGui.GetContentRegionAvail().Y;
      var config = this.context.Config;

      // With one of the tables hidden there is nothing left to drag the splitter between.
      if (config.CurrentListingsCollapsed || config.SalesHistoryCollapsed)
      {
        var rest = Math.Max(available - this.CollapsedSectionHeight(spacing) - spacing, 0.0f);

        this.listingsTable.Draw(config.CurrentListingsCollapsed ? 0.0f : rest);
        this.historyTable.Draw(config.SalesHistoryCollapsed ? 0.0f : rest);
        return;
      }

      var splitterHeight = ImGui.GetTextLineHeight() * 0.5f;

      // The splitter sits flush between the sections, so only the spacing below the last one is left over.
      var usable = available - splitterHeight - spacing;

      if (usable <= 0.0f)
      {
        return;
      }

      // Keep enough room in either section for its heading, its column labels and a couple of entries.
      var minRatio = Math.Min(0.4f, this.MinSectionHeight(spacing) / usable);
      var ratio = Math.Clamp(config.MarketDataSplitRatio, minRatio, 1.0f - minRatio);
      var listingsHeight = usable * ratio;

      this.listingsTable.Draw(listingsHeight);

      // Close the item spacing on either side of the splitter so the sections sit right against it.
      ImGui.SetCursorPosY(ImGui.GetCursorPosY() - spacing);

      var drag = this.DrawSplitter(
        "marketDataSplitter",
        new Vector2(ImGui.GetContentRegionAvail().X, splitterHeight),
        false,
        scale,
        false);

      if (drag != 0.0f)
      {
        config.MarketDataSplitRatio = Math.Clamp(ratio + (drag / usable), minRatio, 1.0f - minRatio);
      }

      if (ImGui.IsItemDeactivated())
      {
        this.context.Plugin.PluginInterface.SavePluginConfig(this.context.Config);
      }

      ImGui.SetCursorPosY(ImGui.GetCursorPosY() - spacing);

      this.historyTable.Draw(usable - listingsHeight);
    }

    /// <summary>
    /// The height a section takes up with its table hidden, leaving its heading and separator.
    /// </summary>
    /// <param name="spacing">The vertical item spacing.</param>
    /// <returns>The collapsed section height in pixels.</returns>
    private float CollapsedSectionHeight(float spacing)
    {
      this.services.TitleFont.Push();
      var height = ImGui.GetTextLineHeightWithSpacing();
      this.services.TitleFont.Pop();

      return height + 1.0f + spacing;
    }

    /// <summary>
    /// The height a section needs for its heading, its separators and a few rows of its table.
    /// </summary>
    /// <param name="spacing">The vertical item spacing.</param>
    /// <returns>The minimum section height in pixels.</returns>
    private float MinSectionHeight(float spacing)
    {
      // The collapsed height, plus the separator closing the table off and a few rows of it.
      return this.CollapsedSectionHeight(spacing) + 1.0f + spacing + (ImGui.GetTextLineHeightWithSpacing() * 3.0f);
    }

    /// <summary>
    /// Draws a bar the user can drag to resize the panels on either side of it.
    /// </summary>
    /// <param name="id">The ImGui id of the bar.</param>
    /// <param name="size">The size of the grab area.</param>
    /// <param name="vertical">True for a bar between two columns, false for one between two rows.</param>
    /// <param name="scale">The current UI scale.</param>
    /// <param name="visibleWhenIdle">True to draw the bar even when it is not hovered.</param>
    /// <returns>The distance the bar was dragged this frame, in pixels.</returns>
    private float DrawSplitter(string id, Vector2 size, bool vertical, float scale, bool visibleWhenIdle)
    {
      ImGui.InvisibleButton(id, size);

      var active = ImGui.IsItemActive();
      var hovered = active || ImGui.IsItemHovered();

      if (hovered)
      {
        ImGui.SetMouseCursor(vertical ? ImGuiMouseCursor.ResizeEw : ImGuiMouseCursor.ResizeNs);
      }

      if (hovered || visibleWhenIdle)
      {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var from = vertical
          ? new Vector2((min.X + max.X) * 0.5f, min.Y)
          : new Vector2(min.X, (min.Y + max.Y) * 0.5f);
        var to = vertical
          ? new Vector2((min.X + max.X) * 0.5f, max.Y)
          : new Vector2(max.X, (min.Y + max.Y) * 0.5f);

        ImGui.GetWindowDrawList().AddLine(
          from,
          to,
          hovered ? this.context.Theme.AccentHover : this.context.Theme.Border,
          (hovered ? 2.0f : 1.0f) * scale);
      }

      if (!active)
      {
        return 0.0f;
      }

      return vertical ? ImGui.GetIO().MouseDelta.X : ImGui.GetIO().MouseDelta.Y;
    }
  }
}
