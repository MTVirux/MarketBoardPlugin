// <copyright file="ItemTooltip.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface.Textures;
  using Dalamud.Utility;
  using Lumina.Excel.Sheets;
  using MarketTerror.Helpers;

  /// <summary>
  /// Draws an in-game style tooltip for an item, rebuilt in ImGui from the game's item sheets
  /// and coloured from the active theme.
  /// </summary>
  public sealed class ItemTooltip
  {
    private const float WrapWidth = 340.0f;
    private const float IconSize = 40.0f;
    private const float BorderSize = 1.0f;

    /// <summary>
    /// The ItemAction types whose second data value is an ItemFood row: combat meals, gatherer and
    /// crafter meals, and the attribute potions. Other consumables reuse that slot for an HP or MP
    /// cap, which resolves to an unrelated ItemFood row if it is followed.
    /// </summary>
    private static readonly uint[] FoodActionTypes = [844, 845, 846];

    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemTooltip"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public ItemTooltip(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the tooltip for an item. The caller has already checked that its anchor is hovered.
    /// </summary>
    /// <param name="item">The item to describe.</param>
    public void Draw(Item item)
    {
      var scale = ImGui.GetIO().FontGlobalScale;

      ImGui.PushStyleColor(ImGuiCol.PopupBg, this.context.Theme.PanelBg);
      ImGui.PushStyleColor(ImGuiCol.Border, this.context.Theme.Border);
      ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12.0f, 10.0f) * scale);
      ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, BorderSize);

      ImGui.BeginTooltip();
      ImGui.PushTextWrapPos(RightEdge());

      this.DrawHeader(item, scale);
      this.DrawFlags(item);
      this.DrawUnlockState(item);
      this.DrawLevels(item);
      this.DrawCombatStats(item);
      this.DrawBonuses(item);
      this.DrawFoodBonuses(item);
      this.DrawSetBonus(item);
      this.DrawCustomisation(item);
      this.DrawDescription(item);
      this.DrawFooter(item);

      ImGui.PopTextWrapPos();
      ImGui.EndTooltip();

      ImGui.PopStyleVar(2);
      ImGui.PopStyleColor(2);
    }

    private static float RightEdge()
    {
      return ImGui.GetStyle().WindowPadding.X + (WrapWidth * ImGui.GetIO().FontGlobalScale);
    }

    private static string Signed(int value)
    {
      return value > 0
        ? "+" + value.ToString(CultureInfo.CurrentCulture)
        : value.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats one food bonus. A relative bonus is a percentage of the character's own stat, capped
    /// at the value the sheet carries alongside it; an absolute bonus is a flat amount.
    /// </summary>
    private static string FoodValue(int value, int max, bool relative)
    {
      if (!relative)
      {
        return Signed(value);
      }

      return max > 0
        ? string.Create(CultureInfo.CurrentCulture, $"{Signed(value)}% (Max {max})")
        : Signed(value) + "%";
    }

    private static void Text(string text, uint color)
    {
      ImGui.PushStyleColor(ImGuiCol.Text, color);
      ImGui.TextUnformatted(text);
      ImGui.PopStyleColor();
    }

    /// <summary>
    /// Item rarity keeps its own colours: it is data about the item rather than window chrome,
    /// so common items fall back to the theme and everything rarer keeps the familiar tint.
    /// </summary>
    private uint RarityColor(byte rarity) => rarity switch
    {
      2 => 0xFF64D964,
      3 => 0xFFE8A445,
      4 => 0xFFF08CC6,
      7 => 0xFFCC9FFF,
      _ => this.context.Theme.TextBright,
    };

    private void Label(string text)
    {
      Text(text, this.context.Theme.TextDim);
    }

    private void Row(string label, string value)
    {
      this.Row(label, value, this.context.Theme.Text);
    }

    /// <summary>
    /// Draws a dim label on the left and a value pinned to the right edge.
    /// </summary>
    private void Row(string label, string value, uint valueColor)
    {
      this.Label(label);
      ImGui.SameLine();
      ImGui.SetCursorPosX(RightEdge() - ImGui.CalcTextSize(value).X);
      Text(value, valueColor);
    }

    /// <summary>
    /// Draws a separated block of stat lines, sizing the high quality column to its widest entry
    /// and heading it only when at least one line has a high quality value.
    /// </summary>
    private void DrawStatRows(List<(string Label, string Value, string Hq)> rows)
    {
      if (rows.Count == 0)
      {
        return;
      }

      var hqColumn = 0.0f;

      foreach (var row in rows)
      {
        hqColumn = Math.Max(hqColumn, ImGui.CalcTextSize(row.Hq).X);
      }

      if (hqColumn > 0.0f)
      {
        hqColumn += ImGui.GetStyle().ItemSpacing.X * 2.0f;
      }

      ImGui.Separator();

      if (hqColumn > 0.0f)
      {
        ImGui.SetCursorPosX(RightEdge() - ImGui.CalcTextSize("HQ").X);
        this.Label("HQ");
      }

      foreach (var row in rows)
      {
        this.StatRow(row.Label, row.Value, row.Hq, hqColumn);
      }
    }

    /// <summary>
    /// Draws a stat line with the normal quality value and, where one exists, the high quality
    /// total in its own column against the right edge.
    /// </summary>
    private void StatRow(string label, string value, string hqValue, float hqColumn)
    {
      var right = RightEdge();

      this.Label(label);

      if (value.Length > 0)
      {
        ImGui.SameLine();
        ImGui.SetCursorPosX(right - hqColumn - ImGui.CalcTextSize(value).X);
        Text(value, this.context.Theme.Text);
      }

      if (hqValue.Length == 0)
      {
        return;
      }

      ImGui.SameLine();
      ImGui.SetCursorPosX(right - ImGui.CalcTextSize(hqValue).X);
      Text(hqValue, this.context.Theme.GilText);
    }

    private void DrawHeader(Item item, float scale)
    {
      using var icon = this.context.Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup
      {
        IconId = item.Icon,
      }).GetWrapOrDefault();

      if (icon != null)
      {
        ImGui.Image(icon.Handle, new Vector2(IconSize, IconSize) * scale);
        ImGui.SameLine();
      }

      ImGui.BeginGroup();

      Text(item.Name.ExtractText(), this.RarityColor(item.Rarity));

      var category = item.ItemUICategory.ValueNullable?.Name.ExtractText() ?? string.Empty;

      if (category.Length > 0)
      {
        this.Label(category);
      }

      ImGui.EndGroup();
    }

    private void DrawFlags(Item item)
    {
      var flags = new List<string>();

      if (item.IsUnique)
      {
        flags.Add("Unique");
      }

      if (item.IsUntradable)
      {
        flags.Add("Untradable");
      }

      if (item.IsIndisposable)
      {
        flags.Add("Indisposable");
      }

      if (item.IsCollectable)
      {
        flags.Add("Collectable");
      }

      if (flags.Count > 0)
      {
        this.Label(string.Join("   ", flags));
      }
    }

    private void DrawUnlockState(Item item)
    {
      var unlocked = ItemUnlock.IsUnlocked(this.context.Plugin.ClientState, item.RowId);

      if (unlocked == null)
      {
        return;
      }

      Text(unlocked.Value ? "Already unlocked" : "Not unlocked", this.context.Theme.TextBright);
    }

    private void DrawLevels(Item item)
    {
      var itemLevel = item.LevelItem.RowId;
      var jobs = item.ClassJobCategory.ValueNullable?.Name.ExtractText() ?? string.Empty;

      if (itemLevel == 0 && item.LevelEquip == 0 && jobs.Length == 0)
      {
        return;
      }

      ImGui.Separator();

      if (itemLevel > 0)
      {
        this.Row("Item Level", itemLevel.ToString(CultureInfo.CurrentCulture));
      }

      if (item.LevelEquip > 0)
      {
        this.Row("Equip Level", item.LevelEquip.ToString(CultureInfo.CurrentCulture));
      }

      if (jobs.Length > 0)
      {
        Text(jobs, this.context.Theme.Text);
      }
    }

    private void DrawCombatStats(Item item)
    {
      var rows = new List<(string Label, string Value)>();

      if (item.DamagePhys > 0)
      {
        rows.Add(("Physical Damage", item.DamagePhys.ToString(CultureInfo.CurrentCulture)));
      }

      if (item.DamageMag > 0)
      {
        rows.Add(("Magic Damage", item.DamageMag.ToString(CultureInfo.CurrentCulture)));
      }

      if (item.Delayms > 0)
      {
        var delay = item.Delayms / 1000.0f;
        var damage = Math.Max(item.DamagePhys, item.DamageMag);

        if (damage > 0)
        {
          rows.Add(("Auto-attack", (damage * delay / 3.0f).ToString("F2", CultureInfo.CurrentCulture)));
        }

        rows.Add(("Delay", delay.ToString("F2", CultureInfo.CurrentCulture)));
      }

      if (item.DefensePhys > 0)
      {
        rows.Add(("Defense", item.DefensePhys.ToString(CultureInfo.CurrentCulture)));
      }

      if (item.DefenseMag > 0)
      {
        rows.Add(("Magic Defense", item.DefenseMag.ToString(CultureInfo.CurrentCulture)));
      }

      if (item.Block > 0)
      {
        rows.Add(("Block Strength", item.Block.ToString(CultureInfo.CurrentCulture)));
      }

      if (item.BlockRate > 0)
      {
        rows.Add(("Block Rate", item.BlockRate.ToString(CultureInfo.CurrentCulture)));
      }

      if (rows.Count == 0)
      {
        return;
      }

      ImGui.Separator();

      foreach (var row in rows)
      {
        this.Row(row.Label, row.Value);
      }
    }

    private void DrawBonuses(Item item)
    {
      var specials = new Dictionary<uint, (string Name, int Value)>();

      for (var i = 0; i < item.BaseParamSpecial.Count; i++)
      {
        var param = item.BaseParamSpecial[i];
        var value = (int)item.BaseParamValueSpecial[i];
        var name = param.ValueNullable?.Name.ExtractText() ?? string.Empty;

        if (param.RowId != 0 && value != 0 && name.Length > 0)
        {
          specials[param.RowId] = (name, value);
        }
      }

      var rows = new List<(string Label, string Value, string Hq)>();

      for (var i = 0; i < item.BaseParam.Count; i++)
      {
        var param = item.BaseParam[i];
        var value = (int)item.BaseParamValue[i];
        var name = param.ValueNullable?.Name.ExtractText() ?? string.Empty;

        if (param.RowId == 0 || value == 0 || name.Length == 0)
        {
          continue;
        }

        var hq = specials.Remove(param.RowId, out var special)
          ? Signed(value + special.Value)
          : string.Empty;

        rows.Add((name, Signed(value), hq));
      }

      // Anything left is a high quality bonus on a stat the item does not carry at normal quality,
      // such as the damage bonus on an HQ weapon.
      foreach (var leftover in specials.Values)
      {
        rows.Add((leftover.Name, string.Empty, Signed(leftover.Value)));
      }

      this.DrawStatRows(rows);
    }

    /// <summary>
    /// Meals and attribute potions carry no bonuses on the item row itself: their stats live in the
    /// ItemFood sheet, reached through the item's action.
    /// </summary>
    private void DrawFoodBonuses(Item item)
    {
      var action = item.ItemAction.ValueNullable;

      if (action == null || action.Value.Data.Count < 2 || !FoodActionTypes.Contains(action.Value.Action.RowId))
      {
        return;
      }

      var food = this.context.Plugin.DataManager.Excel.GetSheet<ItemFood>().GetRowOrDefault(action.Value.Data[1]);

      if (food == null)
      {
        return;
      }

      var rows = new List<(string Label, string Value, string Hq)>();

      for (var i = 0; i < food.Value.Params.Count; i++)
      {
        var param = food.Value.Params[i];
        var name = param.BaseParam.ValueNullable?.Name.ExtractText() ?? string.Empty;

        if (param.BaseParam.RowId == 0 || param.Value == 0 || name.Length == 0)
        {
          continue;
        }

        rows.Add((
          name,
          FoodValue(param.Value, param.Max, param.IsRelative),
          FoodValue(param.ValueHQ, param.MaxHQ, param.IsRelative)));
      }

      this.DrawStatRows(rows);
    }

    private void DrawSetBonus(Item item)
    {
      var name = item.ItemSpecialBonus.ValueNullable?.Name.ExtractText() ?? string.Empty;

      if (name.Length == 0)
      {
        return;
      }

      ImGui.Separator();
      this.Label(name);
    }

    private void DrawCustomisation(Item item)
    {
      var rows = new List<(string Label, string Value)>();

      if (item.MateriaSlotCount > 0)
      {
        rows.Add(("Materia Slots", item.MateriaSlotCount.ToString(CultureInfo.CurrentCulture)));
      }

      if (item.IsAdvancedMeldingPermitted)
      {
        rows.Add(("Advanced Melding", "Permitted"));
      }

      if (item.DyeCount > 0)
      {
        rows.Add(("Dye Slots", item.DyeCount.ToString(CultureInfo.CurrentCulture)));
      }

      if (item.IsGlamorous)
      {
        rows.Add(("Glamour", "Yes"));
      }

      if (rows.Count == 0)
      {
        return;
      }

      ImGui.Separator();

      foreach (var row in rows)
      {
        this.Row(row.Label, row.Value);
      }
    }

    private void DrawDescription(Item item)
    {
      var description = item.Description.ExtractText().Replace('\r', '\n');

      if (string.IsNullOrWhiteSpace(description))
      {
        return;
      }

      ImGui.Separator();
      Text(description, this.context.Theme.TextDim);
    }

    private void DrawFooter(Item item)
    {
      var hasPrice = item.PriceLow > 0;
      var hasStack = item.StackSize > 1;

      if (!hasPrice && !hasStack)
      {
        return;
      }

      ImGui.Separator();

      if (hasPrice)
      {
        this.Row(
          "Sells to vendor",
          item.PriceLow.ToString("C", this.context.Plugin.NumberFormatInfo),
          this.context.Theme.GilText);
      }

      if (hasStack)
      {
        this.Row("Stack", item.StackSize.ToString("N0", CultureInfo.CurrentCulture));
      }
    }
  }
}
