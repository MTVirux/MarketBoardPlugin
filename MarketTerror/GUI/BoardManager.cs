// <copyright file="BoardManager.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Dalamud.Interface.ManagedFontAtlas;
  using Dalamud.Interface.Windowing;
  using MarketTerror.Models;
  using MarketTerror.Services;

  /// <summary>
  /// Owns the main board and every torn-off one, and moves lists between them.
  /// </summary>
  /// <remarks>
  /// Detach and reattach are queued rather than done on the spot: they run while
  /// <see cref="WindowSystem.Draw"/> is walking its window list, and adding to or removing from
  /// that list mid-walk would break it.
  /// </remarks>
  public sealed class BoardManager : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly WindowSystem windowSystem;

    private readonly List<DetachedBoardWindow> detached = new List<DetachedBoardWindow>();

    private readonly List<ItemListTab> pendingDetach = new List<ItemListTab>();

    private readonly List<ItemListTab> pendingReattach = new List<ItemListTab>();

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardManager"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="defaultFont">The body font.</param>
    /// <param name="titleFont">The 1.5x font used for headings.</param>
    /// <param name="windowSystem">The window system the boards register with.</param>
    public BoardManager(
      MarketTerrorPlugin plugin,
      IFontHandle defaultFont,
      IFontHandle titleFont,
      WindowSystem windowSystem)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));

      this.Services = new BoardServices(plugin, defaultFont, titleFont);
      this.MainWindow = new MarketBoardWindow(this);
      this.MainWindow.Board.Manager = this;
    }

    /// <summary>
    /// Gets the services shared by every board.
    /// </summary>
    public BoardServices Services { get; }

    /// <summary>
    /// Gets the main window.
    /// </summary>
    public MarketBoardWindow MainWindow { get; }

    /// <summary>
    /// Gets the windows currently torn off.
    /// </summary>
    public IReadOnlyList<DetachedBoardWindow> Detached => this.detached;

    /// <summary>
    /// Queues a list to be torn off into its own window.
    /// </summary>
    /// <param name="tab">The list to tear off.</param>
    public void Detach(ItemListTab tab)
    {
      if (this.detached.Any(w => w.Tab == tab) || this.pendingDetach.Contains(tab))
      {
        return;
      }

      this.pendingDetach.Add(tab);
    }

    /// <summary>
    /// Queues a list to go back into the main window.
    /// </summary>
    /// <param name="tab">The list to send home.</param>
    public void Reattach(ItemListTab tab)
    {
      if (this.pendingReattach.Contains(tab))
      {
        return;
      }

      this.pendingReattach.Add(tab);
    }

    /// <summary>
    /// Applies the queued detaches and reattaches. Call this after the window system has drawn.
    /// </summary>
    public void ApplyPendingChanges()
    {
      if (this.pendingDetach.Count == 0 && this.pendingReattach.Count == 0)
      {
        return;
      }

      foreach (var tab in this.pendingDetach)
      {
        this.DetachNow(tab, grabbed: true);
      }

      foreach (var tab in this.pendingReattach)
      {
        this.ReattachNow(tab);
      }

      this.pendingDetach.Clear();
      this.pendingReattach.Clear();
      this.SaveLayout();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      foreach (var window in this.detached)
      {
        this.windowSystem.RemoveWindow(window);
        window.Dispose();
      }

      this.detached.Clear();
      this.MainWindow.Dispose();
      this.Services.Dispose();
      this.isDisposed = true;
    }

    private void DetachNow(ItemListTab tab, bool grabbed)
    {
      var state = this.plugin.Config.DetachedBoards.FirstOrDefault(s => s.Tab == tab);

      if (state == null)
      {
        state = new DetachedBoardState { Tab = tab };
        this.plugin.Config.DetachedBoards.Add(state);
      }

      var context = new MarketBoardContext(
        this.Services,
        WorldSelection.ForDetachedBoard(this.plugin, state));

      var board = new MarketBoard(this.Services, context, isMainBoard: false)
      {
        Manager = this,
      };

      board.Tabs.Add(tab);
      context.ItemListTab = tab;

      var window = new DetachedBoardWindow(this.Services, board, tab, state, this.Reattach)
      {
        IsOpen = true,
        Grabbed = grabbed,
      };

      this.detached.Add(window);
      this.windowSystem.AddWindow(window);
      this.MainWindow.Board.Tabs.Remove(tab);
    }

    private void ReattachNow(ItemListTab tab)
    {
      var window = this.detached.FirstOrDefault(w => w.Tab == tab);

      if (window == null)
      {
        return;
      }

      this.detached.Remove(window);
      this.windowSystem.RemoveWindow(window);
      window.Dispose();

      this.plugin.Config.DetachedBoards.RemoveAll(s => s.Tab == tab);

      if (!this.MainWindow.Board.Tabs.Contains(tab))
      {
        this.MainWindow.Board.Tabs.Add(tab);
        this.SortMainTabs();
      }
    }

    private void SortMainTabs()
    {
      var ordered = this.MainWindow.Board.Tabs.OrderBy(t => (int)t).ToList();
      this.MainWindow.Board.Tabs.Clear();

      foreach (var tab in ordered)
      {
        this.MainWindow.Board.Tabs.Add(tab);
      }
    }

    private void SaveLayout()
    {
      this.plugin.PluginInterface.SavePluginConfig(this.plugin.Config);
    }
  }
}
