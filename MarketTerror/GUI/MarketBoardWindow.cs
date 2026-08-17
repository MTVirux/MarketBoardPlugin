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
  using MarketTerror.Services;

  /// <summary>
  /// The market board window.
  /// </summary>
  /// <remarks>
  /// This type owns the services and hosts a <see cref="MarketBoard"/>; the drawing itself lives in
  /// <see cref="MarketBoard"/> and the components under <see cref="MarketTerror.GUI.Components"/>.
  /// </remarks>
  public class MarketBoardWindow : Window, IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly IFontHandle defaultFontHandle;

    private readonly IFontHandle titleFontHandle;

    private readonly BoardServices services;

    private readonly HoveredItemWatcher hoveredItemWatcher;

    private readonly MarketBoard board;

    private readonly TitleBarButton integrationsButton;

    private readonly TitleBarButton shoppingListButton;

    private IDisposable? themeScope;

    private bool isDisposed;

#if DEBUG
    private uint pendingItemId;
#endif

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

      this.services = new BoardServices(this.plugin, this.defaultFontHandle, this.titleFontHandle);
      var context = new MarketBoardContext(this.services, WorldSelection.ForMainWindow(this.plugin));

      this.hoveredItemWatcher = new HoveredItemWatcher(this.plugin, this.services.Catalog, id => context.SelectItem(id));

      this.board = new MarketBoard(this.services, context, isMainBoard: true);

      foreach (var tab in Enum.GetValues<ItemListTab>())
      {
        this.board.Tabs.Add(tab);
      }

      this.board.DrawUnderList = () =>
      {
        this.hoveredItemWatcher.Tick();

        if (this.plugin.Config.WatchForHovered)
        {
          ImGui.ProgressBar(this.hoveredItemWatcher.Progress, new Vector2(-1, 0), string.Empty);
        }
      };

      this.integrationsButton = IntegrationsButton.Build(this.board.Context);
      this.TitleBarButtons.Add(this.integrationsButton);

      this.shoppingListButton = ShoppingListButton.Build(this.board.Context);
      this.TitleBarButtons.Add(this.shoppingListButton);

      this.TitleBarButtons.Add(new TitleBarButton
      {
        Icon = FontAwesomeIcon.Heart,
        IconOffset = new Vector2(2, 1),
        Click = _ => this.board.OpenLinksPopup(),
        ShowTooltip = () =>
        {
          ImGui.BeginTooltip();
          ImGui.Text("Links");
          ImGui.EndTooltip();
        },
      });

      this.TitleBarButtons.Add(new TitleBarButton
      {
        Icon = FontAwesomeIcon.Cog,
        IconOffset = new Vector2(2, 1),
        Click = _ => this.plugin.OpenConfigUi(),
        ShowTooltip = () =>
        {
          ImGui.BeginTooltip();
          ImGui.Text("Settings");
          ImGui.EndTooltip();
        },
      });

#if DEBUG
      if (this.plugin.Config.RememberLastItem)
      {
        this.pendingItemId = this.plugin.Config.LastOpenedItem;
      }
#endif
    }

    /// <summary>
    /// Gets the state and services shared by every component of this window.
    /// </summary>
    public MarketBoardContext Context => this.board.Context;

    /// <summary>
    /// Gets or sets the current search string.
    /// </summary>
    public string SearchString
    {
      get => this.board.Context.SearchString;
      set => this.board.Context.SearchString = value;
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
      this.board.Context.ResetMarketData();
    }

    /// <inheritdoc/>
    public override void PreDraw()
    {
      IntegrationsButton.Refresh(this.integrationsButton, this.board.Context);
      ShoppingListButton.Refresh(this.shoppingListButton, this.board.Context);
      this.themeScope = this.board.Context.Theme.Push();
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
#if DEBUG
      this.RestoreLastOpenedItem();
#endif

      using var fontDispose = this.services.DefaultFont.Push();

      this.board.Draw();
    }

    /// <summary>
    /// Selects an item and refreshes its market data.
    /// </summary>
    /// <param name="itemId">The item row id.</param>
    /// <param name="noHistory">True to leave the search history untouched.</param>
    internal void ChangeSelectedItem(uint itemId, bool noHistory = false)
    {
      this.board.Context.SelectItem(itemId, noHistory);
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
        this.board.Dispose();
        this.services.Dispose();
        this.defaultFontHandle?.Dispose();
        this.titleFontHandle?.Dispose();
      }

      this.isDisposed = true;
    }

#if DEBUG
    /// <summary>
    /// Reselects the item that was open last, once a world is available to query it against.
    /// </summary>
    private void RestoreLastOpenedItem()
    {
      if (this.pendingItemId == 0 || !this.board.Context.Worlds.HasSelection)
      {
        return;
      }

      var itemId = this.pendingItemId;
      this.pendingItemId = 0;
      this.board.Context.SelectItem(itemId, true);
    }
#endif
  }
}
