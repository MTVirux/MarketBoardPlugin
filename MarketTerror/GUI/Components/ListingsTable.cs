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

      var heading = this.context.Config.NoGilSalesTax
        ? "Current listings"
        : "Current listings (Includes 5% GST)";

      var collapsed = this.context.Config.CurrentListingsCollapsed;

      if (SectionHeading.Draw(this.context, "currentListingsHeading", heading, collapsed))
      {
        this.context.Config.CurrentListingsCollapsed = !collapsed;
        this.context.Plugin.PluginInterface.SavePluginConfig(this.context.Config);
      }

      var beforeSeparator = ImGui.GetCursorPosY();
      ImGui.Separator();
      var separatorHeight = ImGui.GetCursorPosY() - beforeSeparator;

      if (collapsed)
      {
        return;
      }

      var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable
        | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

      // Leave room for the matching separator closing the table off.
      var tableHeight = sectionHeight - (ImGui.GetCursorPosY() - top) - separatorHeight;

      // Straddle the panel's padding so the table alone runs to its edges.
      var padding = ImGui.GetStyle().WindowPadding.X;
      var tableWidth = ImGui.GetContentRegionAvail().X + (padding * 2.0f);
      ImGui.SetCursorPosX(ImGui.GetCursorPosX() - padding);

      if (!ImGui.BeginTable("currentListings", 5, flags, new Vector2(tableWidth, tableHeight)))
      {
        return;
      }

      ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeightWithSpacing() * 1.5f);
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("Qty");
      ImGui.TableSetupColumn("Total");
      ImGui.TableSetupColumn("Retainer");

      ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextDim);
      DrawHeaders();
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

    private void DrawRow(MarketDataListing listing, int index)
    {
      var worlds = this.context.Worlds;

      ImGui.TableNextRow();
      ImGui.TableSetColumnIndex(0);

      var cursor = ImGui.GetCursorPos();
      var hqOffset = CenterOffset(SeIconChar.HighQuality.AsString());

      var clicked = ImGui.Selectable(
        $"##listing{index}",
        this.context.SelectedListing == index,
        ImGuiSelectableFlags.SpanAllColumns);

      // Bound while the selectable is still the last item, so the whole row answers the right click.
      this.DrawRowContextMenu(listing, index);

      if (listing.Hq)
      {
        ImGui.SetCursorPos(new Vector2(cursor.X + hqOffset, cursor.Y));
        ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextBright);
        ImGui.Text(SeIconChar.HighQuality.AsString());
        ImGui.PopStyleColor();
      }

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

      if (worlds.IsMultiWorld)
      {
        retainerSB.Append(CultureInfo.CurrentCulture, $" {listing.WorldName ?? string.Empty}");

        if (worlds.IsRegionWide)
        {
          retainerSB.Append(CultureInfo.CurrentCulture, $" @ {worlds.GetDataCenterName(listing.WorldID!.Value)}");
        }
      }
      else
      {
        retainerSB.Append(CultureInfo.CurrentCulture, $" {worlds.QueryTarget}");
      }

      ImGui.Text(retainerSB.ToString());
    }

    private void DrawRowContextMenu(MarketDataListing listing, int index)
    {
      if (!ImGui.BeginPopupContextItem($"listingContextMenu{index}"))
      {
        return;
      }

      if (this.context.SelectedItem.HasValue && ImGui.Selectable("Add to the shopping list"))
      {
        this.context.AddListingToShoppingList(this.context.SelectedItem.Value, listing);
      }

      ImGui.EndPopup();
    }

    private void HandleClick(MarketDataListing listing, int index)
    {
      var plugin = this.context.Plugin;
      var worlds = this.context.Worlds;

      this.context.SelectedListing = index;

      // Single-world Universalis queries don't populate per-listing WorldName, so fall back to the selected world.
      var worldName = worlds.IsMultiWorld
        ? listing.WorldName ?? string.Empty
        : worlds.QueryTarget;

      this.context.GoToMarketBoard(worldName, this.context.SelectedItem, this.context.Config.AutoTeleportToWorld);

      if (this.context.Config.CopyItemNameOnListingClick && this.context.SelectedItem.HasValue)
      {
        this.context.CopyToClipboard(this.context.SelectedItem.Value.Name.ExtractText());
      }
    }
  }
}
