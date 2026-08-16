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
  using MarketTerror.Services;

  /// <summary>
  /// The market board config window.
  /// </summary>
  public class MarketBoardShoppingListWindow : Window
  {
    /// <summary>
    /// What a row shows in place of a price or a world it does not have.
    /// </summary>
    private const string NoValue = "-";

    private const ImGuiTableFlags TableFlags =
      ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp |
      ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate;

    /// <summary>
    /// The scopes as the picker lists them, widest reach first.
    /// </summary>
    private static readonly MarketScope[] ScopeOrder =
    {
      MarketScope.RegionWithOceania,
      MarketScope.Region,
      MarketScope.DataCentre,
      MarketScope.World,
    };

    private readonly TerrorTheme theme;

    private readonly List<SavedItem> sortedItems = new List<SavedItem>();

    private IDisposable? themeScope;

    private bool forceShown;

    private bool hidden;

    private int lastCount;

    private int sortColumn = -1;

    private int sortedRevision = -1;

    private bool sortAscending = true;

    private string worldFilter = string.Empty;

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
      this.ShowCloseButton = true;
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
    public override void OnClose()
    {
      this.hidden = true;
      this.forceShown = false;

      // The window stays open so a newly added item can bring it back.
      this.IsOpen = true;
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
        this.DrawScopePickers(false);
        ImGui.Separator();

        if (!this.Plugin.ShoppingListBulkAdd.IsRunning)
        {
          ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
          ImGui.TextWrapped("Your buy list is empty. Add items from the item list right click menu.");
          ImGui.PopStyleColor();
        }

        return;
      }

      this.DrawActionBar();

      // The total keeps its own row pinned under the table, so it stays put while the list scrolls.
      var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;

      if (!ImGui.BeginTable("shoppingList", 4, TableFlags | ImGuiTableFlags.ScrollY, new Vector2(0, -footerHeight)))
      {
        return;
      }

      ImGui.TableSetupColumn("Name");
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("World");
      ImGui.TableSetupColumn(
        "Action",
        ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.WidthFixed,
        (72 * ImGui.GetIO().FontGlobalScale) + ImGui.GetStyle().ItemSpacing.X);
      ImGui.TableSetupScrollFreeze(0, 1);

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

        if (item.Unlisted)
        {
          ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
        }

        ImGui.Text(item.SourceItem.Name.ExtractText());

        if (item.Unlisted)
        {
          ImGui.PopStyleColor();
        }

        ImGui.TableSetColumnIndex(1);
        var price = this.PriceText(item);
        var padding = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(price).X;
        if (padding > 0)
        {
          ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, item.Refreshing || item.Unlisted ? this.theme.TextDim : this.theme.GilText);
        ImGui.Text(price);
        ImGui.PopStyleColor();

        ImGui.TableSetColumnIndex(2);
        ImGui.Text(item.Unlisted ? NoValue : item.World);

        var buttonSize = new Vector2(32 * ImGui.GetIO().FontGlobalScale, 1.5f * ImGui.GetItemRectSize().Y);

        ImGui.TableSetColumnIndex(3);

        var travel = false;

        // Nothing to travel to when the scope had no listings, but the gap keeps the bin where it was.
        if (item.Unlisted)
        {
          ImGui.Dummy(buttonSize);
        }
        else
        {
          ImGui.BeginDisabled(string.IsNullOrEmpty(item.World));
          ImGui.PushFont(UiBuilder.IconFont);
          travel = ImGui.Button($"{(char)FontAwesomeIcon.Walking}##shoplistgo" + k, buttonSize);
          ImGui.PopFont();
          ImGui.EndDisabled();
          Utilities.HoverTooltip($"Go to the market board on {item.World}.");
        }

        ImGui.SameLine();

        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{(char)FontAwesomeIcon.TrashAlt}##shoplist" + k, buttonSize))
        {
          todel.Add(item);
        }

        ImGui.PopFont();
        Utilities.HoverTooltip("Remove from the list.");

        if (travel)
        {
          this.Plugin.MarketBoardContext.GoToMarketBoard(item.World, item.SourceItem, true);
        }

        k += 1;
      }

      ImGui.EndTable();

      ImGui.Separator();
      this.DrawTotal();

      foreach (var item in todel)
      {
        this.Plugin.ShoppingList.Remove(item);
      }
    }

    private static void DrawScopePicker(ShoppingListScope scope)
    {
      if (ImGui.BeginCombo("##shoppingListScope", ScopeLabel(scope.Scope)))
      {
        foreach (var level in ScopeOrder)
        {
          var isSelected = level == scope.Scope;

          if (ImGui.Selectable(ScopeLabel(level), isSelected))
          {
            scope.SelectScope(level);
          }

          if (isSelected)
          {
            ImGui.SetItemDefaultFocus();
          }
        }

        ImGui.EndCombo();
      }

      var target = scope.QueryTargetLabel;
      Utilities.HoverTooltip(target.Length > 0
        ? $"How far the searches reach. Prices come from {target}."
        : "How far the searches reach around the picked world.");
    }

    private static string ScopeLabel(MarketScope scope)
    {
      return scope switch
      {
        MarketScope.RegionWithOceania => "Region + Oceania",
        MarketScope.Region => "Region",
        MarketScope.DataCentre => "Data Centre",
        _ => "World",
      };
    }

    /// <summary>
    /// Draws the world and scope combos.
    /// </summary>
    /// <param name="sameLine">
    /// True to right-align them at the end of the action bar, false to give them a row of their own.
    /// </param>
    private void DrawScopePickers(bool sameLine)
    {
      var scope = this.Plugin.ShoppingListScope;
      var scale = ImGui.GetIO().FontGlobalScale;
      var spacing = ImGui.GetStyle().ItemSpacing.X;

      if (sameLine)
      {
        ImGui.SameLine();
      }

      var available = ImGui.GetContentRegionAvail().X;
      var scopeWidth = 140 * scale;
      var worldWidth = sameLine ? 150 * scale : available - scopeWidth - spacing;

      if (scopeWidth + worldWidth + spacing > available)
      {
        // Share what is left rather than spilling out of the window.
        var share = Math.Max(available - spacing, 80 * scale);
        scopeWidth = share * 0.5f;
        worldWidth = share - scopeWidth;
      }

      var padding = available - worldWidth - scopeWidth - spacing;
      if (padding > 0)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
      }

      ImGui.BeginDisabled(this.Plugin.ShoppingListBulkAdd.IsRunning);

      ImGui.SetNextItemWidth(worldWidth);
      this.DrawWorldPicker(scope);

      ImGui.SameLine();

      ImGui.SetNextItemWidth(scopeWidth);
      DrawScopePicker(scope);

      ImGui.EndDisabled();
    }

    private void DrawWorldPicker(ShoppingListScope scope)
    {
      var selected = scope.SelectedWorld;

      var open = ImGui.BeginCombo("##shoppingListWorld", selected.Length > 0 ? selected : "Pick a world");
      var resetToHome = ImGui.IsItemClicked(ImGuiMouseButton.Right);

      if (open)
      {
        if (ImGui.IsWindowAppearing())
        {
          this.worldFilter = string.Empty;
          ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##shoppingListWorldFilter", "Search worlds", ref this.worldFilter, 64);
        ImGui.Separator();

        var filter = this.worldFilter.Trim();
        var lastGroup = string.Empty;
        var matches = 0;

        foreach (var world in scope.Worlds)
        {
          var group = $"{world.Region} - {world.DataCentre}";

          if (filter.Length > 0 &&
              world.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
              group.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
          {
            continue;
          }

          matches++;

          if (group != lastGroup)
          {
            lastGroup = group;
            ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
            ImGui.Text(group);
            ImGui.PopStyleColor();
          }

          var isSelected = world.Name == selected;

          if (ImGui.Selectable(world.Name, isSelected))
          {
            scope.SelectWorld(world.Name);
          }

          if (isSelected)
          {
            ImGui.SetItemDefaultFocus();
          }
        }

        if (matches == 0)
        {
          ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
          ImGui.Text("No worlds match.");
          ImGui.PopStyleColor();
        }

        ImGui.EndCombo();
      }

      if (resetToHome)
      {
        scope.SelectHomeWorld();
      }

      Utilities.HoverTooltip("Your world");
    }

    private void DrawActionBar()
    {
      var scope = this.Plugin.ShoppingListScope;
      var busy = this.Plugin.ShoppingListBulkAdd.IsRunning;

      ImGui.BeginDisabled(busy || !scope.HasSelection);

      if (ImGui.Button("Refresh"))
      {
        this.Plugin.ShoppingListBulkAdd.StartRefresh(
          this.Plugin.ShoppingList.Select(i => i.SourceItem).ToArray(),
          scope.QueryTargets);
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

      this.DrawScopePickers(true);

      ImGui.Separator();
    }

    private string PriceText(SavedItem item)
    {
      if (item.Refreshing)
      {
        return "Refreshing";
      }

      if (item.Unlisted)
      {
        return NoValue;
      }

      return this.Plugin.Config.PriceIconShown
        ? item.Price.ToString("C", this.Plugin.NumberFormatInfo)
        : item.Price.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void DrawTotal()
    {
      var total = this.Plugin.ShoppingList.Sum(i => i.Price);
      var text = "Total Cost: " + (this.Plugin.Config.PriceIconShown
        ? total.ToString("C", this.Plugin.NumberFormatInfo)
        : total.ToString("N0", CultureInfo.CurrentCulture));

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
          parts.Add(item.Unlisted ? NoValue : item.Price.ToString("N0", CultureInfo.CurrentCulture));
        }

        if (config.ShoppingListCopyWorld)
        {
          parts.Add(item.Unlisted ? NoValue : item.World);
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
