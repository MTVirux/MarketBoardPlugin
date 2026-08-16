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

    private readonly StatusFooter statusFooter;

    private IDisposable? themeScope;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardWindow"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public MarketBoardWindow(MarketTerrorPlugin plugin)
      : base("Market Board")
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
      this.statusFooter = new StatusFooter(this.context);
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
      this.catalog.ApplyFilter(
        this.context.SearchString,
        this.context.ItemCategory,
        this.context.MinLevel,
        this.context.MaxLevel,
        this.context.SelectedClassJob);

      var scale = ImGui.GetIO().FontGlobalScale;

      using var fontDispose = this.defaultFontHandle.Push();

      ImGui.BeginChild("itemListColumn", new Vector2(267, 0) * scale, true);

      this.searchPanel.Draw();

      ImGui.Separator();

      this.itemListPanel.Draw();

      this.hoveredItemWatcher.Tick();

      ImGui.Text("Settings : ");
      ImGui.SameLine();
      ImGui.PushFont(UiBuilder.IconFont);
      if (ImGui.Button($"{(char)FontAwesomeIcon.Cog}"))
      {
        this.plugin.OpenConfigUi();
      }

      ImGui.PopFont();

      ImGui.ProgressBar(this.hoveredItemWatcher.Progress, new Vector2(-1, 0), string.Empty);

      ImGui.EndChild();
      ImGui.SameLine();
      ImGui.BeginChild("tabColumn", new Vector2(0, 0), false, ImGuiWindowFlags.NoScrollbar);

      if (this.context.SelectedItem?.RowId > 0)
      {
        this.headerBar.Draw();

        if (ImGui.BeginTabBar("tabBar"))
        {
          if (ImGui.BeginTabItem("Market Data##marketDataTab"))
          {
            float tableHeight;

            this.titleFontHandle.Push();
            var usedTile = this.plugin.Config.RecentHistoryDisabled ? 1 : 2;
            tableHeight = (ImGui.GetContentRegionAvail().Y / usedTile) - (ImGui.GetTextLineHeightWithSpacing() * 2);
            this.titleFontHandle.Pop();

            this.listingsTable.Draw(tableHeight);

            if (!this.plugin.Config.RecentHistoryDisabled)
            {
              this.historyTable.Draw(tableHeight);
            }

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

      this.statusFooter.Draw();

      ImGui.EndChild();
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
  }
}
