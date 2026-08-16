// <copyright file="HistoryTable.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Globalization;
  using System.Linq;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Game.Text;
  using MarketTerror.Extensions;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// The table of recent sales for the selected item.
  /// </summary>
  public sealed class HistoryTable
  {
    /// <summary>How much of the table's width the line under the heading covers.</summary>
    private const float HeadingUnderlineWidth = 0.95f;

    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryTable"/> class.
    /// </summary>
    /// <param name="context">The state and services shared by every component.</param>
    public HistoryTable(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the recent history heading and table.
    /// </summary>
    /// <param name="sectionHeight">The height of the heading and table together, as computed by the window.</param>
    public void Draw(float sectionHeight)
    {
      var top = ImGui.GetCursorPosY();

      this.context.TitleFont.Push();
      ImGui.Text("Recent history");
      this.context.TitleFont.Pop();

      DrawHeadingUnderline(ImGui.GetContentRegionAvail().X * HeadingUnderlineWidth);

      var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable
        | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
      var tableHeight = sectionHeight - (ImGui.GetCursorPosY() - top);

      if (!ImGui.BeginTable("recentHistory", 6, flags, new Vector2(0.0f, tableHeight)))
      {
        return;
      }

      ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeightWithSpacing() * 1.5f);
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("Qty");
      ImGui.TableSetupColumn("Total");
      ImGui.TableSetupColumn("Date");
      ImGui.TableSetupColumn("Buyer");

      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);
      ImGui.TableHeadersRow();
      ImGui.PopStyleColor();

      var historySnapshot = this.context.MarketData.MarketData?.RecentHistory.ToArray();
      var marketDataRecentHistory = historySnapshot?.OrderByDescending(h => h.Timestamp).ToList();

      if (marketDataRecentHistory != null)
      {
        for (var index = 0; index < marketDataRecentHistory.Count; index++)
        {
          this.DrawRow(marketDataRecentHistory[index], index);
        }
      }

      ImGui.EndTable();
    }

    private static void DrawHeadingUnderline(float width)
    {
      var available = ImGui.GetContentRegionAvail().X;
      var start = ImGui.GetCursorScreenPos() + new Vector2((available - width) * 0.5f, 0.0f);

      ImGui.GetWindowDrawList().AddLine(
        start,
        start + new Vector2(width, 0.0f),
        ImGui.GetColorU32(ImGuiCol.Separator));

      ImGui.Dummy(new Vector2(available, 1.0f));
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

    private void DrawGil(double value)
    {
      var text = this.context.Config.PriceIconShown
        ? value.ToString("C", this.context.Plugin.NumberFormatInfo)
        : value.ToString("N0", CultureInfo.CurrentCulture);

      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.GilText);
      DrawRightAligned(text);
      ImGui.PopStyleColor();
    }

    private void DrawRow(MarketDataRecentHistory history, int index)
    {
      var selectedWorld = this.context.Worlds.SelectedIndex;

      ImGui.TableNextRow();
      ImGui.TableSetColumnIndex(0);

      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.Accent);
      var clicked = ImGui.Selectable(
        $"{(history.Hq ? SeIconChar.HighQuality.AsString() : string.Empty)}##history{index}",
        this.context.SelectedHistory == index,
        ImGuiSelectableFlags.SpanAllColumns);
      ImGui.PopStyleColor();

      if (clicked)
      {
        this.context.SelectedHistory = index;
      }

      ImGui.TableSetColumnIndex(1);
      this.DrawGil(history.PricePerUnit);

      ImGui.TableSetColumnIndex(2);
      DrawRightAligned($"{history.Quantity:##,###}");

      ImGui.TableSetColumnIndex(3);
      this.DrawGil(history.Total);

      ImGui.TableSetColumnIndex(4);
      ImGui.Text($"{DateTimeOffset.FromUnixTimeSeconds(history.Timestamp).LocalDateTime:G}");

      ImGui.TableSetColumnIndex(5);
      ImGui.Text(
        $"{history.BuyerName} {SeIconChar.CrossWorld.ToChar()} {(selectedWorld <= 1 ? history.WorldName : this.context.Worlds.Worlds[selectedWorld].Query)}");
    }
  }
}
