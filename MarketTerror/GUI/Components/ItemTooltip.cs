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

  /// <summary>
  /// Draws an in-game style tooltip for an item, rebuilt in ImGui from the game's item sheets.
  /// </summary>
  public sealed class ItemTooltip
  {
    private const float WrapWidth = 340.0f;
    private const float IconSize = 40.0f;
    private const float HqColumnWidth = 54.0f;

    private const uint BackgroundColor = 0xE61E1414;
    private const uint FrameColor = 0x558C8C8C;
    private const uint LabelColor = 0xFF9A9A9A;
    private const uint ValueColor = 0xFFF0F0F0;
    private const uint DescriptionColor = 0xFFC8C8C8;
    private const uint HqColor = 0xFF4AD2FF;

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

      ImGui.PushStyleColor(ImGuiCol.PopupBg, BackgroundColor);
      ImGui.PushStyleColor(ImGuiCol.Border, FrameColor);
      ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12.0f, 10.0f) * scale);
      ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);

      ImGui.BeginTooltip();
      ImGui.PushTextWrapPos(RightEdge());

      this.DrawHeader(item, scale);
      DrawFlags(item);
      DrawLevels(item);
      DrawCombatStats(item);
      DrawBonuses(item);
      DrawSetBonus(item);
      DrawCustomisation(item);
      DrawDescription(item);
      this.DrawFooter(item);

      ImGui.PopTextWrapPos();
      ImGui.EndTooltip();

      ImGui.PopStyleVar(2);
      ImGui.PopStyleColor(2);
    }

    private static uint RarityColor(byte rarity) => rarity switch
    {
      2 => 0xFF64D964,
      3 => 0xFFE8A445,
      4 => 0xFFF08CC6,
      7 => 0xFFCC9FFF,
      _ => 0xFFFFFFFF,
    };

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

    private static void Label(string text)
    {
      ImGui.PushStyleColor(ImGuiCol.Text, LabelColor);
      ImGui.TextUnformatted(text);
      ImGui.PopStyleColor();
    }

    /// <summary>
    /// Draws a dim label on the left and a bright value pinned to the right edge.
    /// </summary>
    private static void Row(string label, string value)
    {
      Label(label);
      ImGui.SameLine();
      ImGui.SetCursorPosX(RightEdge() - ImGui.CalcTextSize(value).X);

      ImGui.PushStyleColor(ImGuiCol.Text, ValueColor);
      ImGui.TextUnformatted(value);
      ImGui.PopStyleColor();
    }

    /// <summary>
    /// Draws a stat line with the normal quality value and, where one exists, the high quality
    /// total in its own column against the right edge.
    /// </summary>
    private static void StatRow(string label, string value, string hqValue)
    {
      var right = RightEdge();
      var hqColumn = HqColumnWidth * ImGui.GetIO().FontGlobalScale;

      Label(label);

      if (value.Length > 0)
      {
        ImGui.SameLine();
        ImGui.SetCursorPosX(right - hqColumn - ImGui.CalcTextSize(value).X);

        ImGui.PushStyleColor(ImGuiCol.Text, ValueColor);
        ImGui.TextUnformatted(value);
        ImGui.PopStyleColor();
      }

      if (hqValue.Length == 0)
      {
        return;
      }

      ImGui.SameLine();
      ImGui.SetCursorPosX(right - ImGui.CalcTextSize(hqValue).X);

      ImGui.PushStyleColor(ImGuiCol.Text, HqColor);
      ImGui.TextUnformatted(hqValue);
      ImGui.PopStyleColor();
    }

    private static void DrawFlags(Item item)
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
        Label(string.Join("   ", flags));
      }
    }

    private static void DrawLevels(Item item)
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
        Row("Item Level", itemLevel.ToString(CultureInfo.CurrentCulture));
      }

      if (item.LevelEquip > 0)
      {
        Row("Equip Level", item.LevelEquip.ToString(CultureInfo.CurrentCulture));
      }

      if (jobs.Length > 0)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, ValueColor);
        ImGui.TextUnformatted(jobs);
        ImGui.PopStyleColor();
      }
    }

    private static void DrawCombatStats(Item item)
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
        Row(row.Label, row.Value);
      }
    }

    private static void DrawBonuses(Item item)
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

      if (rows.Count == 0)
      {
        return;
      }

      ImGui.Separator();

      if (rows.Exists(row => row.Hq.Length > 0))
      {
        ImGui.SetCursorPosX(RightEdge() - ImGui.CalcTextSize("HQ").X);
        Label("HQ");
      }

      foreach (var row in rows)
      {
        StatRow(row.Label, row.Value, row.Hq);
      }
    }

    private static void DrawSetBonus(Item item)
    {
      var name = item.ItemSpecialBonus.ValueNullable?.Name.ExtractText() ?? string.Empty;

      if (name.Length == 0)
      {
        return;
      }

      ImGui.Separator();
      Label(name);
    }

    private static void DrawCustomisation(Item item)
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
        Row(row.Label, row.Value);
      }
    }

    private static void DrawDescription(Item item)
    {
      var description = item.Description.ExtractText().Replace('\r', '\n');

      if (string.IsNullOrWhiteSpace(description))
      {
        return;
      }

      ImGui.Separator();

      ImGui.PushStyleColor(ImGuiCol.Text, DescriptionColor);
      ImGui.TextUnformatted(description);
      ImGui.PopStyleColor();
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

      ImGui.PushStyleColor(ImGuiCol.Text, RarityColor(item.Rarity));
      ImGui.TextUnformatted(item.Name.ExtractText());
      ImGui.PopStyleColor();

      var category = item.ItemUICategory.ValueNullable?.Name.ExtractText() ?? string.Empty;

      if (category.Length > 0)
      {
        Label(category);
      }

      ImGui.EndGroup();
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
        Row("Sells to vendor", item.PriceLow.ToString("C", this.context.Plugin.NumberFormatInfo));
      }

      if (hasStack)
      {
        Row("Stack", item.StackSize.ToString("N0", CultureInfo.CurrentCulture));
      }
    }
  }
}
