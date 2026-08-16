// <copyright file="TerrorTheme.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Theme
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;

  /// <summary>
  /// The Terror skin: a scoped set of ImGui colours plus the named tokens the UI components draw with.
  /// </summary>
  /// <remarks>
  /// Push the skin from <see cref="Dalamud.Interface.Windowing.Window.PreDraw"/> and dispose it in
  /// <see cref="Dalamud.Interface.Windowing.Window.PostDraw"/>, so window colours are in place before
  /// ImGui begins the window and are removed before any other plugin draws.
  /// Every colour can be overridden from the configuration; unset colours fall back to the defaults here.
  /// </remarks>
  public sealed class TerrorTheme
  {
    private const int PushedColorCount = 31;

    private const int PushedStyleVarCount = 6;

    private const uint TransparentColor = 0u;

    private static readonly Dictionary<ThemeColor, uint> Defaults = new()
    {
      { ThemeColor.WindowBg, Rgb(0x0A, 0x0A, 0x0B) },
      { ThemeColor.PanelBg, Rgb(0x12, 0x12, 0x13) },
      { ThemeColor.FrameBg, Rgb(0x1A, 0x1A, 0x1C) },
      { ThemeColor.FrameBgHovered, Rgb(0x24, 0x1A, 0x1A) },
      { ThemeColor.FrameBgActive, Rgb(0x23, 0x0C, 0x0C) },
      { ThemeColor.Border, Rgb(0x69, 0x00, 0x00) },
      { ThemeColor.Accent, Rgb(0x74, 0x1C, 0x1C) },
      { ThemeColor.AccentDim, Rgb(0x4A, 0x13, 0x10) },
      { ThemeColor.AccentHover, Rgb(0x8A, 0x1E, 0x18) },
      { ThemeColor.RowAlt, Rgb(0x18, 0x18, 0x18) },
      { ThemeColor.TableHeaderBg, Rgb(0x08, 0x08, 0x09) },
      { ThemeColor.Text, Rgb(0xE8, 0xE6, 0xE3) },
      { ThemeColor.TextDim, Rgb(0x83, 0x80, 0x81) },
      { ThemeColor.TextBright, Rgb(0xF4, 0xF2, 0xEF) },
      { ThemeColor.GilText, Rgb(0xE0, 0xB3, 0x41) },
      { ThemeColor.Tab, Rgb(0x1C, 0x03, 0x03) },
      { ThemeColor.TabActive, Rgb(0x69, 0x00, 0x00) },
    };

    private readonly MarketTerrorConfig config;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerrorTheme"/> class.
    /// </summary>
    /// <param name="config">The plugin configuration the skin toggle and colour overrides are read from.</param>
    public TerrorTheme(MarketTerrorConfig config)
    {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Gets a value indicating whether the skin is currently applied.
    /// </summary>
    public bool Enabled => this.config.TerrorSkinEnabled;

    /// <summary>
    /// Gets the accent colour, used for selection and the active tab.
    /// </summary>
    public uint Accent => this.Resolve(ThemeColor.Accent, ImGuiCol.CheckMark);

    /// <summary>
    /// Gets the dimmed accent colour, used as the background of a selected row.
    /// </summary>
    public uint AccentDim => this.Resolve(ThemeColor.AccentDim, ImGuiCol.Header);

    /// <summary>
    /// Gets the hovered accent colour, used for hovered rows and tabs and the market data resize bar.
    /// </summary>
    public uint AccentHover => this.Resolve(ThemeColor.AccentHover, ImGuiCol.HeaderHovered);

    /// <summary>
    /// Gets the alternating table row colour.
    /// </summary>
    public uint RowAlt => this.Resolve(ThemeColor.RowAlt, ImGuiCol.TableRowBgAlt);

    /// <summary>
    /// Gets the body text colour.
    /// </summary>
    public uint Text => this.Resolve(ThemeColor.Text, ImGuiCol.Text);

    /// <summary>
    /// Gets the secondary text colour, used for column labels and captions.
    /// </summary>
    public uint TextDim => this.Resolve(ThemeColor.TextDim, ImGuiCol.TextDisabled);

    /// <summary>
    /// Gets the emphasised text colour, used for the selected item's name and the high quality marker.
    /// </summary>
    public uint TextBright => this.Resolve(ThemeColor.TextBright, ImGuiCol.Text);

    /// <summary>
    /// Gets the colour gil figures are drawn in.
    /// </summary>
    public uint GilText => this.Resolve(ThemeColor.GilText, ImGuiCol.Text);

    /// <summary>
    /// Gets the panel and table border colour.
    /// </summary>
    public uint Border => this.Resolve(ThemeColor.Border, ImGuiCol.Border);

    /// <summary>
    /// Gets the child panel background colour.
    /// </summary>
    public uint PanelBg => this.Resolve(ThemeColor.PanelBg, ImGuiCol.ChildBg);

    /// <summary>
    /// Gets every colour the skin defines, in display order.
    /// </summary>
    /// <returns>The theme colour keys.</returns>
    public static IReadOnlyList<ThemeColor> AllColors()
    {
      return new List<ThemeColor>(Defaults.Keys);
    }

    /// <summary>
    /// Gets the built-in default for a colour, ignoring any override.
    /// </summary>
    /// <param name="color">The colour to look up.</param>
    /// <returns>The packed default colour.</returns>
    public static uint GetDefault(ThemeColor color)
    {
      return Defaults.TryGetValue(color, out var value) ? value : 0xFF000000u;
    }

    /// <summary>
    /// Converts a packed colour to the vector form the ImGui colour pickers use.
    /// </summary>
    /// <param name="packed">The packed colour.</param>
    /// <returns>The colour as red, green, blue and alpha in the range 0 to 1.</returns>
    public static Vector4 ToVector(uint packed)
    {
      return new Vector4(
        (packed & 0xFF) / 255.0f,
        ((packed >> 8) & 0xFF) / 255.0f,
        ((packed >> 16) & 0xFF) / 255.0f,
        ((packed >> 24) & 0xFF) / 255.0f);
    }

    /// <summary>
    /// Converts a colour vector back to packed form.
    /// </summary>
    /// <param name="value">The colour as red, green, blue and alpha in the range 0 to 1.</param>
    /// <returns>The packed colour.</returns>
    public static uint FromVector(Vector4 value)
    {
      var r = (uint)Math.Clamp((int)(value.X * 255.0f), 0, 255);
      var g = (uint)Math.Clamp((int)(value.Y * 255.0f), 0, 255);
      var b = (uint)Math.Clamp((int)(value.Z * 255.0f), 0, 255);
      var a = (uint)Math.Clamp((int)(value.W * 255.0f), 0, 255);

      return (a << 24) | (b << 16) | (g << 8) | r;
    }

    /// <summary>
    /// Formats a packed colour as the hex string used in the editor.
    /// </summary>
    /// <param name="packed">The packed colour.</param>
    /// <returns>A six digit RGB hex string.</returns>
    public static string ToHex(uint packed)
    {
      return string.Create(
        CultureInfo.InvariantCulture,
        $"{packed & 0xFF:X2}{(packed >> 8) & 0xFF:X2}{(packed >> 16) & 0xFF:X2}");
    }

    /// <summary>
    /// Gets the current value of a colour, which is its override when one is set and its default otherwise.
    /// </summary>
    /// <param name="color">The colour to look up.</param>
    /// <returns>The packed colour.</returns>
    public uint Get(ThemeColor color)
    {
      return this.config.ThemeOverrides.TryGetValue(color.ToString(), out var value)
        ? value
        : GetDefault(color);
    }

    /// <summary>
    /// Checks whether a colour has been changed from its default.
    /// </summary>
    /// <param name="color">The colour to check.</param>
    /// <returns>True when an override is set.</returns>
    public bool IsOverridden(ThemeColor color)
    {
      return this.config.ThemeOverrides.ContainsKey(color.ToString());
    }

    /// <summary>
    /// Overrides a colour. Setting a colour back to its default removes the override instead of storing it.
    /// </summary>
    /// <param name="color">The colour to change.</param>
    /// <param name="value">The packed colour to use.</param>
    public void Set(ThemeColor color, uint value)
    {
      if (value == GetDefault(color))
      {
        this.config.ThemeOverrides.Remove(color.ToString());
        return;
      }

      this.config.ThemeOverrides[color.ToString()] = value;
    }

    /// <summary>
    /// Restores a single colour to its default.
    /// </summary>
    /// <param name="color">The colour to reset.</param>
    public void Reset(ThemeColor color)
    {
      this.config.ThemeOverrides.Remove(color.ToString());
    }

    /// <summary>
    /// Restores every colour to its default.
    /// </summary>
    public void ResetAll()
    {
      this.config.ThemeOverrides.Clear();
    }

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

      ImGui.PushStyleColor(ImGuiCol.Text, this.Get(ThemeColor.Text));
      ImGui.PushStyleColor(ImGuiCol.TextDisabled, this.Get(ThemeColor.TextDim));
      ImGui.PushStyleColor(ImGuiCol.WindowBg, this.Get(ThemeColor.WindowBg));
      ImGui.PushStyleColor(ImGuiCol.ChildBg, this.Get(ThemeColor.PanelBg));
      ImGui.PushStyleColor(ImGuiCol.PopupBg, this.Get(ThemeColor.PanelBg));
      ImGui.PushStyleColor(ImGuiCol.Border, this.Get(ThemeColor.Border));
      ImGui.PushStyleColor(ImGuiCol.FrameBg, this.Get(ThemeColor.FrameBg));
      ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, this.Get(ThemeColor.FrameBgHovered));
      ImGui.PushStyleColor(ImGuiCol.FrameBgActive, this.Get(ThemeColor.FrameBgActive));
      ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, this.Get(ThemeColor.WindowBg));
      ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, this.Get(ThemeColor.FrameBgActive));
      ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, this.Get(ThemeColor.AccentHover));
      ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, this.Get(ThemeColor.Accent));
      ImGui.PushStyleColor(ImGuiCol.CheckMark, this.Get(ThemeColor.Accent));
      ImGui.PushStyleColor(ImGuiCol.SliderGrab, this.Get(ThemeColor.AccentDim));
      ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, this.Get(ThemeColor.Accent));
      ImGui.PushStyleColor(ImGuiCol.Button, this.Get(ThemeColor.FrameBg));
      ImGui.PushStyleColor(ImGuiCol.ButtonHovered, this.Get(ThemeColor.FrameBgActive));
      ImGui.PushStyleColor(ImGuiCol.ButtonActive, this.Get(ThemeColor.AccentDim));
      ImGui.PushStyleColor(ImGuiCol.Header, this.Get(ThemeColor.AccentDim));
      ImGui.PushStyleColor(ImGuiCol.HeaderHovered, this.Get(ThemeColor.AccentHover));
      ImGui.PushStyleColor(ImGuiCol.HeaderActive, this.Get(ThemeColor.Accent));
      ImGui.PushStyleColor(ImGuiCol.Separator, this.Get(ThemeColor.Border));
      ImGui.PushStyleColor(ImGuiCol.Tab, this.Get(ThemeColor.Tab));
      ImGui.PushStyleColor(ImGuiCol.TabHovered, this.Get(ThemeColor.AccentHover));
      ImGui.PushStyleColor(ImGuiCol.TabActive, this.Get(ThemeColor.TabActive));
      ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, this.Get(ThemeColor.TableHeaderBg));
      ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, this.Get(ThemeColor.Border));
      ImGui.PushStyleColor(ImGuiCol.TableBorderLight, this.Get(ThemeColor.Border));
      ImGui.PushStyleColor(ImGuiCol.TableRowBg, TransparentColor);
      ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, this.Get(ThemeColor.RowAlt));

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

    private uint Resolve(ThemeColor color, ImGuiCol fallback)
    {
      return this.Enabled ? this.Get(color) : ImGui.GetColorU32(fallback);
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
