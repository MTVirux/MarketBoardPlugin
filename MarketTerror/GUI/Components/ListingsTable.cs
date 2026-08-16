// <copyright file="ListingsTable.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Globalization;
  using System.Linq;
  using System.Numerics;
  using System.Text;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Game.Text;
  using MarketTerror.Extensions;
  using MarketTerror.Helpers;
  using MarketTerror.Models.Universalis;

  /// <summary>
  /// The table of current market board listings for the selected item.
  /// </summary>
  public sealed class ListingsTable
  {
    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListingsTable"/> class.
    /// </summary>
    /// <param name="context">The state and services shared by every component.</param>
    public ListingsTable(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the current listings heading and table.
    /// </summary>
    /// <param name="sectionHeight">The height of the heading and table together, as computed by the window.</param>
    public void Draw(float sectionHeight)
    {
      var top = ImGui.GetCursorPosY();

      this.context.TitleFont.Push();
      ImGui.Text("Current listings (Includes 5% GST)");
      this.context.TitleFont.Pop();

      ImGui.Separator();

      var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable
        | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
      var tableHeight = sectionHeight - (ImGui.GetCursorPosY() - top);

      if (!ImGui.BeginTable("currentListings", 5, flags, new Vector2(0.0f, tableHeight)))
      {
        return;
      }

      ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeightWithSpacing() * 1.5f);
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("Qty");
      ImGui.TableSetupColumn("Total");
      ImGui.TableSetupColumn("Retainer");

      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);
      ImGui.TableHeadersRow();
      ImGui.PopStyleColor();

      var listingsSnapshot = this.context.MarketData.MarketData?.Listings.ToArray();
      var marketDataListings = listingsSnapshot?.OrderBy(l => l.PricePerUnit).ToList();

      if (marketDataListings != null)
      {
        for (var index = 0; index < marketDataListings.Count; index++)
        {
          this.DrawRow(marketDataListings[index], index);
        }
      }

      ImGui.EndTable();
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

    private void DrawRow(MarketDataListing listing, int index)
    {
      var selectedWorld = this.context.Worlds.SelectedIndex;

      ImGui.TableNextRow();
      ImGui.TableSetColumnIndex(0);

      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.Accent);
      var clicked = ImGui.Selectable(
        $"{(listing.Hq ? SeIconChar.HighQuality.AsString() : string.Empty)}##listing{index}",
        this.context.SelectedListing == index,
        ImGuiSelectableFlags.SpanAllColumns);
      ImGui.PopStyleColor();

      if (clicked)
      {
        this.HandleClick(listing, index);
      }

      ImGui.TableSetColumnIndex(1);
      double pricePerUnit = this.context.Config.NoGilSalesTax
        ? listing.PricePerUnit
        : listing.PricePerUnit + (listing.Tax / listing.Quantity);
      this.DrawGil(pricePerUnit);

      ImGui.TableSetColumnIndex(2);
      DrawRightAligned($"{listing.Quantity:##,###}");

      ImGui.TableSetColumnIndex(3);
      double totalPrice = this.context.Config.NoGilSalesTax
        ? listing.Total
        : listing.Total + listing.Tax;
      this.DrawGil(totalPrice);

      ImGui.TableSetColumnIndex(4);

      var retainerSB = new StringBuilder($"{listing.RetainerName} {SeIconChar.CrossWorld.ToChar()}");

      if (selectedWorld <= 1)
      {
        retainerSB.Append(CultureInfo.CurrentCulture, $" {listing.WorldName ?? string.Empty}");

        if (selectedWorld == 0)
        {
          retainerSB.Append(CultureInfo.CurrentCulture, $" @ {this.context.Worlds.GetDataCenterName(listing.WorldID!.Value)}");
        }
      }
      else
      {
        retainerSB.Append(CultureInfo.CurrentCulture, $" {this.context.Worlds.Worlds[selectedWorld].Query}");
      }

      ImGui.Text(retainerSB.ToString());
    }

    private void HandleClick(MarketDataListing listing, int index)
    {
      var plugin = this.context.Plugin;
      var selectedWorld = this.context.Worlds.SelectedIndex;

      this.context.SelectedListing = index;

      // Single-world Universalis queries don't populate per-listing WorldName, so fall back to the selected world.
      var worldName = selectedWorld > 1
        ? this.context.Worlds.Worlds[selectedWorld].Query
        : listing.WorldName ?? string.Empty;

      var travelEnabled = this.context.Config.AutoTeleportToWorld && !string.IsNullOrEmpty(worldName);
      var sameWorld = plugin.PlayerState.IsLoaded
        && string.Equals(worldName, plugin.PlayerState.CurrentWorld.Value.Name.ExtractText(), StringComparison.OrdinalIgnoreCase);

      // Skip travel when we're already at a Market Board in that world.
      var alreadyAtMarketBoard = sameWorld && plugin.GameGui.GetAddonByName("ItemSearch") != nint.Zero;

      // Right world already, and a board within reach: open that one instead of travelling.
      var openedLocalBoard = travelEnabled && sameWorld && !alreadyAtMarketBoard
        && MarketBoardInteraction.TryInteractWithNearbyBoard(plugin.ObjectTable, plugin.DataManager, plugin.Log);

      plugin.Log.Debug($"Listing click: world \"{worldName}\", sameWorld {sameWorld}, boardOpen {alreadyAtMarketBoard}, usedLocalBoard {openedLocalBoard}");

      var traveled = false;

      if (travelEnabled && !alreadyAtMarketBoard && !openedLocalBoard)
      {
        plugin.CommandManager.ProcessCommand($"/li {worldName} mb");
        traveled = true;
      }

      // Auto-search: queue for after the travel, or fill the open Market Board immediately.
      if (this.context.Config.AutoSearchOnMarketBoard && this.context.SelectedItem.HasValue)
      {
        var autoSearchName = this.context.SelectedItem.Value.Name.ExtractText();
        var autoSearchId = this.context.SelectedItem.Value.RowId;

        if (traveled && plugin.IsLifestreamInstalled)
        {
          plugin.AutoSearch.Arm(autoSearchName, autoSearchId);
        }
        else if (openedLocalBoard)
        {
          plugin.AutoSearch.ArmForLocalBoard(autoSearchName, autoSearchId);
        }
        else
        {
          plugin.AutoSearch.TryFillNow(autoSearchName, autoSearchId);
        }
      }

      if (this.context.Config.CopyItemNameOnListingClick && this.context.SelectedItem.HasValue)
      {
        this.context.CopyToClipboard(this.context.SelectedItem.Value.Name.ExtractText());
      }
    }
  }
}
