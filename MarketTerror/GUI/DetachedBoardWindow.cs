// <copyright file="DetachedBoardWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface.Windowing;
  using MarketTerror.Models;

  /// <summary>
  /// One item list torn off the main window, as a board of its own.
  /// </summary>
  public sealed class DetachedBoardWindow : Window, IDisposable
  {
    private readonly BoardServices services;

    private readonly Action<ItemListTab> onClosed;

    private IDisposable? themeScope;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetachedBoardWindow"/> class.
    /// </summary>
    /// <param name="services">The services shared by every board.</param>
    /// <param name="board">The board this window hosts.</param>
    /// <param name="tab">The list this window is locked to.</param>
    /// <param name="state">The stored entry this window reads and writes.</param>
    /// <param name="onClosed">Called with the list when the window is closed, so it can go home.</param>
    public DetachedBoardWindow(
      BoardServices services,
      MarketBoard board,
      ItemListTab tab,
      DetachedBoardState state,
      Action<ItemListTab> onClosed)
      : base($"Market Terror - {ItemListTabNames.Label(tab)}")
    {
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.Board = board ?? throw new ArgumentNullException(nameof(board));
      this.State = state ?? throw new ArgumentNullException(nameof(state));
      this.onClosed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));
      this.Tab = tab;

      this.Flags = ImGuiWindowFlags.NoScrollbar;
      this.Size = new Vector2(700, 500);
      this.SizeCondition = ImGuiCond.FirstUseEver;
      this.SizeConstraints = new WindowSizeConstraints
      {
        MinimumSize = new Vector2(350, 225),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
      };
    }

    /// <summary>
    /// Gets the list this window is locked to.
    /// </summary>
    public ItemListTab Tab { get; }

    /// <summary>
    /// Gets the board this window hosts.
    /// </summary>
    public MarketBoard Board { get; }

    /// <summary>
    /// Gets the stored entry this window reads and writes.
    /// </summary>
    public DetachedBoardState State { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this window is still stuck to the cursor
    /// from the drag that tore it off.
    /// </summary>
    public bool Grabbed { get; set; }

    /// <summary>
    /// Gets or sets where in the window the cursor grabbed it.
    /// </summary>
    public Vector2 GrabOffset { get; set; }

    /// <summary>
    /// Gets a value indicating whether the user is dragging this window by its title bar.
    /// </summary>
    public bool IsBeingDragged { get; private set; }

    /// <inheritdoc/>
    public override void OnOpen()
    {
      this.IsBeingDragged = false;
    }

    /// <inheritdoc/>
    public override void PreDraw()
    {
      // ImGui has no way to hand a window an in-progress drag, so a freshly torn-off window
      // is walked under the cursor by hand until the button comes up.
      if (this.Grabbed)
      {
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
          ImGui.SetNextWindowPos(ImGui.GetMousePos() - this.GrabOffset);
        }
        else
        {
          this.Grabbed = false;
        }
      }

      this.themeScope = this.services.Theme.Push();
    }

    /// <inheritdoc/>
    public override void PostDraw()
    {
      this.themeScope?.Dispose();
      this.themeScope = null;
    }

    /// <inheritdoc/>
    public override void Draw()
    {
      this.IsBeingDragged = !this.Grabbed
        && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootWindow)
        && ImGui.IsMouseDragging(ImGuiMouseButton.Left)
        && !ImGui.IsAnyItemActive();

      using var fontDispose = this.services.DefaultFont.Push();

      this.Board.Draw();
    }

    /// <inheritdoc/>
    public override void OnClose()
    {
      this.onClosed(this.Tab);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.themeScope?.Dispose();
      this.themeScope = null;
      this.Board.Dispose();
      this.isDisposed = true;
    }
  }
}
