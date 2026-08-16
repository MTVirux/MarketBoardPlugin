// <copyright file="MarketBoardShoppingListWindow.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Dalamud.Interface.Windowing;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// The market board config window.
  /// </summary>
  public class MarketBoardShoppingListWindow : Window
  {
    private const ImGuiTableFlags TableFlags =
      ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp;

    private readonly TerrorTheme theme;

    private IDisposable? themeScope;

    private bool forceShown;

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

    private MarketTerrorPlugin Plugin { get; init; }

    /// <summary>
    /// Toggles whether the window is shown while the buy list is empty.
    /// </summary>
    public void ToggleForceShown()
    {
      this.forceShown = !this.forceShown;
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
    public override bool DrawConditions() =>
      this.Plugin.ShoppingList.Count > 0 || this.forceShown || this.Plugin.ShoppingListBulkAdd.IsRunning;

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

      if (!ImGui.BeginTable("shoppingList", 4, TableFlags))
      {
        return;
      }

      ImGui.TableSetupColumn("Name");
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("World");
      ImGui.TableSetupColumn("Action");

      ImGui.PushStyleColor(ImGuiCol.Text, this.theme.TextDim);
      ImGui.TableHeadersRow();
      ImGui.PopStyleColor();

      List<SavedItem> todel = new List<SavedItem>();

      int k = 0;
      foreach (var item in this.Plugin.ShoppingList)
      {
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        ImGui.Text(item.SourceItem.Name.ExtractText());

        ImGui.TableSetColumnIndex(1);
        var price = this.Plugin.Config.PriceIconShown
          ? item.Price.ToString("C", this.Plugin.NumberFormatInfo)
          : item.Price.ToString("N0", CultureInfo.CurrentCulture);
        var padding = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(price).X;
        if (padding > 0)
        {
          ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, this.theme.GilText);
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

    private void DrawBulkAddProgress()
    {
      var bulkAdd = this.Plugin.ShoppingListBulkAdd;

      if (!bulkAdd.IsRunning)
      {
        return;
      }

      var processed = bulkAdd.Processed;
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
