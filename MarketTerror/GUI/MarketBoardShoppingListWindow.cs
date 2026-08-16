// <copyright file="MarketBoardShoppingListWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Numerics;
  using System.Text;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Dalamud.Interface.Windowing;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// The market board config window.
  /// </summary>
  public class MarketBoardShoppingListWindow : Window
  {
    private const ImGuiTableFlags TableFlags =
      ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp |
      ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate;

    private readonly TerrorTheme theme;

    private readonly List<SavedItem> sortedItems = new List<SavedItem>();

    private IDisposable? themeScope;

    private bool forceShown;

    private bool hidden;

    private int lastCount;

    private int sortColumn = -1;

    private int sortedRevision = -1;

    private bool sortAscending = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardShoppingListWindow"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public MarketBoardShoppingListWindow(MarketTerrorPlugin plugin)
      : base("Market Terror Shopping List")
    {
      this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

      this.Flags = ImGuiWindowFlags.NoScrollbar;
      this.IsOpen = true;
      this.RespectCloseHotkey = false;
      this.ShowCloseButton = false;
      this.Size = new Vector2(400, 150);
      this.SizeCondition = ImGuiCond.FirstUseEver;
      this.SizeConstraints = new WindowSizeConstraints
      {
        MinimumSize = new Vector2(400, 150),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
      };

      this.theme = new TerrorTheme(this.Plugin.Config);
    }

    /// <summary>
    /// Gets a value indicating whether the window is currently on screen.
    /// </summary>
    public bool IsShown =>
      !this.hidden && (this.Plugin.ShoppingList.Count > 0 || this.forceShown || this.Plugin.ShoppingListBulkAdd.IsRunning);

    private MarketTerrorPlugin Plugin { get; init; }

    /// <summary>
    /// Shows the window, or hides it when it is already shown.
    /// </summary>
    /// <remarks>The window is always open; what it draws is decided by <see cref="DrawConditions"/>.</remarks>
    public void ToggleShown()
    {
      this.hidden = this.IsShown;
      this.forceShown = !this.hidden;
    }

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
    public override bool DrawConditions()
    {
      var count = this.Plugin.ShoppingList.Count;

      // A newly added item brings the window back even after it was hidden.
      if (count > this.lastCount)
      {
        this.hidden = false;
      }

      this.lastCount = count;

      return this.IsShown;
    }

    /// <inheritdoc/>
    public override void Draw()
    {
      this.DrawBulkAddProgress();

      if (this.Plugin.ShoppingList.Count == 0)
      {
        if (!this.Plugin.ShoppingListBulkAdd.IsRunning)
        {
          ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
          ImGui.TextWrapped("Your buy list is empty. Add items from the item list right click menu.");
          ImGui.PopStyleColor();
        }

        return;
      }

      this.DrawActionBar();

      if (!ImGui.BeginTable("shoppingList", 4, TableFlags))
      {
        return;
      }

      ImGui.TableSetupColumn("Name");
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("World");
      ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.NoSort);

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.TableHeadersRow();
      ImGui.PopStyleColor();

      this.UpdateSort();

      List<SavedItem> todel = new List<SavedItem>();

      int k = 0;
      foreach (var item in this.sortedItems)
      {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.Text(item.SourceItem.Name.ExtractText());

        ImGui.TableSetColumnIndex(1);
        var price = item.Refreshing
          ? "Refreshing"
          : this.Plugin.Config.PriceIconShown
            ? item.Price.ToString("C", this.Plugin.NumberFormatInfo)
            : item.Price.ToString("N0", CultureInfo.CurrentCulture);
        var padding = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(price).X;
        if (padding > 0)
        {
          ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, item.Refreshing ? this.theme.TextDim : this.theme.GilText);
        ImGui.Text(price);
        ImGui.PopStyleColor();

        ImGui.TableSetColumnIndex(2);
        ImGui.Text(item.World);

        ImGui.TableSetColumnIndex(3);
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{(char)FontAwesomeIcon.TrashAlt}##shoplist" + k, new Vector2(32 * ImGui.GetIO().FontGlobalScale, 1.5f * ImGui.GetItemRectSize().Y)))
        {
          todel.Add(item);
        }

        ImGui.PopFont();
        k += 1;
      }

      ImGui.EndTable();

      foreach (var item in todel)
      {
        this.Plugin.ShoppingList.Remove(item);
      }
    }

    private void DrawActionBar()
    {
      var worlds = this.Plugin.MarketBoardContext.Worlds;
      var busy = this.Plugin.ShoppingListBulkAdd.IsRunning;

      ImGui.BeginDisabled(busy || !worlds.HasSelection);

      if (ImGui.Button("Refresh"))
      {
        this.Plugin.ShoppingListBulkAdd.StartRefresh(
          this.Plugin.ShoppingList.Select(i => i.SourceItem).ToArray(),
          worlds.QueryTarget);
      }

      ImGui.EndDisabled();
      Utilities.HoverTooltip("Price every item on the list again.");

      ImGui.SameLine();

      if (ImGui.Button("Copy"))
      {
        ImGui.OpenPopup("shoppingListCopy");
      }

      Utilities.HoverTooltip("Copy the list to the clipboard.");
      this.DrawCopyPopup();

      ImGui.SameLine();

      ImGui.BeginDisabled(busy);

      if (ImGui.Button("Clear"))
      {
        this.Plugin.ShoppingList.Clear();
      }

      ImGui.EndDisabled();
      Utilities.HoverTooltip("Remove every item from the list.");

      this.DrawTotal();

      ImGui.Separator();
    }

    private void DrawTotal()
    {
      var total = this.Plugin.ShoppingList.Sum(i => i.Price);
      var text = "Total Cost: " + (this.Plugin.Config.PriceIconShown
        ? total.ToString("C", this.Plugin.NumberFormatInfo)
        : total.ToString("N0", CultureInfo.CurrentCulture));

      ImGui.SameLine();

      var padding = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X;
      if (padding > 0)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
      }

      ImGui.AlignTextToFramePadding();
      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.GilText);
      ImGui.Text(text);
      ImGui.PopStyleColor();
    }

    private void DrawCopyPopup()
    {
      if (!ImGui.BeginPopup("shoppingListCopy"))
      {
        return;
      }

      var config = this.Plugin.Config;

      this.DrawCopyOption("Item name", config.ShoppingListCopyName, v => config.ShoppingListCopyName = v);
      this.DrawCopyOption("Price", config.ShoppingListCopyPrice, v => config.ShoppingListCopyPrice = v);
      this.DrawCopyOption("World", config.ShoppingListCopyWorld, v => config.ShoppingListCopyWorld = v);

      ImGui.Separator();

      ImGui.BeginDisabled(!config.ShoppingListCopyName && !config.ShoppingListCopyPrice && !config.ShoppingListCopyWorld);

      if (ImGui.Button("Copy to clipboard"))
      {
        this.CopyList();
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndDisabled();
      ImGui.EndPopup();
    }

    private void DrawCopyOption(string label, bool value, Action<bool> setter)
    {
      var current = value;

      if (ImGui.Checkbox(label, ref current))
      {
        setter(current);
        this.Plugin.PluginInterface.SavePluginConfig(this.Plugin.Config);
      }
    }

    private void CopyList()
    {
      var config = this.Plugin.Config;

      // The rows are copied in the order they are shown, unless the sorted view is out of date.
      IReadOnlyList<SavedItem> rows = this.sortedItems.Count == this.Plugin.ShoppingList.Count
        ? this.sortedItems
        : this.Plugin.ShoppingList;

      var builder = new StringBuilder();

      foreach (var item in rows)
      {
        var parts = new List<string>();

        if (config.ShoppingListCopyName)
        {
          parts.Add(item.SourceItem.Name.ExtractText());
        }

        if (config.ShoppingListCopyPrice)
        {
          parts.Add(item.Price.ToString("N0", CultureInfo.CurrentCulture));
        }

        if (config.ShoppingListCopyWorld)
        {
          parts.Add(item.World);
        }

        builder.AppendLine(string.Join(" - ", parts));
      }

      var text = builder.ToString().TrimEnd();

      if (text.Length == 0)
      {
        return;
      }

      ImGui.SetClipboardText(text);

      if (config.ClipboardNotificationsEnabled)
      {
        this.Plugin.NotifyClipboardCopied($"{rows.Count} shopping list rows");
      }
    }

    private void UpdateSort()
    {
      var specs = ImGui.TableGetSortSpecs();

      if (specs.IsNull)
      {
        return;
      }

      var column = -1;
      var ascending = true;

      if (specs.SpecsCount > 0)
      {
        var spec = specs.Specs;
        column = spec.ColumnIndex;
        ascending = spec.SortDirection != ImGuiSortDirection.Descending;
      }

      specs.SpecsDirty = false;

      if (column == this.sortColumn && ascending == this.sortAscending && this.sortedRevision == this.Plugin.ShoppingList.Revision)
      {
        return;
      }

      this.sortColumn = column;
      this.sortAscending = ascending;
      this.sortedRevision = this.Plugin.ShoppingList.Revision;

      this.sortedItems.Clear();
      this.sortedItems.AddRange(this.SortItems());
    }

    private IEnumerable<SavedItem> SortItems()
    {
      var items = this.Plugin.ShoppingList;

      switch (this.sortColumn)
      {
        case 0:
          return this.sortAscending
            ? items.OrderBy(i => i.SourceItem.Name.ExtractText(), StringComparer.CurrentCultureIgnoreCase)
            : items.OrderByDescending(i => i.SourceItem.Name.ExtractText(), StringComparer.CurrentCultureIgnoreCase);
        case 1:
          return this.sortAscending
            ? items.OrderBy(i => i.Price)
            : items.OrderByDescending(i => i.Price);
        case 2:
          return this.sortAscending
            ? items.OrderBy(i => i.World, StringComparer.CurrentCultureIgnoreCase)
            : items.OrderByDescending(i => i.World, StringComparer.CurrentCultureIgnoreCase);
        default:
          return items;
      }
    }

    private void DrawBulkAddProgress()
    {
      var bulkAdd = this.Plugin.ShoppingListBulkAdd;

      if (!bulkAdd.IsRunning)
      {
        return;
      }

      var processed = bulkAdd.Counted;
      var total = bulkAdd.Total;

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.TextWrapped($"Pricing {bulkAdd.CategoryName}...");
      ImGui.PopStyleColor();

      var cancelWidth = ImGui.CalcTextSize("Cancel").X + (ImGui.GetStyle().FramePadding.X * 2);
      var barWidth = ImGui.GetContentRegionAvail().X - cancelWidth - ImGui.GetStyle().ItemSpacing.X;

      ImGui.ProgressBar(
        total > 0 ? processed / (float)total : 0f,
        new Vector2(barWidth, 0),
        $"{processed} / {total}");

      ImGui.SameLine();

      if (ImGui.Button("Cancel"))
      {
        bulkAdd.Cancel();
      }

      ImGui.Separator();
    }
  }
}
