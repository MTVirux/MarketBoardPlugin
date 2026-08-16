// <copyright file="StatsPanel.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Bindings.ImPlot;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// Draws the body of the Stats tab: the KPI grid, the price and volume plots, the stack size
  /// histogram and the FFXIVMT gilflux rankings.
  /// </summary>
  public sealed class StatsPanel
  {
    private const ImGuiTableFlags GridFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame;

    private static readonly char[] SpinnerFrames = ['|', '/', '-', '\\'];

    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatsPanel"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public StatsPanel(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the stats sections for the selected item.
    /// </summary>
    public void Draw()
    {
      this.context.TitleFont.Push();
      ImGui.Text("Stats");
      this.context.TitleFont.Pop();

      var marketData = this.context.MarketData.MarketData;

      if (marketData == null)
      {
        ImGui.Text("No market data available.");
        return;
      }

      var queryTarget = this.context.Worlds.QueryTarget;

      ImGui.SetNextItemOpen(this.context.OpenStatsSection == 0, ImGuiCond.Always);
      if (ImGui.CollapsingHeader($"KPIs ({queryTarget})##statsKpisHeader"))
      {
        this.context.OpenStatsSection = 0;
        this.DrawKpis(marketData);
      }
      else if (this.context.OpenStatsSection == 0)
      {
        this.context.OpenStatsSection = -1;
      }

      ImGui.SetNextItemOpen(this.context.OpenStatsSection == 1, ImGuiCond.Always);
      if (ImGui.CollapsingHeader("Price trend##statsPriceHeader"))
      {
        this.context.OpenStatsSection = 1;
        DrawPriceTrend(marketData);
      }
      else if (this.context.OpenStatsSection == 1)
      {
        this.context.OpenStatsSection = -1;
      }

      ImGui.SetNextItemOpen(this.context.OpenStatsSection == 2, ImGuiCond.Always);
      if (ImGui.CollapsingHeader("Volume##statsVolumeHeader"))
      {
        this.context.OpenStatsSection = 2;
        DrawVolume(marketData);
      }
      else if (this.context.OpenStatsSection == 2)
      {
        this.context.OpenStatsSection = -1;
      }

      ImGui.SetNextItemOpen(this.context.OpenStatsSection == 3, ImGuiCond.Always);
      if (ImGui.CollapsingHeader("Stack size histogram##statsStackHeader"))
      {
        this.context.OpenStatsSection = 3;
        DrawStackHistogram(marketData);
      }
      else if (this.context.OpenStatsSection == 3)
      {
        this.context.OpenStatsSection = -1;
      }

      // Section 4 is unused; gilflux has always been section 5.
      ImGui.SetNextItemOpen(this.context.OpenStatsSection == 5, ImGuiCond.Always);
      if (ImGui.CollapsingHeader($"Gilflux ({queryTarget})##statsGilfluxHeader"))
      {
        this.context.OpenStatsSection = 5;
        this.DrawGilflux();
      }
      else if (this.context.OpenStatsSection == 5)
      {
        this.context.OpenStatsSection = -1;
      }
    }

    private static void DrawPriceTrend(MarketDataResponse marketData)
    {
      var priceChildHeight = Math.Max(250, ImGui.GetContentRegionAvail().Y);
      ImGui.BeginChild("priceChild", new Vector2(-1, priceChildHeight), false);

      var x = new List<float>();
      var y = new List<float>();
      foreach (var historyEntry in marketData.RecentHistory ?? new List<MarketDataRecentHistory>())
      {
        x.Add(historyEntry.Timestamp);
        y.Add(historyEntry.PricePerUnit);
      }

      if (x.Count > 0)
      {
        var xa = x.ToArray();
        var ya = y.ToArray();
        if (ImPlot.BeginPlot("##statsPricePlot", new Vector2(-1, priceChildHeight - 30)))
        {
          ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
          ImPlot.PlotLine("Price", ref xa[0], ref ya[0], xa.Length);
          ImPlot.EndPlot();
        }
      }
      else
      {
        ImGui.Text("No recent history available to draw price trend.");
      }

      ImGui.EndChild();
    }

    private static void DrawVolume(MarketDataResponse marketData)
    {
      var volChildHeight = Math.Max(250, ImGui.GetContentRegionAvail().Y);
      ImGui.BeginChild("volumeChild", new Vector2(-1, volChildHeight), false);

      var xq = new List<float>();
      var q = new List<float>();
      foreach (var historyEntry in marketData.RecentHistory ?? new List<MarketDataRecentHistory>())
      {
        xq.Add(historyEntry.Timestamp);
        q.Add(historyEntry.Quantity);
      }

      if (xq.Count > 0)
      {
        var xa = xq.ToArray();
        var qa = q.ToArray();
        if (ImPlot.BeginPlot("##statsQtyPlot", new Vector2(-1, volChildHeight - 30)))
        {
          ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
          ImPlot.PlotBars("Quantity", ref xa[0], ref qa[0], xa.Length, 3600);
          ImPlot.EndPlot();
        }
      }
      else
      {
        ImGui.Text("No recent history available to draw volumes.");
      }

      ImGui.EndChild();
    }

    private static void DrawStackHistogram(MarketDataResponse marketData)
    {
      var stackChildHeight = Math.Max(250, ImGui.GetContentRegionAvail().Y);
      ImGui.BeginChild("stackChild", new Vector2(-1, stackChildHeight), false);

      if (marketData.StackSizeHistogram != null && marketData.StackSizeHistogram.Count > 0)
      {
        foreach (var kv in marketData.StackSizeHistogram)
        {
          ImGui.Text($"{kv.Key}: {kv.Value}");
        }
      }
      else
      {
        ImGui.Text("No stack size histogram available.");
      }

      ImGui.EndChild();
    }

    private static void DrawRow(uint color, string first, string second, string third)
    {
      ImGui.TableNextRow();
      DrawCell(0, color, first);
      DrawCell(1, color, second);
      DrawCell(2, color, third);
    }

    private static void DrawRow(string first, string second, string third)
    {
      ImGui.TableNextRow();
      DrawCell(0, first);
      DrawCell(1, second);
      DrawCell(2, third);
    }

    private static void DrawGilfluxRow(uint labelColor, string label, long value)
    {
      ImGui.TableNextRow();
      DrawCell(0, labelColor, label);
      DrawCell(1, value.ToString("N0", CultureInfo.CurrentCulture));
    }

    private static void DrawCell(int column, uint color, string text)
    {
      ImGui.TableSetColumnIndex(column);
      ImGui.PushStyleColor(ImGuiCol.Text, color);
      ImGui.Text(text);
      ImGui.PopStyleColor();
    }

    private static void DrawCell(int column, string text)
    {
      ImGui.TableSetColumnIndex(column);
      ImGui.Text(text);
    }

    private void DrawKpis(MarketDataResponse marketData)
    {
      if (!ImGui.BeginTable("statsKpis", 3, GridFlags))
      {
        return;
      }

      var currency = this.context.Plugin.NumberFormatInfo;
      var label = this.context.Theme.TextDim;
      var gil = this.context.Theme.GilText;

      DrawRow(label, "Current Avg", "Current Avg NQ", "Current Avg HQ");
      DrawRow(
        gil,
        marketData.CurrentAveragePrice.ToString("C", currency),
        marketData.CurrentAveragePriceNq.ToString("C", currency),
        marketData.CurrentAveragePriceHq.ToString("C", currency));

      DrawRow(label, "Average", "Average NQ", "Average HQ");
      DrawRow(
        gil,
        marketData.AveragePrice.ToString("C", currency),
        marketData.AveragePriceNq.ToString("C", currency),
        marketData.AveragePriceHq.ToString("C", currency));

      DrawRow(label, "Min / Max", "Min / Max NQ", "Min / Max HQ");
      DrawRow(
        gil,
        $"{marketData.MinPrice:N0} / {marketData.MaxPrice:N0}",
        $"{marketData.MinPriceNq:N0} / {marketData.MaxPriceNq:N0}",
        $"{marketData.MinPriceHq:N0} / {marketData.MaxPriceHq:N0}");

      DrawRow(label, "Sale Velocity", "Sale Velocity NQ", "Sale Velocity HQ");
      DrawRow(
        marketData.SaleVelocity.ToString("N2", CultureInfo.CurrentCulture),
        marketData.SaleVelocityNq.ToString("N2", CultureInfo.CurrentCulture),
        marketData.SaleVelocityHq.ToString("N2", CultureInfo.CurrentCulture));

      ImGui.TableNextRow();
      DrawCell(0, label, "Units For Sale");
      DrawCell(1, label, "Listings");

      ImGui.TableNextRow();
      DrawCell(0, marketData.UnitsForSale.ToString("N0", CultureInfo.CurrentCulture));
      DrawCell(1, marketData.ListingsCount.ToString("N0", CultureInfo.CurrentCulture));

      ImGui.EndTable();
    }

    private void DrawGilflux()
    {
      if (this.context.MarketData.IsLoadingGilflux)
      {
        var spinnerIndex = (int)(ImGui.GetTime() / 0.15) % SpinnerFrames.Length;
        ImGui.Text($"{SpinnerFrames[spinnerIndex]} Loading data from FFXIVMT...");
        return;
      }

      var gilflux = this.context.MarketData.Gilflux;

      if (gilflux == null)
      {
        ImGui.Text("No gilflux data available for this item.");
        return;
      }

      if (ImGui.BeginTable("gilfluxColumns", 2, GridFlags))
      {
        var label = this.context.Theme.TextDim;

        DrawGilfluxRow(label, "1 Hour", gilflux.Ranking1h);
        DrawGilfluxRow(label, "3 Hours", gilflux.Ranking3h);
        DrawGilfluxRow(label, "6 Hours", gilflux.Ranking6h);
        DrawGilfluxRow(label, "12 Hours", gilflux.Ranking12h);
        DrawGilfluxRow(label, "1 Day", gilflux.Ranking1d);
        DrawGilfluxRow(label, "3 Days", gilflux.Ranking3d);
        DrawGilfluxRow(label, "7 Days", gilflux.Ranking7d);

        ImGui.EndTable();
      }

      if (gilflux.LastSaleTime > 0)
      {
        var lastSale = DateTimeOffset.FromUnixTimeMilliseconds(gilflux.LastSaleTime).LocalDateTime;
        ImGui.Text($"Last Sale: {lastSale.ToString("g", CultureInfo.CurrentCulture)}");
      }

      if (gilflux.UpdatedAt > 0)
      {
        var updatedAt = DateTimeOffset.FromUnixTimeMilliseconds(gilflux.UpdatedAt).LocalDateTime;
        ImGui.TextDisabled($"Updated: {updatedAt.ToString("g", CultureInfo.CurrentCulture)}");
      }
    }
  }
}
