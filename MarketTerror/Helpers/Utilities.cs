// <copyright file="Utilities.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Helpers
{
  using System;
  using System.Diagnostics;
  using System.Numerics;
  using System.Runtime.CompilerServices;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface.Utility.Raii;

  /// <summary>
  /// Utilities.
  /// </summary>
  internal static class Utilities
  {
    public static bool Checkbox(string label, string description, bool current, Action<bool> setter, ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
    {
      var tmp = current;
      var result = ImGui.Checkbox(label, ref tmp);
      HoverTooltip(description, flags);
      if (!result || tmp == current)
      {
        return false;
      }

      setter(tmp);
      return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void HoverTooltip(string tooltip, ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
    {
      if (tooltip.Length > 0 && ImGui.IsItemHovered(flags))
      {
        using var tt = ImRaii.Tooltip();
        ImGui.TextUnformatted(tooltip);
      }
    }

    /// <summary>
    /// Draws text with every letter on its own point of a slowly turning rainbow.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    public static void RainbowText(string text)
    {
      var spacing = ImGui.GetStyle().ItemSpacing;
      var offset = (float)ImGui.GetTime() * 0.35f;

      ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, spacing.Y));

      for (var i = 0; i < text.Length; i++)
      {
        ImGui.TextColored(Hue(offset + (i / 12f)), text[i].ToString());

        if (i < text.Length - 1)
        {
          ImGui.SameLine();
        }
      }

      ImGui.PopStyleVar();
    }

    internal static void OpenBrowser(string url)
    {
      Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    /// <summary>
    /// Turns a hue into a fully saturated colour.
    /// </summary>
    /// <param name="hue">The hue, where 0 and 1 are both red.</param>
    /// <returns>The colour of that hue.</returns>
    private static Vector4 Hue(float hue)
    {
      hue -= MathF.Floor(hue);

      var r = Math.Clamp(MathF.Abs((hue * 6f) - 3f) - 1f, 0f, 1f);
      var g = Math.Clamp(2f - MathF.Abs((hue * 6f) - 2f), 0f, 1f);
      var b = Math.Clamp(2f - MathF.Abs((hue * 6f) - 4f), 0f, 1f);

      return new Vector4(r, g, b, 1f);
    }
  }
}
