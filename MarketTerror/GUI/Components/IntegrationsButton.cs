// <copyright file="IntegrationsButton.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Dalamud.Interface.Windowing;

  /// <summary>
  /// The title bar button reporting which optional plugins MarketTerror is currently working with.
  /// </summary>
  public static class IntegrationsButton
  {
    private static readonly Vector4 ConnectedColor = new(0.3f, 0.85f, 0.5f, 1.0f);

    private static readonly Vector4 PartialColor = new(0.98f, 0.75f, 0.15f, 1.0f);

    private static readonly Vector4 AbsentColor = new(0.45f, 0.45f, 0.45f, 1.0f);

    private static readonly Vector4 AllGoodColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    /// <summary>
    /// Builds the integrations button.
    /// </summary>
    /// <param name="plugin">The plugin instance the integration states are read from.</param>
    /// <returns>The title bar button.</returns>
    public static TitleBarButton Build(MarketTerrorPlugin plugin)
    {
      ArgumentNullException.ThrowIfNull(plugin);

      return new TitleBarButton
      {
        Icon = FontAwesomeIcon.Link,
        IconOffset = new Vector2(2, 1),
        IconColor = AggregateColor(All(plugin)),

        // A read-only indicator, but Dalamud invokes Click unconditionally, so it cannot be null.
        Click = _ => { },
        ShowTooltip = () => DrawTooltip(plugin),
      };
    }

    /// <summary>
    /// Repaints the button for the current integration states.
    /// </summary>
    /// <remarks>
    /// IconColor is a property, not a callback, so the owning window has to refresh it from PreDraw,
    /// before ImGui lays the title bar out.
    /// </remarks>
    /// <param name="button">The button built by <see cref="Build"/>.</param>
    /// <param name="plugin">The plugin instance the integration states are read from.</param>
    public static void Refresh(TitleBarButton button, MarketTerrorPlugin plugin)
    {
      ArgumentNullException.ThrowIfNull(button);
      ArgumentNullException.ThrowIfNull(plugin);

      button.IconColor = AggregateColor(All(plugin));
    }

    private static IReadOnlyList<Integration> All(MarketTerrorPlugin plugin)
    {
      return [Lifestream(plugin.IsLifestreamInstalled)];
    }

    private static Integration Lifestream(bool installed)
    {
      var detail = installed
        ? "Clicking a listing can travel to its world and open the Market Board there."
        : "Listing clicks stay where you are. Install Lifestream to travel to the\nlisting's world automatically.";

      return new Integration("Lifestream", installed, installed ? "installed" : "not detected", detail);
    }

    /// <summary>
    /// White once every integration is present so the button reads as ordinary; colour is spent only on
    /// what needs attention, unlike the per-integration text colour.
    /// </summary>
    /// <param name="integrations">The integrations to summarise.</param>
    /// <returns>The colour of the button icon.</returns>
    private static Vector4 AggregateColor(IReadOnlyList<Integration> integrations)
    {
      var connected = 0;

      foreach (var integration in integrations)
      {
        if (integration.Connected)
        {
          connected++;
        }
      }

      if (connected == 0)
      {
        return AbsentColor;
      }

      return connected == integrations.Count ? AllGoodColor : PartialColor;
    }

    private static void DrawTooltip(MarketTerrorPlugin plugin)
    {
      ImGui.BeginTooltip();

      ImGui.Text("Plugin integrations");
      ImGui.Separator();

      foreach (var integration in All(plugin))
      {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(integration.Color, $"{(char)FontAwesomeIcon.Circle}");
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.TextColored(integration.Color, $"{integration.Name} - {integration.State}");

        ImGui.Indent();
        ImGui.TextDisabled(integration.Detail);
        ImGui.Unindent();
      }

      ImGui.EndTooltip();
    }

    /// <summary>
    /// One optional plugin MarketTerror works with but never requires. The wording lives here so the
    /// tooltip and the settings window cannot drift apart.
    /// </summary>
    private readonly record struct Integration(string Name, bool Connected, string State, string Detail)
    {
      public Vector4 Color => this.Connected ? ConnectedColor : AbsentColor;
    }
  }
}
