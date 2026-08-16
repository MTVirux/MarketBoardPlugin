// <copyright file="ThemeEditorWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Numerics;
  using System.Text;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Game.Text;
  using Dalamud.Interface.Windowing;
  using MarketTerror.Extensions;
  using MarketTerror.GUI.Theme;

  /// <summary>
  /// The live editor for the Terror skin colours.
  /// </summary>
  public class ThemeEditorWindow : Window
  {
    private const float HexColumnOffset = 190.0f;

    private const float RevertColumnOffset = 250.0f;

    private const float FooterLines = 7.0f;

    private const int SelectedPreviewRow = 1;

    private static readonly (string Heading, ThemeColor[] Colors)[] Sections =
    {
      ("Surfaces", new[] { ThemeColor.WindowBg, ThemeColor.PanelBg, ThemeColor.FrameBg, ThemeColor.FrameBgHovered, ThemeColor.FrameBgActive, ThemeColor.Border }),
      ("Title bar", new[] { ThemeColor.TitleBg, ThemeColor.TitleBgActive }),
      ("Accent", new[] { ThemeColor.Accent, ThemeColor.AccentDim, ThemeColor.AccentHover }),
      ("Tabs", new[] { ThemeColor.Tab, ThemeColor.TabActive }),
      ("Text", new[] { ThemeColor.Text, ThemeColor.TextDim, ThemeColor.TextBright, ThemeColor.GilText }),
      ("Tables", new[] { ThemeColor.RowAlt }),
    };

    private static readonly (bool Hq, string Price, string Quantity, string Retainer)[] PreviewRows =
    {
      (false, "8,900", "3", "Kiri"),
      (true, "12,400", "1", "Mogsworth"),
      (false, "13,750", "12", "Bellwether"),
    };

    private readonly TerrorTheme theme;

    private IDisposable? themeScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeEditorWindow"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public ThemeEditorWindow(MarketTerrorPlugin plugin)
      : base("Market Terror Theme")
    {
      this.Size = new Vector2(420, 560);
      this.SizeCondition = ImGuiCond.FirstUseEver;
      this.SizeConstraints = new WindowSizeConstraints
      {
        MinimumSize = new Vector2(340, 320),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
      };

      this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.theme = new TerrorTheme(this.Plugin.Config);
    }

    private MarketTerrorPlugin Plugin { get; init; }

    /// <inheritdoc/>
    public override void PreDraw()
    {
      this.themeScope = this.theme.Push();
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
      if (!this.Plugin.Config.TerrorSkinEnabled)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
        ImGui.TextWrapped("The Terror skin is off, so these colours only show in the preview below.");
        ImGui.PopStyleColor();

        ImGui.SameLine();

        if (ImGui.SmallButton("Turn it on"))
        {
          this.Plugin.Config.TerrorSkinEnabled = true;
          this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
        }

        ImGui.Separator();
      }

      var footerHeight = (ImGui.GetTextLineHeightWithSpacing() * FooterLines) + ImGui.GetFrameHeightWithSpacing();

      ImGui.BeginChild("themeColors", new Vector2(0.0f, -footerHeight), false);

      foreach (var section in Sections)
      {
        this.SectionHeading(section.Heading);

        foreach (var color in section.Colors)
        {
          this.DrawColorRow(color);
        }

        ImGui.NewLine();
      }

      ImGui.EndChild();

      this.DrawPreview();

      ImGui.Spacing();

      if (ImGui.Button("Reset all to defaults##themeResetAll"))
      {
        this.theme.ResetAll();
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }
    }

    private static string Humanize(string name)
    {
      var builder = new StringBuilder(name.Length + 4);

      for (var index = 0; index < name.Length; index++)
      {
        if (index > 0 && char.IsUpper(name[index]))
        {
          builder.Append(' ');
        }

        builder.Append(name[index]);
      }

      return builder.ToString();
    }

    private static void DrawRightAligned(string text)
    {
      var offset = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X;

      if (offset > 0.0f)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
      }

      ImGui.Text(text);
    }

    private void SectionHeading(string label)
    {
      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.Text(label);
      ImGui.PopStyleColor();
      ImGui.Separator();
    }

    private void DrawColorRow(ThemeColor color)
    {
      var scale = ImGui.GetIO().FontGlobalScale;
      var name = color.ToString();
      var value = TerrorTheme.ToVector(this.theme.Get(color));

      var edited = ImGui.ColorEdit4(
        $"{Humanize(name)}##color{name}",
        ref value,
        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreview);

      if (edited)
      {
        this.theme.Set(color, TerrorTheme.FromVector(value));
      }

      if (ImGui.IsItemDeactivatedAfterEdit())
      {
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }

      ImGui.SameLine(HexColumnOffset * scale);
      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.Text(TerrorTheme.ToHex(this.theme.Get(color)));
      ImGui.PopStyleColor();

      if (!this.theme.IsOverridden(color))
      {
        return;
      }

      ImGui.SameLine(RevertColumnOffset * scale);

      if (ImGui.SmallButton($"Revert##revert{name}"))
      {
        this.theme.Reset(color);
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }
    }

    private void DrawPreview()
    {
      this.SectionHeading("Preview");

      var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV;

      if (!ImGui.BeginTable("themePreview", 4, flags))
      {
        return;
      }

      ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeightWithSpacing() * 1.5f);
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("Qty");
      ImGui.TableSetupColumn("Retainer");

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.Get(ThemeColor.TextDim));
      ImGui.TableHeadersRow();
      ImGui.PopStyleColor();

      for (var index = 0; index < PreviewRows.Length; index++)
      {
        var row = PreviewRows[index];
        this.DrawPreviewRow(row.Hq, row.Price, row.Quantity, row.Retainer, index == SelectedPreviewRow);
      }

      ImGui.EndTable();
    }

    private void DrawPreviewRow(bool hq, string price, string quantity, string retainer, bool selected)
    {
      ImGui.TableNextRow();

      if (selected)
      {
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, this.theme.Get(ThemeColor.AccentDim));
      }

      ImGui.TableSetColumnIndex(0);

      if (hq)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, this.theme.Get(ThemeColor.Accent));
        ImGui.Text(SeIconChar.HighQuality.AsString());
        ImGui.PopStyleColor();
      }

      ImGui.TableSetColumnIndex(1);
      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.Get(ThemeColor.GilText));
      DrawRightAligned(price);
      ImGui.PopStyleColor();

      ImGui.TableSetColumnIndex(2);
      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.Get(ThemeColor.Text));
      DrawRightAligned(quantity);
      ImGui.PopStyleColor();

      ImGui.TableSetColumnIndex(3);
      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.Get(ThemeColor.Text));
      ImGui.Text(retainer);
      ImGui.PopStyleColor();
    }
  }
}
