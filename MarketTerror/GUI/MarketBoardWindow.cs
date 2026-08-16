// <copyright file="MarketBoardWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>
namespace MarketTerror.GUI
{
  using System;
  using System.Globalization;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Bindings.ImPlot;
  using Dalamud.Interface;
  using Dalamud.Interface.ManagedFontAtlas;
  using Dalamud.Interface.Windowing;
  using MarketTerror.GUI.Components;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Services;

  /// <summary>
  /// The market board window.
  /// </summary>
  /// <remarks>
  /// This type owns the components and services and composes them; the drawing itself lives in the
  /// components under <see cref="MarketTerror.GUI.Components"/>.
  /// </remarks>
  public class MarketBoardWindow : Window, IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly IFontHandle defaultFontHandle;

    private readonly IFontHandle titleFontHandle;

    private readonly TerrorTheme theme;

    private readonly ItemCatalog catalog;

    private readonly MarketDataProvider marketDataProvider;

    private readonly WorldSelection worldSelection;

    private readonly HoveredItemWatcher hoveredItemWatcher;

    private readonly MarketBoardContext context;

    private readonly ItemSearchPanel searchPanel;

    private readonly ItemListPanel itemListPanel;

    private readonly ItemHeaderBar headerBar;

    private readonly ListingsTable listingsTable;

    private readonly HistoryTable historyTable;

    private readonly StatsPanel statsPanel;

    private readonly LinksPopup linksPopup;

    private readonly TitleBarButton integrationsButton;

    private IDisposable? themeScope;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardWindow"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public MarketBoardWindow(MarketTerrorPlugin plugin)
      : base("Market Terror")
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.Flags = ImGuiWindowFlags.NoScrollbar;
      this.Size = new Vector2(800, 600);
      this.SizeCondition = ImGuiCond.FirstUseEver;
      this.SizeConstraints = new WindowSizeConstraints
      {
        MinimumSize = new Vector2(350, 225),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
      };

