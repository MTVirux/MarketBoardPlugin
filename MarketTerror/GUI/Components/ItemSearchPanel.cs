// <copyright file="ItemSearchPanel.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// The search field, the history and favourites toggles and the advanced search options.
  /// </summary>
  public sealed class ItemSearchPanel
  {
    private readonly string[] categoryLabels = new[] { "All", "Weapons", "Equipments", "Others", "Furniture" };

    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemSearchPanel"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public ItemSearchPanel(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the search panel.
    /// </summary>
    public void Draw()
    {
      var scale = ImGui.GetIO().FontGlobalScale;

      ImGui.SetNextItemWidth(-1);
      var searchString = this.context.SearchString;
      ImGui.InputTextWithHint("##searchString", "Search for item", ref searchString, 256);
      this.context.SearchString = searchString;

      var previousYCursor = ImGui.GetCursorPosY();
      ImGui.SetCursorPosY(previousYCursor - (ImGui.GetFontSize() / 2.0f) + (13 * scale));
      ImGui.Text("Advanced Search");
      ImGui.SameLine();
      ImGui.SetCursorPosY(previousYCursor);
      var buttonSize = new Vector2(32 * scale, 1.5f * ImGui.GetItemRectSize().Y);
      ImGui.PushFont(UiBuilder.IconFont);
      ImGui.PushStyleColor(ImGuiCol.Text, this.context.AdvancedSearchOpen ? this.context.Theme.Accent : this.context.Theme.Text);
      if (ImGui.Button($"{(char)FontAwesomeIcon.Search}", buttonSize))
      {
        this.context.AdvancedSearchOpen = !this.context.AdvancedSearchOpen;
      }

      ImGui.PopStyleColor();

      ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - buttonSize.X);
      ImGui.SetCursorPosY(previousYCursor);
      if (ImGui.Button($"{(char)FontAwesomeIcon.Cog}", buttonSize))
      {
        this.context.Plugin.OpenConfigUi();
      }

      ImGui.PopFont();

      if (this.context.AdvancedSearchOpen)
      {
        this.DrawAdvancedSearch();
      }
    }

    private void DrawAdvancedSearch()
    {
      ImGui.Text("Category: ");
      ImGui.SameLine();
      var itemCategory = this.context.ItemCategory;
      ImGui.Combo("###ListBox", ref itemCategory, this.categoryLabels, this.categoryLabels.Length);
      this.context.ItemCategory = itemCategory;

      ImGui.Text("HQ Only : ");
      ImGui.SameLine();
      var hqOnly = this.context.HqOnly;
      ImGui.Checkbox("###Checkbox", ref hqOnly);
      this.context.HqOnly = hqOnly;

      ImGui.Text("Min Qty : ");
      ImGui.SameLine();
      var minQuantity = this.context.MinQuantity;
      ImGui.InputInt("###MinQuantity", ref minQuantity);
      this.context.MinQuantity = minQuantity;

      ImGui.Text("Class: ");
      ImGui.SameLine();
      if (ImGui.BeginCombo(
        "###ClassJobCombo",
        this.context.SelectedClassJob == null ? "All Classes" : this.context.SelectedClassJob.Value.Abbreviation.ExtractText()))
      {
        void SelectClassJob(ClassJob? classJob)
        {
          var selected = this.context.SelectedClassJob?.RowId == classJob?.RowId;
          if (ImGui.Selectable(classJob == null ? "All Classes" : classJob?.Abbreviation.ExtractText(), selected))
          {
            this.context.SelectedClassJob = classJob;
          }

          if (selected)
          {
            ImGui.SetItemDefaultFocus();
          }
        }

        SelectClassJob(null);

        foreach (var classJob in this.context.Catalog.ClassJobs)
        {
          SelectClassJob(classJob);
        }

        ImGui.EndCombo();
      }

      if (this.context.ItemCategory is 1 or 2)
      {
        ImGui.Text("Min level : ");
        ImGui.SameLine();
        var minLevel = this.context.MinLevel;
        ImGui.InputInt("##lvlmin", ref minLevel);
        this.context.MinLevel = minLevel;

        ImGui.Text("Max level : ");
        ImGui.SameLine();
        var maxLevel = this.context.MaxLevel;
        ImGui.InputInt("##lvlmax", ref maxLevel);
        this.context.MaxLevel = maxLevel;
      }
      else
      {
        // If the category selected doesn't need an equip level -> reset to default
        this.context.MinLevel = 0;
        this.context.MaxLevel = 100;
      }
    }
  }
}
