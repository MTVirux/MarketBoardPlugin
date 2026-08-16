// <copyright file="IntegrationsWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Dalamud.Interface.Windowing;
  using MarketTerror.Services;

  /// <summary>
  /// The window opened by the title bar link button, listing the optional plugins and services and
  /// letting the user dismiss the warnings they do not want to see.
  /// </summary>
  public class IntegrationsWindow : Window
  {
    private readonly MarketBoardContext context;

    private IDisposable? themeScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationsWindow"/> class.
    /// </summary>
    /// <param name="context">The shared market board state the integration states are read from.</param>
    public IntegrationsWindow(MarketBoardContext context)
      : base("Market Terror Integrations")
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));

      this.Size = new Vector2(460, 300);
      this.SizeCondition = ImGuiCond.FirstUseEver;
      this.SizeConstraints = new WindowSizeConstraints
      {
        MinimumSize = new Vector2(360, 200),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
      };
    }

    /// <inheritdoc/>
    public override void PreDraw()
    {
      this.context.Integrations.Update();
      this.themeScope = this.context.Theme.Push();
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
      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);
      ImGui.TextWrapped("MarketTerror works with these, but never requires them. Dismissing a warning hides it until that integration starts working and fails again.");
      ImGui.PopStyleColor();

      ImGui.Separator();
      ImGui.Spacing();

      foreach (var integration in this.context.Integrations.All)
      {
        this.DrawRow(integration);
      }
    }

    private static bool RightAlignedSmallButton(string label, string id)
    {
      SameLineRightAligned(ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2.0f));

      return ImGui.SmallButton($"{label}##{id}");
    }

    private static void RightAlignedDisabledText(string text)
    {
      SameLineRightAligned(ImGui.CalcTextSize(text).X);
      ImGui.TextDisabled(text);
    }

    private static void SameLineRightAligned(float width)
    {
      ImGui.SameLine(Math.Max(ImGui.GetWindowContentRegionMax().X - width, 0.0f));
    }

    private void DrawRow(Integration integration)
    {
      ImGui.PushFont(UiBuilder.IconFont);
      ImGui.TextColored(integration.Color, $"{(char)FontAwesomeIcon.Circle}");
      ImGui.PopFont();

      ImGui.SameLine();
      ImGui.TextColored(integration.Color, $"{integration.Name} - {integration.Status}");

      this.DrawAction(integration);

      ImGui.Indent();
      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);
      ImGui.TextWrapped(integration.Detail);
      ImGui.PopStyleColor();
      ImGui.Unindent();

      ImGui.Spacing();
    }

    private void DrawAction(Integration integration)
    {
      if (integration.Dismissed)
      {
        if (RightAlignedSmallButton("Restore", $"restore{integration.Name}"))
        {
          this.context.Integrations.Restore(integration);
        }

        return;
      }

      if (!integration.IsWarning)
      {
        return;
      }

      if (!integration.CanDismiss)
      {
        RightAlignedDisabledText("always shown");

        if (ImGui.IsItemHovered())
        {
          ImGui.SetTooltip("Every listing and sale in the plugin comes from Universalis, so this\nwarning cannot be hidden.");
        }

        return;
      }

      if (RightAlignedSmallButton("Dismiss", $"dismiss{integration.Name}"))
      {
        this.context.Integrations.Dismiss(integration);
      }
    }
  }
}
