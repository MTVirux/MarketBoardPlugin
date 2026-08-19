// <copyright file="ModalDim.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Theme
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;

  /// <summary>
  /// Puts the skin's wash behind the plugin's modal boxes.
  /// </summary>
  /// <remarks>
  /// ImGui reads <see cref="ImGuiCol.ModalWindowDimBg"/> when it ends the frame, long after every
  /// window has popped the style <see cref="TerrorTheme.Push"/> put on the stack, so this is the one
  /// colour the skin cannot push. It has to be written to the shared style and left there until the
  /// frame ends, so it is only borrowed while one of the plugin's own modals is on screen and given
  /// back the first frame that stops being true.
  /// </remarks>
  public static class ModalDim
  {
    private static Vector4 borrowed;

    private static bool lent;

    private static bool wanted;

    /// <summary>
    /// Marks that one of the plugin's modals is on screen this frame.
    /// </summary>
    /// <remarks>Call right after the BeginPopupModal that opened it.</remarks>
    public static void Mark()
    {
      wanted = true;
    }

    /// <summary>
    /// Lends the shared style the skin's wash for the rest of the frame, or gives back the colour
    /// that was there before when no modal wants it.
    /// </summary>
    /// <param name="theme">The theme to take the wash from.</param>
    /// <remarks>Call once a frame from the draw handler, after every window has drawn.</remarks>
    public static void Apply(TerrorTheme theme)
    {
      ArgumentNullException.ThrowIfNull(theme);

      var take = wanted && theme.Enabled;

      wanted = false;

      var style = ImGui.GetStyle();

      if (take)
      {
        if (!lent)
        {
          borrowed = style.Colors[(int)ImGuiCol.ModalWindowDimBg];
          lent = true;
        }

        style.Colors[(int)ImGuiCol.ModalWindowDimBg] = TerrorTheme.ToVector(theme.Get(ThemeColor.ModalDim));
      }
      else if (lent)
      {
        style.Colors[(int)ImGuiCol.ModalWindowDimBg] = borrowed;
        lent = false;
      }
    }
  }
}
