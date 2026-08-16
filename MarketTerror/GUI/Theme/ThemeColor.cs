// <copyright file="ThemeColor.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Theme
{
  /// <summary>
  /// The named colours the Terror skin is built from.
  /// </summary>
  public enum ThemeColor
  {
    /// <summary>The window background.</summary>
    WindowBg,

    /// <summary>The background of child panels.</summary>
    PanelBg,

    /// <summary>The background of inputs, combos and buttons.</summary>
    FrameBg,

    /// <summary>The background of a hovered input or button.</summary>
    FrameBgHovered,

    /// <summary>The background of an active input or button.</summary>
    FrameBgActive,

    /// <summary>The title bar of an unfocused window.</summary>
    TitleBg,

    /// <summary>The title bar of the focused window.</summary>
    TitleBgActive,

    /// <summary>Panel and table borders.</summary>
    Border,

    /// <summary>Selection, the active tab underline and the high quality marker.</summary>
    Accent,

    /// <summary>The background of a selected row.</summary>
    AccentDim,

    /// <summary>The background of a hovered row.</summary>
    AccentHover,

    /// <summary>The alternating table row background.</summary>
    RowAlt,

    /// <summary>Body text.</summary>
    Text,

    /// <summary>Column labels and secondary text.</summary>
    TextDim,

    /// <summary>The selected item's name.</summary>
    TextBright,

    /// <summary>Gil figures.</summary>
    GilText,

    /// <summary>An inactive tab.</summary>
    Tab,

    /// <summary>The active tab.</summary>
    TabActive,
  }
}
