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

    /// <summary>How far the mouse may sit from a sale point and still pick it, in pixels.</summary>
    private const float SalePickRadius = 12f;

    /// <summary>How much slack is left around the data when a sale plot is refitted.</summary>
    private const double ZoomOutMargin = 0.08;

    private static readonly char[] SpinnerFrames = ['|', '/', '-', '\\'];

    private readonly MarketBoardContext context;

    private bool refitPriceTrend = true;

    private bool refitVolume = true;

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

      var trendsWereOpen = this.context.OpenStatsSection == 1;
      ImGui.SetNextItemOpen(trendsWereOpen, ImGuiCond.Always);
      if (ImGui.CollapsingHeader("Trends##statsTrendsHeader"))
      {
        this.context.OpenStatsSection = 1;
        this.refitPriceTrend |= !trendsWereOpen;
        this.refitVolume |= !trendsWereOpen;
        this.DrawTrends(marketData);
      }
      else if (trendsWereOpen)
      {
        this.context.OpenStatsSection = -1;
      }

      // Sections 2, 3 and 4 are unused; gilflux has always been section 5.
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

    private static void SetupFittedAxes(double[] xs, double[] ys)
    {
      var xMin = xs[0];
      var xMax = xs[0];
      var yMin = ys[0];
      var yMax = ys[0];
      for (var i = 1; i < xs.Length; i++)
      {
        xMin = Math.Min(xMin, xs[i]);
        xMax = Math.Max(xMax, xs[i]);
        yMin = Math.Min(yMin, ys[i]);
        yMax = Math.Max(yMax, ys[i]);
      }

      // A single sale, or a run of identical prices, has no range to pad, so fall back to a
      // half hour either side and a tenth of the price.
      var xPad = Math.Max((xMax - xMin) * ZoomOutMargin, 1800);
      var yPad = Math.Max((yMax - yMin) * ZoomOutMargin, Math.Max(yMax * 0.1, 1));

      ImPlot.SetupAxesLimits(xMin - xPad, xMax + xPad, Math.Max(0, yMin - yPad), yMax + yPad, ImPlotCond.Always);
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

    private void DrawTrends(MarketDataResponse marketData)
    {
      if (!ImGui.BeginTabBar("statsTrendTabs"))
      {
        return;
      }

      if (ImGui.BeginTabItem("Price##statsPriceTab"))
      {
        this.DrawSalePlot(
          marketData,
          "priceChild",
          "##statsPricePlot",
          "Price",
          "No recent history available to draw price trend.",
          sale => sale.PricePerUnit,
          ref this.refitPriceTrend);
        ImGui.EndTabItem();
      }

      if (ImGui.BeginTabItem("Volume##statsVolumeTab"))
      {
        this.DrawSalePlot(
          marketData,
          "volumeChild",
          "##statsQtyPlot",
          "Quantity",
          "No recent history available to draw volumes.",
          sale => sale.Quantity,
          ref this.refitVolume);
        ImGui.EndTabItem();
      }

      ImGui.EndTabBar();
    }

    private void DrawSalePlot(
      MarketDataResponse marketData,
      string childId,
      string plotId,
      string seriesLabel,
      string emptyText,
      Func<MarketDataRecentHistory, double> selectValue,
      ref bool refit)
    {
      var childHeight = Math.Max(250, ImGui.GetContentRegionAvail().Y);
      ImGui.BeginChild(childId, new Vector2(-1, childHeight), false);

      var sales = marketData.RecentHistory ?? new List<MarketDataRecentHistory>();
      var x = new List<double>();
      var y = new List<double>();
      foreach (var historyEntry in sales)
      {
        x.Add(historyEntry.Timestamp);
        y.Add(selectValue(historyEntry));
      }

      if (x.Count > 0)
      {
        var xa = x.ToArray();
        var ya = y.ToArray();
        if (ImPlot.BeginPlot(plotId, new Vector2(-1, childHeight - 30)))
        {
          ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);

          if (refit)
          {
            SetupFittedAxes(xa, ya);
            refit = false;
          }

          ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, 3f);
          ImPlot.PlotLine(seriesLabel, ref xa[0], ref ya[0], xa.Length);
          this.DrawHoveredSale(sales, xa, ya);
          ImPlot.EndPlot();
        }
      }
      else
      {
        ImGui.Text(emptyText);
      }

      ImGui.EndChild();
    }

    private void DrawHoveredSale(IList<MarketDataRecentHistory> sales, double[] xs, double[] ys)
    {
      if (!ImPlot.IsPlotHovered())
      {
        return;
      }

      var mouse = ImGui.GetMousePos();
      var nearest = -1;
      var nearestDistance = float.MaxValue;
      for (var i = 0; i < xs.Length; i++)
      {
        var distance = Vector2.Distance(ImPlot.PlotToPixels(xs[i], ys[i]), mouse);
        if (distance < nearestDistance)
        {
          nearestDistance = distance;
          nearest = i;
        }
      }

      if (nearest < 0 || nearestDistance > SalePickRadius)
      {
        return;
      }

      var markerX = new[] { xs[nearest] };
      var markerY = new[] { ys[nearest] };
      ImPlot.SetNextMarkerStyle(ImPlotMarker.Circle, 6f);
      ImPlot.PlotScatter("##statsPriceHover", ref markerX[0], ref markerY[0], 1);

      var sale = sales[nearest];
      var currency = this.context.Plugin.NumberFormatInfo;
      var soldAt = DateTimeOffset.FromUnixTimeSeconds(sale.Timestamp).LocalDateTime;

      ImGui.BeginTooltip();
      ImGui.Text($"Unit price: {sale.PricePerUnit.ToString("C", currency)}");
      ImGui.Text($"Quantity: {sale.Quantity.ToString("N0", CultureInfo.CurrentCulture)}");
      ImGui.Text($"Total: {sale.Total.ToString("C", currency)}");
      ImGui.Text($"Date: {soldAt.ToString("g", CultureInfo.CurrentCulture)}");
      ImGui.EndTooltip();
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
        $"{marketData.MinPrice.ToString("C", currency)} / {marketData.MaxPrice.ToString("C", currency)}",
        $"{marketData.MinPriceNq.ToString("C", currency)} / {marketData.MaxPriceNq.ToString("C", currency)}",
        $"{marketData.MinPriceHq.ToString("C", currency)} / {marketData.MaxPriceHq.ToString("C", currency)}");

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
