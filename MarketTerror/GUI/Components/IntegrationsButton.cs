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
  using MarketTerror.Services;

  /// <summary>
  /// The title bar button reporting which optional plugins and services MarketTerror is currently working with.
  /// </summary>
  public static class IntegrationsButton
  {
    private static readonly Vector4 IdleColor = new(0.45f, 0.45f, 0.45f, 1.0f);

    private static readonly Vector4 PartialColor = new(0.98f, 0.75f, 0.15f, 1.0f);

    private static readonly Vector4 AllGoodColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    /// <summary>
    /// Builds the integrations button.
    /// </summary>
    /// <param name="context">The shared market board state the integration states are read from.</param>
    /// <returns>The title bar button.</returns>
    public static TitleBarButton Build(MarketBoardContext context)
    {
      ArgumentNullException.ThrowIfNull(context);

      return new TitleBarButton
      {
        Icon = FontAwesomeIcon.Link,
        IconOffset = new Vector2(2, 1),
        IconColor = AggregateColor(context.Integrations.All),
        Click = _ => context.Plugin.OpenIntegrations(),
        ShowTooltip = () => DrawTooltip(context),
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
    /// <param name="context">The shared market board state the integration states are read from.</param>
    public static void Refresh(TitleBarButton button, MarketBoardContext context)
    {
      ArgumentNullException.ThrowIfNull(button);
      ArgumentNullException.ThrowIfNull(context);

      context.Integrations.Update();
      button.IconColor = AggregateColor(context.Integrations.All);
    }

    /// <summary>
    /// White once nothing needs attention so the button reads as ordinary; colour is spent only on
    /// what does, unlike the per-integration text colour. A dismissed warning counts as settled.
    /// </summary>
    /// <param name="integrations">The integrations to summarise.</param>
    /// <returns>The colour of the button icon.</returns>
    private static Vector4 AggregateColor(IReadOnlyList<Integration> integrations)
    {
      var settled = 0;

      foreach (var integration in integrations)
      {
        if (!integration.IsWarning || integration.Dismissed)
        {
          settled++;
        }
      }

      if (settled == 0)
      {
        return IdleColor;
      }

      return settled == integrations.Count ? AllGoodColor : PartialColor;
    }

    private static void DrawTooltip(MarketBoardContext context)
    {
      ImGui.BeginTooltip();

      ImGui.Text("Integrations");
      ImGui.Separator();

      foreach (var integration in context.Integrations.All)
      {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(integration.Color, $"{(char)FontAwesomeIcon.Circle}");
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.TextColored(integration.Color, $"{integration.Name} - {integration.Status}");

        if (integration.Dismissed)
        {
          ImGui.SameLine();
          ImGui.TextDisabled("(dismissed)");
        }

        ImGui.Indent();
        ImGui.TextDisabled(integration.Detail);
        ImGui.Unindent();
      }

      ImGui.Separator();
      ImGui.TextDisabled("Click to manage integrations.");

      ImGui.EndTooltip();
    }
  }
}
