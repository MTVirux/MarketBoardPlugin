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
  /// The title bar button reporting which optional plugins and services MarketTerror is currently working with.
  /// </summary>
  public static class IntegrationsButton
  {
    private static readonly Vector4 OkColor = new(0.3f, 0.85f, 0.5f, 1.0f);

    private static readonly Vector4 IdleColor = new(0.45f, 0.45f, 0.45f, 1.0f);

    private static readonly Vector4 DownColor = new(0.9f, 0.35f, 0.3f, 1.0f);

    private static readonly Vector4 PartialColor = new(0.98f, 0.75f, 0.15f, 1.0f);

    private static readonly Vector4 AllGoodColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    private enum State
    {
      /// <summary>The integration is installed or answering.</summary>
      Ok,

      /// <summary>The integration is absent, or has not been contacted yet.</summary>
      Idle,

      /// <summary>The integration is expected to answer but did not.</summary>
      Down,
    }

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
        IconColor = AggregateColor(All(context)),

        // A read-only indicator, but Dalamud invokes Click unconditionally, so it cannot be null.
        Click = _ => { },
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

      button.IconColor = AggregateColor(All(context));
    }

    private static IReadOnlyList<Integration> All(MarketBoardContext context)
    {
      return
      [
        Lifestream(context.Plugin.IsLifestreamInstalled),
        Universalis(context.MarketData.IsUniversalisUp),
        Ffxivmt(context.MarketData.IsFFXIVMTUp),
      ];
    }

    private static Integration Lifestream(bool installed)
    {
      var detail = installed
        ? "Clicking a listing can travel to its world and open the Market Board there."
        : "Listing clicks stay where you are. Install Lifestream to travel to the\nlisting's world automatically.";

      return installed
        ? new Integration("Lifestream", State.Ok, "installed", detail)
        : new Integration("Lifestream", State.Idle, "not detected", detail);
    }

    private static Integration Universalis(bool up)
    {
      var detail = up
        ? "Listings and sale history come from Universalis as you browse."
        : "Listings and sale history cannot be fetched right now.\nCheck status.universalis.app.";

      return up
        ? new Integration("Universalis", State.Ok, "reachable", detail)
        : new Integration("Universalis", State.Down, "not answering", detail);
    }

    private static Integration Ffxivmt(bool? up)
    {
      if (up == null)
      {
        return new Integration(
          "FFXIVMT",
          State.Idle,
          "not contacted yet",
          "Gilflux rankings in the Stats tab are fetched once you select an item.");
      }

      return up.Value
        ? new Integration("FFXIVMT", State.Ok, "reachable", "Gilflux rankings in the Stats tab come from the FFXIVMT API.")
        : new Integration("FFXIVMT", State.Down, "not answering", "The last gilflux request failed, so the Stats tab has no rankings to show.");
    }

    /// <summary>
    /// White once everything is live so the button reads as ordinary; colour is spent only on what
    /// needs attention, unlike the per-integration text colour.
    /// </summary>
    /// <param name="integrations">The integrations to summarise.</param>
    /// <returns>The colour of the button icon.</returns>
    private static Vector4 AggregateColor(IReadOnlyList<Integration> integrations)
    {
      var ok = 0;

      foreach (var integration in integrations)
      {
        if (integration.State == State.Ok)
        {
          ok++;
        }
      }

      if (ok == 0)
      {
        return IdleColor;
      }

      return ok == integrations.Count ? AllGoodColor : PartialColor;
    }

    private static void DrawTooltip(MarketBoardContext context)
    {
      ImGui.BeginTooltip();

      ImGui.Text("Integrations");
      ImGui.Separator();

      foreach (var integration in All(context))
      {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(integration.Color, $"{(char)FontAwesomeIcon.Circle}");
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.TextColored(integration.Color, $"{integration.Name} - {integration.Status}");

        ImGui.Indent();
        ImGui.TextDisabled(integration.Detail);
        ImGui.Unindent();
      }

      ImGui.EndTooltip();
    }

    /// <summary>
    /// One optional plugin or service MarketTerror works with but never requires. The wording lives
    /// here so the tooltip and the settings window cannot drift apart.
    /// </summary>
    private readonly record struct Integration(string Name, State State, string Status, string Detail)
    {
      public Vector4 Color => this.State switch
      {
        State.Ok => OkColor,
        State.Down => DownColor,
        _ => IdleColor,
      };
    }
  }
}
