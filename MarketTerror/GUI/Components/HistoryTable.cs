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
      ImGui.Text("Sales history");
      this.context.TitleFont.Pop();

      var beforeSeparator = ImGui.GetCursorPosY();
      ImGui.Separator();
      var separatorHeight = ImGui.GetCursorPosY() - beforeSeparator;

      var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable
        | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

      // Leave room for the matching separator closing the table off.
      var tableHeight = sectionHeight - (ImGui.GetCursorPosY() - top) - separatorHeight;

      // Straddle the panel's padding so the table alone runs to its edges.
      var padding = ImGui.GetStyle().WindowPadding.X;
      var tableWidth = ImGui.GetContentRegionAvail().X + (padding * 2.0f);
      ImGui.SetCursorPosX(ImGui.GetCursorPosX() - padding);

      if (!ImGui.BeginTable("recentHistory", 6, flags, new Vector2(tableWidth, tableHeight)))
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
      DrawHeaders();
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

      ImGui.Separator();
    }

    /// <summary>
    /// The offset that centres text in the HQ column. Its left edge is flush with the table, so the
    /// drawable width is the cell plus the padding held back on the right.
    /// </summary>
    /// <param name="text">The text to centre.</param>
    /// <returns>The distance to move the cursor right by.</returns>
    private static float CenterOffset(string text)
    {
      var width = ImGui.GetContentRegionAvail().X + ImGui.GetStyle().CellPadding.X;

      return Math.Max((width - ImGui.CalcTextSize(text).X) * 0.5f, 0.0f);
    }

    private static void DrawHeaders()
    {
      ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

      ImGui.TableSetColumnIndex(0);
      ImGui.SetCursorPosX(ImGui.GetCursorPosX() + CenterOffset("HQ"));
      ImGui.TableHeader("HQ");

      for (var column = 1; column < ImGui.TableGetColumnCount(); column++)
      {
        ImGui.TableSetColumnIndex(column);
        ImGui.TableHeader(ImGui.TableGetColumnName(column));
      }
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

      var cursor = ImGui.GetCursorPos();
      var hqOffset = CenterOffset(SeIconChar.HighQuality.AsString());

      var clicked = ImGui.Selectable(
        $"##history{index}",
        this.context.SelectedHistory == index,
        ImGuiSelectableFlags.SpanAllColumns);

      if (history.Hq)
      {
        ImGui.SetCursorPos(new Vector2(cursor.X + hqOffset, cursor.Y));
        ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextBright);
        ImGui.Text(SeIconChar.HighQuality.AsString());
        ImGui.PopStyleColor();
      }

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
