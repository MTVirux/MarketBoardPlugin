// <copyright file="TerrorTheme.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Theme
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;

  /// <summary>
  /// The Terror skin: a scoped set of ImGui colours plus the named tokens the UI components draw with.
  /// </summary>
  /// <remarks>
  /// Push the skin from <see cref="Dalamud.Interface.Windowing.Window.PreDraw"/> and dispose it in
  /// <see cref="Dalamud.Interface.Windowing.Window.PostDraw"/>, so window and title bar colours are in
  /// place before ImGui begins the window and are removed before any other plugin draws.
  /// </remarks>
  public sealed class TerrorTheme
  {
    private const int PushedColorCount = 34;

    private const int PushedStyleVarCount = 6;

    private const uint TransparentColor = 0u;

    private static readonly uint WindowBgColor = Rgb(0x0A, 0x0A, 0x0B);
    private static readonly uint PanelBgColor = Rgb(0x12, 0x12, 0x13);
    private static readonly uint FrameBgColor = Rgb(0x1A, 0x1A, 0x1C);
    private static readonly uint FrameBgHoveredColor = Rgb(0x24, 0x1A, 0x1A);
    private static readonly uint FrameBgActiveColor = Rgb(0x2E, 0x20, 0x20);
    private static readonly uint BorderColor = Rgb(0x3A, 0x2C, 0x2C);
    private static readonly uint AccentColor = Rgb(0xB3, 0x27, 0x1F);
    private static readonly uint AccentDimColor = Rgb(0x4A, 0x13, 0x10);
    private static readonly uint AccentHoverColor = Rgb(0x8A, 0x1E, 0x18);
    private static readonly uint RowAltColor = Rgb(0x0E, 0x0E, 0x0F);
    private static readonly uint TextColor = Rgb(0xE8, 0xE6, 0xE3);
    private static readonly uint TextDimColor = Rgb(0x83, 0x80, 0x81);
    private static readonly uint TextBrightColor = Rgb(0xF4, 0xF2, 0xEF);
    private static readonly uint GilTextColor = Rgb(0xE0, 0xB3, 0x41);
    private static readonly uint TabColor = Rgb(0x1A, 0x1A, 0x1C);
    private static readonly uint TabActiveColor = Rgb(0x24, 0x1A, 0x1A);

    private readonly MarketTerrorConfig config;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerrorTheme"/> class.
    /// </summary>
    /// <param name="config">The plugin configuration the skin toggle is read from.</param>
    public TerrorTheme(MarketTerrorConfig config)
    {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Gets a value indicating whether the skin is currently applied.
    /// </summary>
    public bool Enabled => this.config.TerrorSkinEnabled;

    /// <summary>
    /// Gets the accent colour, used for selection, the active tab and the high quality marker.
    /// </summary>
    public uint Accent => this.Enabled ? AccentColor : ImGui.GetColorU32(ImGuiCol.CheckMark);

    /// <summary>
    /// Gets the dimmed accent colour, used as the background of a selected row.
    /// </summary>
    public uint AccentDim => this.Enabled ? AccentDimColor : ImGui.GetColorU32(ImGuiCol.Header);

    /// <summary>
    /// Gets the alternating table row colour.
    /// </summary>
    public uint RowAlt => this.Enabled ? RowAltColor : ImGui.GetColorU32(ImGuiCol.TableRowBgAlt);

    /// <summary>
    /// Gets the body text colour.
    /// </summary>
    public uint Text => this.Enabled ? TextColor : ImGui.GetColorU32(ImGuiCol.Text);

    /// <summary>
    /// Gets the secondary text colour, used for column labels and captions.
    /// </summary>
    public uint TextDim => this.Enabled ? TextDimColor : ImGui.GetColorU32(ImGuiCol.TextDisabled);

    /// <summary>
    /// Gets the emphasised text colour, used for the selected item's name.
    /// </summary>
    public uint TextBright => this.Enabled ? TextBrightColor : ImGui.GetColorU32(ImGuiCol.Text);

    /// <summary>
    /// Gets the colour gil figures are drawn in.
    /// </summary>
    public uint GilText => this.Enabled ? GilTextColor : ImGui.GetColorU32(ImGuiCol.Text);

    /// <summary>
    /// Gets the panel and table border colour.
    /// </summary>
    public uint Border => this.Enabled ? BorderColor : ImGui.GetColorU32(ImGuiCol.Border);

    /// <summary>
    /// Gets the child panel background colour.
    /// </summary>
    public uint PanelBg => this.Enabled ? PanelBgColor : ImGui.GetColorU32(ImGuiCol.ChildBg);

    /// <summary>
    /// Applies the skin until the returned handle is disposed.
    /// </summary>
    /// <returns>A handle that restores the previous style when disposed.</returns>
    public IDisposable Push()
    {
      if (!this.Enabled)
      {
        return NullScope.Instance;
      }

      ImGui.PushStyleColor(ImGuiCol.Text, TextColor);
      ImGui.PushStyleColor(ImGuiCol.TextDisabled, TextDimColor);
      ImGui.PushStyleColor(ImGuiCol.WindowBg, WindowBgColor);
      ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelBgColor);
      ImGui.PushStyleColor(ImGuiCol.PopupBg, PanelBgColor);
      ImGui.PushStyleColor(ImGuiCol.Border, BorderColor);
      ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBgColor);
      ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, FrameBgHoveredColor);
      ImGui.PushStyleColor(ImGuiCol.FrameBgActive, FrameBgActiveColor);
      ImGui.PushStyleColor(ImGuiCol.TitleBg, PanelBgColor);
      ImGui.PushStyleColor(ImGuiCol.TitleBgActive, FrameBgActiveColor);
      ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, PanelBgColor);
      ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, WindowBgColor);
      ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, FrameBgActiveColor);
      ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, AccentHoverColor);
      ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, AccentColor);
      ImGui.PushStyleColor(ImGuiCol.CheckMark, AccentColor);
      ImGui.PushStyleColor(ImGuiCol.SliderGrab, AccentDimColor);
      ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, AccentColor);
      ImGui.PushStyleColor(ImGuiCol.Button, FrameBgColor);
      ImGui.PushStyleColor(ImGuiCol.ButtonHovered, FrameBgActiveColor);
      ImGui.PushStyleColor(ImGuiCol.ButtonActive, AccentDimColor);
      ImGui.PushStyleColor(ImGuiCol.Header, AccentDimColor);
      ImGui.PushStyleColor(ImGuiCol.HeaderHovered, AccentHoverColor);
      ImGui.PushStyleColor(ImGuiCol.HeaderActive, AccentColor);
      ImGui.PushStyleColor(ImGuiCol.Separator, BorderColor);
      ImGui.PushStyleColor(ImGuiCol.Tab, TabColor);
      ImGui.PushStyleColor(ImGuiCol.TabHovered, AccentHoverColor);
      ImGui.PushStyleColor(ImGuiCol.TabActive, TabActiveColor);
      ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, PanelBgColor);
      ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, BorderColor);
      ImGui.PushStyleColor(ImGuiCol.TableBorderLight, BorderColor);
      ImGui.PushStyleColor(ImGuiCol.TableRowBg, TransparentColor);
      ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, RowAltColor);

      ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4.0f);
      ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4.0f);
      ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3.0f);
      ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 3.0f);
      ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1.0f);
      ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(6.0f, 3.0f));

      return new ThemeScope();
    }

    private static uint Rgb(byte r, byte g, byte b)
    {
      return 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;
    }

    private sealed class ThemeScope : IDisposable
    {
      private bool disposed;

      public void Dispose()
      {
        if (this.disposed)
        {
          return;
        }

        ImGui.PopStyleVar(PushedStyleVarCount);
        ImGui.PopStyleColor(PushedColorCount);
        this.disposed = true;
      }
    }

    private sealed class NullScope : IDisposable
    {
      public static readonly NullScope Instance = new();

      public void Dispose()
      {
      }
    }
  }
}
