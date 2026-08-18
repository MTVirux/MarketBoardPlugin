// <copyright file="ListingsTable.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
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
    /// The row a drag started on, or -1 when no drag is in progress.
    /// </summary>
    private int dragAnchor = -1;

    /// <summary>
    /// The row the drag is currently over. Only meaningful while <see cref="dragAnchor"/> is set.
    /// </summary>
    private int dragCursor = -1;

    /// <summary>
    /// How many listings were drawn last frame. The selection is positional, so a different count
    /// means the rows moved under it and it has to go.
    /// </summary>
    private int lastListingCount = -1;

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
      var count = marketDataListings?.Count ?? 0;

      if (count != this.lastListingCount)
      {
        this.ClearSelection();
        this.lastListingCount = count;
      }

      if (marketDataListings != null)
      {
        for (var index = 0; index < count; index++)
        {
          this.DrawRow(marketDataListings, index);
        }

        this.ApplyDrag(count);
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

    private void DrawRow(List<MarketDataListing> listings, int index)
    {
      var listing = listings[index];
      var worlds = this.context.Worlds;

      ImGui.TableNextRow();
      ImGui.TableSetColumnIndex(0);

      var cursor = ImGui.GetCursorPos();
      var hqOffset = CenterOffset(SeIconChar.HighQuality.AsString());

      // How many rows were lit up going into this frame, before the drag gets a say. A click that
      // lands on a multi-row selection only narrows it down; it must not travel anywhere.
      var wasMultiple = this.context.SelectedListings.Count > 1;

      var clicked = ImGui.Selectable(
        $"##listing{index}",
        this.context.SelectedListings.Contains(index),
        ImGuiSelectableFlags.SpanAllColumns);

      // The row that started the drag holds ImGui's active id, which would otherwise stop every
      // other row reporting the mouse passing over it.
      var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

      // Bound while the selectable is still the last item, so the whole row answers the right click.
      this.DrawRowContextMenu(listings, index);

      this.TrackDrag(index, hovered);

      if (listing.Hq)
      {
        ImGui.SetCursorPos(new Vector2(cursor.X + hqOffset, cursor.Y));
        ImGui.PushStyleColor(ImGuiCol.Text, this.context.Theme.TextBright);
        ImGui.Text(SeIconChar.HighQuality.AsString());
        ImGui.PopStyleColor();
      }

      if (clicked && !wasMultiple && !ImGui.GetIO().KeyCtrl)
      {
        this.HandleClick(listing);
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

    private void DrawRowContextMenu(List<MarketDataListing> listings, int index)
    {
      if (!ImGui.BeginPopupContextItem($"listingContextMenu{index}"))
      {
        return;
      }

      // Right-clicking away from the selection acts on that row alone, the way selections usually do.
      if (!this.context.SelectedListings.Contains(index))
      {
        this.ClearSelection();
        this.context.SelectedListings.Add(index);
      }

      var selected = this.SelectedRows(listings);

      if (this.context.SelectedItem.HasValue)
      {
        var label = selected.Count > 1
          ? $"Add {selected.Count} listings to the shopping list"
          : "Add to the shopping list";

        if (ImGui.Selectable(label))
        {
          this.context.AddListingsToShoppingList(this.context.SelectedItem.Value, selected);
        }
      }

      ImGui.EndPopup();
    }

    /// <summary>
    /// Notes what the mouse is doing to a row, so the drag can be turned into a range once every row
    /// has had its say.
    /// </summary>
    /// <param name="index">The row.</param>
    /// <param name="hovered">True when the mouse is over it.</param>
    private void TrackDrag(int index, bool hovered)
    {
      if (!hovered)
      {
        return;
      }

      if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
      {
        if (ImGui.GetIO().KeyCtrl)
        {
          // Ctrl picks rows off one at a time and starts no drag, so no range overwrites them.
          this.dragAnchor = -1;

          if (!this.context.SelectedListings.Remove(index))
          {
            this.context.SelectedListings.Add(index);
          }

          return;
        }

        this.dragAnchor = index;
        this.dragCursor = index;
        return;
      }

      if (this.dragAnchor >= 0 && ImGui.IsMouseDown(ImGuiMouseButton.Left))
      {
        this.dragCursor = index;
      }
    }

    /// <summary>
    /// Lights up every row between where the drag started and where it has got to.
    /// </summary>
    /// <param name="count">How many listings the table is drawing.</param>
    private void ApplyDrag(int count)
    {
      if (this.dragAnchor < 0)
      {
        return;
      }

      if (this.dragAnchor >= count || this.dragCursor >= count)
      {
        // The listings changed under the drag, so there is nothing left to drag over.
        this.dragAnchor = -1;
        return;
      }

      var first = Math.Min(this.dragAnchor, this.dragCursor);
      var last = Math.Max(this.dragAnchor, this.dragCursor);

      this.context.SelectedListings.Clear();

      for (var index = first; index <= last; index++)
      {
        this.context.SelectedListings.Add(index);
      }

      if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
      {
        this.dragAnchor = -1;
      }
    }

    /// <summary>
    /// The selected listings, in the order the table shows them.
    /// </summary>
    /// <param name="listings">The listings the table is drawing.</param>
    /// <returns>The listings the selection points at.</returns>
    private List<MarketDataListing> SelectedRows(List<MarketDataListing> listings)
    {
      return this.context.SelectedListings
        .Where(index => index >= 0 && index < listings.Count)
        .OrderBy(index => index)
        .Select(index => listings[index])
        .ToList();
    }

    private void ClearSelection()
    {
      this.context.SelectedListings.Clear();
      this.dragAnchor = -1;
    }

    private void HandleClick(MarketDataListing listing)
    {
      var worlds = this.context.Worlds;

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
