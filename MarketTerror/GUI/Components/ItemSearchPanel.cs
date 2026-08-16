// <copyright file="ItemSearchPanel.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// The search field, the history and favourites toggles and the advanced search options.
  /// </summary>
  public sealed class ItemSearchPanel
  {
    private static readonly string[] TopLevelLabels = new[] { "Weapons", "Armor", "Items", "Housing" };

    private static readonly (byte Rarity, string Label)[] RarityOptions = new[]
    {
      ((byte)1, "White"),
      ((byte)2, "Green"),
      ((byte)3, "Blue"),
      ((byte)4, "Purple"),
      ((byte)7, "Pink"),
    };

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
      ImGui.PopFont();

      if (this.context.AdvancedSearchOpen)
      {
        this.DrawAdvancedSearch();
      }
    }

    private static void Toggle<T>(ISet<T> set, T value, bool on)
    {
      if (on)
      {
        set.Add(value);
      }
      else
      {
        set.Remove(value);
      }
    }

    private static void DrawRange(string id, ref int min, ref int max, int limit, float scale)
    {
      var width = 55.0f * scale;

      ImGui.SetNextItemWidth(width);
      ImGui.InputInt($"##{id}Min", ref min, 0, 0);
      ImGui.SameLine();
      ImGui.Text("to");
      ImGui.SameLine();
      ImGui.SetNextItemWidth(width);
      ImGui.InputInt($"##{id}Max", ref max, 0, 0);

      min = Math.Clamp(min, 0, limit);
      max = Math.Clamp(max, 0, limit);
    }

    private void DrawAdvancedSearch()
    {
      var scale = ImGui.GetIO().FontGlobalScale;
      var equipment = this.SelectionHasEquipment();

      if (!ImGui.BeginTable("advancedSearch", 2, ImGuiTableFlags.SizingFixedFit))
      {
        return;
      }

      ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthFixed);
      ImGui.TableSetupColumn("value", ImGuiTableColumnFlags.WidthStretch);

      void Row(string label)
      {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.TableSetColumnIndex(1);
      }

      Row("Category");
      this.DrawCategoryPicker(scale);

      Row("Rarity");
      this.DrawRarityPicker();

      Row("Item level");
      var minItemLevel = this.context.MinItemLevel;
      var maxItemLevel = this.context.MaxItemLevel;
      DrawRange("ilvl", ref minItemLevel, ref maxItemLevel, MarketBoardContext.DefaultMaxItemLevel, scale);
      this.context.MinItemLevel = minItemLevel;
      this.context.MaxItemLevel = maxItemLevel;

      if (equipment)
      {
        Row("Equip level");
        var minLevel = this.context.MinLevel;
        var maxLevel = this.context.MaxLevel;
        DrawRange("lvl", ref minLevel, ref maxLevel, MarketBoardContext.DefaultMaxLevel, scale);
        this.context.MinLevel = minLevel;
        this.context.MaxLevel = maxLevel;
      }
      else
      {
        // Nothing equippable is selected, so a hidden equip level filter would silently drop items.
        this.context.MinLevel = 0;
        this.context.MaxLevel = MarketBoardContext.DefaultMaxLevel;
      }

      Row("Class");
      this.DrawClassPicker();

      // Nobody is logged in, so there is no character whose collection could be read.
      if (this.context.CanReadUnlockState)
      {
        Row("Collection");
        this.DrawUnlockPicker();
      }

      ImGui.EndTable();

      if (ImGui.Button("Reset filters"))
      {
        this.context.ResetFilters();
      }
    }

    private bool SelectionHasEquipment()
    {
      var selected = this.context.SelectedCategories;

      return selected.Count > 0
        && this.context.Catalog.Categories.Any(c => selected.Contains(c.RowId) && c.Category is 1 or 2);
    }

    private string CategoryPreview()
    {
      var selected = this.context.SelectedCategories;

      if (selected.Count == 0)
      {
        return "All categories";
      }

      if (selected.Count == 1)
      {
        return this.context.Catalog.Categories
          .Where(c => selected.Contains(c.RowId))
          .Select(c => c.Name.ExtractText())
          .FirstOrDefault() ?? "1 category";
      }

      return $"{selected.Count} categories";
    }

    private void DrawCategoryPicker(float scale)
    {
      var selected = this.context.SelectedCategories;

      ImGui.SetNextItemWidth(-1);
      ImGui.SetNextWindowSizeConstraints(Vector2.Zero, new Vector2(float.MaxValue, 400.0f * scale));

      if (!ImGui.BeginCombo("##categoryPicker", this.CategoryPreview()))
      {
        return;
      }

      if (ImGui.Selectable("All categories", selected.Count == 0))
      {
        selected.Clear();
      }

      ImGui.Separator();

      foreach (var group in this.context.Catalog.Categories.GroupBy(c => (int)c.Category))
      {
        var children = group.ToList();
        var checkedCount = children.Count(c => selected.Contains(c.RowId));
        var all = checkedCount > 0 && checkedCount == children.Count;

        if (ImGui.Checkbox($"##group{group.Key}", ref all))
        {
          foreach (var child in children)
          {
            Toggle(selected, child.RowId, all);
          }
        }

        ImGui.SameLine();

        var label = group.Key >= 1 && group.Key <= TopLevelLabels.Length
          ? TopLevelLabels[group.Key - 1]
          : $"Category {group.Key}";

        if (checkedCount > 0 && !all)
        {
          label += $" ({checkedCount})";
        }

        if (ImGui.TreeNode($"{label}##group{group.Key}"))
        {
          foreach (var child in children)
          {
            var on = selected.Contains(child.RowId);
            if (ImGui.Checkbox($"{child.Name.ExtractText()}##cat{child.RowId}", ref on))
            {
              Toggle(selected, child.RowId, on);
            }
          }

          ImGui.TreePop();
        }
      }

      ImGui.EndCombo();
    }

    private void DrawRarityPicker()
    {
      var selected = this.context.SelectedRarities;

      var preview = selected.Count switch
      {
        0 => "All rarities",
        1 => RarityOptions.First(r => selected.Contains(r.Rarity)).Label,
        _ => $"{selected.Count} rarities",
      };

      ImGui.SetNextItemWidth(-1);

      if (!ImGui.BeginCombo("##rarityPicker", preview))
      {
        return;
      }

      foreach (var option in RarityOptions)
      {
        var on = selected.Contains(option.Rarity);
        if (ImGui.Checkbox($"{option.Label}##rarity{option.Rarity}", ref on))
        {
          Toggle(selected, option.Rarity, on);
        }
      }

      ImGui.EndCombo();
    }

    private void DrawUnlockPicker()
    {
      var preview = this.context.UnlockFilter switch
      {
        true => "Already unlocked",
        false => "Not unlocked",
        _ => "All items",
      };

      ImGui.SetNextItemWidth(-1);

      if (!ImGui.BeginCombo("##unlockPicker", preview))
      {
        if (this.context.UnlockFilter != null && ImGui.IsItemHovered())
        {
          ImGui.SetTooltip("Only items that unlock something - minions, mounts, orchestrion rolls,\ncards - have a collection state, so everything else is hidden.");
        }

        return;
      }

      void SelectUnlockState(bool? unlocked, string label)
      {
        var selected = this.context.UnlockFilter == unlocked;
        if (ImGui.Selectable(label, selected))
        {
          this.context.UnlockFilter = unlocked;
        }

        if (selected)
        {
          ImGui.SetItemDefaultFocus();
        }
      }

      SelectUnlockState(null, "All items");
      SelectUnlockState(true, "Already unlocked");
      SelectUnlockState(false, "Not unlocked");

      ImGui.EndCombo();
    }

    private void DrawClassPicker()
    {
      ImGui.SetNextItemWidth(-1);

      if (!ImGui.BeginCombo(
        "##classJobPicker",
        this.context.SelectedClassJob == null ? "All Classes" : this.context.SelectedClassJob.Value.Abbreviation.ExtractText()))
      {
        return;
      }

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
  }
}