      this.defaultFontHandle = this.plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        e.OnPreBuild(toolkit =>
        {
          var fontStream = this.GetType().Assembly.GetManifestResourceStream("MarketTerror.Resources.NotoSans-Medium-NNBSP.otf");

          if (fontStream == null)
          {
            this.plugin.Log.Warning("Failed to load embedded font MarketTerror.Resources.NotoSans-Medium-NNBSP.otf");
            return;
          }

          toolkit.AddFontFromStream(
            fontStream,
            new SafeFontConfig()
            {
              SizePx = UiBuilder.DefaultFontSizePx,
              GlyphRanges = FontAtlasBuildToolkitUtilities.ToGlyphRange(char.ConvertFromUtf32(0x202F)),
              MergeFont = toolkit.AddDalamudDefaultFont(-1),
            },
            false,
            "NNBSP");
        }));

      this.titleFontHandle = this.plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        e.OnPreBuild(toolkit =>
          toolkit.AddDalamudDefaultFont(this.plugin.PluginInterface.UiBuilder.DefaultFontSpec.SizePx * 1.5f)));

      var imPlotStylePtr = ImPlot.GetStyle();

      imPlotStylePtr.Use24HourClock = DateTimeFormatInfo.CurrentInfo.ShortTimePattern.Contains('H', StringComparison.InvariantCulture);
      imPlotStylePtr.UseISO8601 = DateTimeFormatInfo.CurrentInfo.ShortDatePattern != "M/d/yyyy";
      imPlotStylePtr.UseLocalTime = true;

      this.theme = new TerrorTheme(this.plugin.Config);
      this.catalog = new ItemCatalog(this.plugin.DataManager, this.plugin.Log);
      this.marketDataProvider = new MarketDataProvider(this.plugin);
      this.worldSelection = new WorldSelection(this.plugin);

      this.context = new MarketBoardContext(
        this.plugin,
        this.theme,
        this.catalog,
        this.marketDataProvider,
        this.worldSelection,
        this.titleFontHandle);

      this.hoveredItemWatcher = new HoveredItemWatcher(this.plugin, this.catalog, id => this.context.SelectItem(id));

      this.searchPanel = new ItemSearchPanel(this.context);
      this.itemListPanel = new ItemListPanel(this.context);
      this.headerBar = new ItemHeaderBar(this.context);
      this.listingsTable = new ListingsTable(this.context);
      this.historyTable = new HistoryTable(this.context);
      this.statsPanel = new StatsPanel(this.context);
      this.linksPopup = new LinksPopup(this.context);

      this.integrationsButton = IntegrationsButton.Build(this.context);
      this.TitleBarButtons.Add(this.integrationsButton);

      this.TitleBarButtons.Add(new TitleBarButton
      {
        Icon = FontAwesomeIcon.Heart,
        IconOffset = new Vector2(2, 1),
        Click = _ => this.linksPopup.Open(),
        ShowTooltip = () =>
        {
          ImGui.BeginTooltip();
          ImGui.Text("Links");
          ImGui.EndTooltip();
        },
      });
    }

    /// <summary>
    /// Gets or sets the current search string.
    /// </summary>
    public string SearchString
    {
      get => this.context.SearchString;
      set => this.context.SearchString = value;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Reset the market data.
    /// </summary>
    public void ResetMarketData()
    {
      this.context.ResetMarketData();
    }

    /// <inheritdoc/>
    public override void PreDraw()
    {
      IntegrationsButton.Refresh(this.integrationsButton, this.context);
      this.themeScope = this.theme.Push();
    }

    /// <inheritdoc/>
    public override void PostDraw()
    {
      this.themeScope?.Dispose();
      this.themeScope = null;
    }

    /// <summary>
    /// Draws the window.
    /// </summary>
    public override void Draw()
    {
      var scale = ImGui.GetIO().FontGlobalScale;

      using var fontDispose = this.defaultFontHandle.Push();

      var splitterWidth = ImGui.GetTextLineHeight() * 0.5f;
      var minColumnWidth = 150.0f * scale;
      var maxColumnWidth = Math.Max(minColumnWidth, ImGui.GetContentRegionAvail().X - splitterWidth - (200.0f * scale));
      var columnWidth = Math.Clamp(this.plugin.Config.ItemListColumnWidth * scale, minColumnWidth, maxColumnWidth);

      ImGui.BeginChild("itemListColumn", new Vector2(columnWidth, 0), true);

      this.searchPanel.Draw();

      ImGui.Separator();

      this.itemListPanel.Draw();

      this.hoveredItemWatcher.Tick();

      if (this.plugin.Config.WatchForHovered)
      {
        ImGui.ProgressBar(this.hoveredItemWatcher.Progress, new Vector2(-1, 0), string.Empty);
      }

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
        this.plugin.Config.ItemListColumnWidth =
          Math.Clamp(columnWidth + columnDrag, minColumnWidth, maxColumnWidth) / scale;
      }

      if (ImGui.IsItemDeactivated())
      {
        this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
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

          ImGui.Separator();
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
    }

    /// <summary>
    /// Selects an item and refreshes its market data.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <param name="noHistory">True to leave the search history untouched.</param>
    internal void ChangeSelectedItem(uint itemId, bool noHistory = false)
    {
      this.context.SelectItem(itemId, noHistory);
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
        this.hoveredItemWatcher.Dispose();
        this.worldSelection.Dispose();
        this.marketDataProvider.Dispose();
        this.defaultFontHandle?.Dispose();
        this.titleFontHandle?.Dispose();
      }

      this.isDisposed = true;
    }

    /// <summary>
    /// Draws the listings and history tables, split by a bar the user can drag to resize them.
    /// </summary>
    /// <param name="scale">The current UI scale.</param>
    private void DrawMarketData(float scale)
    {
      this.titleFontHandle.Push();
      var headingHeight = ImGui.GetTextLineHeightWithSpacing();
      this.titleFontHandle.Pop();

      var spacing = ImGui.GetStyle().ItemSpacing.Y;
      var available = ImGui.GetContentRegionAvail().Y;

      if (this.plugin.Config.RecentHistoryDisabled)
      {
        this.listingsTable.Draw(available - headingHeight - spacing);
        return;
      }

      var splitterHeight = ImGui.GetTextLineHeight() * 0.5f;

      // The two tables and the splitter each add an item spacing below themselves.
      var usable = available - (headingHeight * 2) - splitterHeight - (spacing * 3);

      if (usable <= 0.0f)
      {
        return;
      }

      // Keep enough room in either table for its header row and a couple of entries.
      var minRatio = Math.Min(0.4f, ImGui.GetTextLineHeightWithSpacing() * 3.0f / usable);
      var ratio = Math.Clamp(this.plugin.Config.MarketDataSplitRatio, minRatio, 1.0f - minRatio);
      var listingsHeight = usable * ratio;

      this.listingsTable.Draw(listingsHeight);

      var drag = this.DrawSplitter(
        "marketDataSplitter",
        new Vector2(ImGui.GetContentRegionAvail().X, splitterHeight),
        false,
        scale,
        true);

      if (drag != 0.0f)
      {
        this.plugin.Config.MarketDataSplitRatio = Math.Clamp(ratio + (drag / usable), minRatio, 1.0f - minRatio);
      }

      if (ImGui.IsItemDeactivated())
      {
        this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
      }

      this.historyTable.Draw(usable - listingsHeight);
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
          hovered ? this.theme.AccentHover : this.theme.Border,
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
