// <copyright file="ConditionEditor.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Lumina.Excel.Sheets;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// The popup that writes the rule a conditional buy list entry buys by.
  /// </summary>
  /// <remarks>
  /// Everything is edited on a copy of the rule, so an entry keeps buying what it was buying until
  /// Save is pressed. The four groups sit behind collapsing headers because most rules only ever use
  /// one of them.
  /// </remarks>
  public sealed class ConditionEditor : IDisposable
  {
    /// <summary>
    /// The popup's ImGui id. Only one rule is edited at a time, so it does not need the entry in it.
    /// </summary>
    private const string PopupId = "shoppingListConditions";

    /// <summary>
    /// What the multi-select reads as while no world has been singled out.
    /// </summary>
    private const string AnyWorld = "Any world in the scope";

    private static readonly string[] QualityNames = new[] { "Any quality", "High quality only", "Normal quality only" };

    private static readonly string[] RetainerMatchNames = new[] { "Contains", "Is exactly" };

    private static readonly string[] CraftedNames = new[] { "Crafted or not", "Crafted only", "Not crafted" };

    private static readonly string[] DyeNames = new[] { "Dyed or not", "Undyed only", "Dyed only", "One dye" };

    private readonly MarketTerrorPlugin plugin;

    /// <summary>
    /// The worlds the open scope reaches, which is what the seller filter may be narrowed to.
    /// </summary>
    private readonly List<string> scopeWorlds = new List<string>();

    private Item item;

    private ListingScope? scope;

    private ListingEntry? entry;

    private ListingConditions? draft;

    private bool opening;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionEditor"/> class.
    /// </summary>
    /// <param name="plugin">The <see cref="MarketTerrorPlugin"/>.</param>
    public ConditionEditor(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    /// <summary>
    /// Opens the editor on a rule, either an entry's own or a new one.
    /// </summary>
    /// <param name="item">The item the rule buys.</param>
    /// <param name="scope">The market it buys in.</param>
    /// <param name="existing">The rule to start from, or null to start from nothing set.</param>
    /// <param name="entry">The entry being edited, or null when Save is to add a new one.</param>
    public void Open(Item item, ListingScope scope, ListingConditions? existing, ListingEntry? entry)
    {
      ArgumentNullException.ThrowIfNull(scope);

      this.item = item;
      this.scope = scope;
      this.entry = entry;

      // A copy, so an editor that is closed without saving leaves the entry's own rule alone.
      this.draft = existing?.Clone() ?? new ListingConditions();

      this.scopeWorlds.Clear();
      this.scopeWorlds.AddRange(this.WorldsInScope(scope));

      this.opening = true;
    }

    /// <summary>
    /// Draws the popup while a rule is open in it.
    /// </summary>
    /// <param name="theme">The theme to draw in.</param>
    public void Draw(TerrorTheme theme)
    {
      ArgumentNullException.ThrowIfNull(theme);

      if (this.draft == null || this.scope == null)
      {
        this.opening = false;
        return;
      }

      // The name is what the title bar shows; the id after it is what ImGui matches the popup on, so
      // the title can change with the entry without the popup counting as a different one.
      var label = this.item.Name.ExtractText() + " conditions##" + PopupId;

      if (this.opening)
      {
        ImGui.OpenPopup(label);
        this.opening = false;
      }

      var scale = ImGui.GetIO().FontGlobalScale;
      ImGui.SetNextWindowSize(new Vector2(540 * scale, 480 * scale), ImGuiCond.Appearing);

      var open = true;

      if (!ImGui.BeginPopupModal(label, ref open, ImGuiWindowFlags.NoSavedSettings))
      {
        // The popup is gone, so nothing is being edited any more.
        this.Close();
        return;
      }

      ModalDim.Mark();

      ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
      ImGui.TextWrapped(
        "Buys every listing in " + this.scope.Display(this.plugin.WorldCatalogue)
        + " that all of these hold for, cheapest first.");
      ImGui.PopStyleColor();

      ImGui.Separator();

      // The summary and the buttons keep their own rows under the inputs, so the inputs scroll alone.
      var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetTextLineHeightWithSpacing()
        + (ImGui.GetStyle().ItemSpacing.Y * 2);

      ImGui.BeginChild("conditionFields", new Vector2(0, -footerHeight), false);

      var width = 150 * scale;
      var textWidth = 200 * scale;

      this.DrawPriceAndStack(width);
      this.DrawSeller(scale, width, textWidth);
      this.DrawCrafted(width, textWidth);
      this.DrawCaps(width);

      ImGui.EndChild();

      ImGui.Separator();
      this.DrawSummary(theme);

      ImGui.BeginDisabled(!this.draft.IsSet);
      var save = ImGui.Button("Save");
      ImGui.EndDisabled();
      Utilities.HoverTooltip(
        this.draft.IsSet
          ? "Keep the rule and price this entry against it."
          : "A rule with nothing set would buy nothing, so at least one condition or cap has to be set.",
        ImGuiHoveredFlags.AllowWhenDisabled);

      ImGui.SameLine();

      var cancel = ImGui.Button("Cancel");

      if (save)
      {
        this.Save();
      }

      if (save || cancel)
      {
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndPopup();

      if (save || cancel || !open)
      {
        this.Close();
      }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.Close();
      this.isDisposed = true;
    }

    /// <summary>
    /// Squares a stored figure up with the whole number the input boxes edit.
    /// </summary>
    /// <param name="value">The stored figure.</param>
    /// <returns>The figure as a whole number a box can hold.</returns>
    private static int AsInt(double value)
    {
      return (int)Math.Clamp(value, 0, int.MaxValue);
    }

    /// <summary>
    /// Draws a whole number box that never goes below zero, where zero means the condition is unset.
    /// </summary>
    /// <param name="label">The box's label.</param>
    /// <param name="width">How wide the box is drawn.</param>
    /// <param name="value">What the box starts at.</param>
    /// <param name="setter">Takes the new figure when the box is changed.</param>
    /// <param name="tooltip">What the box says for itself.</param>
    private static void IntInput(string label, float width, int value, Action<int> setter, string tooltip)
    {
      var current = value;

      ImGui.SetNextItemWidth(width);

      if (ImGui.InputInt(label, ref current, 0, 0))
      {
        setter(Math.Max(0, current));
      }

      Utilities.HoverTooltip(tooltip);
    }

    /// <summary>
    /// Draws a text box holding a name to match on.
    /// </summary>
    /// <param name="label">The box's label.</param>
    /// <param name="width">How wide the box is drawn.</param>
    /// <param name="value">What the box starts at.</param>
    /// <param name="setter">Takes the new text when the box is changed.</param>
    /// <param name="tooltip">What the box says for itself.</param>
    private static void TextInput(string label, float width, string value, Action<string> setter, string tooltip)
    {
      var current = value;

      ImGui.SetNextItemWidth(width);

      if (ImGui.InputText(label, ref current, 64, ImGuiInputTextFlags.None))
      {
        setter(current);
      }

      Utilities.HoverTooltip(tooltip);
    }

    /// <summary>
    /// Draws a combo over the names of an enumeration.
    /// </summary>
    /// <param name="label">The combo's label.</param>
    /// <param name="width">How wide the combo is drawn.</param>
    /// <param name="names">What each value of the enumeration reads as.</param>
    /// <param name="index">The value that is picked, replaced when another one is.</param>
    /// <returns>True when another value was picked.</returns>
    private static bool EnumCombo(string label, float width, string[] names, ref int index)
    {
      var current = Math.Clamp(index, 0, names.Length - 1);
      var picked = -1;

      ImGui.SetNextItemWidth(width);

      if (ImGui.BeginCombo(label, names[current]))
      {
        for (var i = 0; i < names.Length; i++)
        {
          if (ImGui.Selectable(names[i], i == current))
          {
            picked = i;
          }

          if (i == current)
          {
            ImGui.SetItemDefaultFocus();
          }
        }

        ImGui.EndCombo();
      }

      if (picked < 0 || picked == index)
      {
        return false;
      }

      index = picked;
      return true;
    }

    /// <summary>
    /// Names the worlds a scope reaches, so the seller filter can be narrowed to some of them.
    /// </summary>
    /// <param name="scope">The market the rule buys in.</param>
    /// <returns>The world names, in reading order.</returns>
    private string[] WorldsInScope(ListingScope scope)
    {
      var catalogue = this.plugin.WorldCatalogue;
      var names = new List<string>();

      // A scope names a world, a data centre or a region, so each target is opened out into worlds.
      foreach (var target in scope.Targets(catalogue))
      {
        var world = catalogue.Find(target);

        if (world != null)
        {
          names.Add(world.Name);
          continue;
        }

        names.AddRange(catalogue.Worlds
          .Where(w => string.Equals(w.DataCentre, target, StringComparison.Ordinal)
            || string.Equals(w.Region, target, StringComparison.Ordinal))
          .Select(w => w.Name));
      }

      return names
        .Distinct(StringComparer.Ordinal)
        .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();
    }

    /// <summary>
    /// Draws what a listing may cost and how big its stack may be.
    /// </summary>
    /// <param name="width">How wide the boxes are drawn.</param>
    private void DrawPriceAndStack(float width)
    {
      if (!ImGui.CollapsingHeader("Price and stack##conditionPrice", ImGuiTreeNodeFlags.DefaultOpen))
      {
        return;
      }

      var draft = this.draft!;

      IntInput(
        "Least per unit",
        width,
        AsInt(draft.MinUnitPrice),
        v => draft.MinUnitPrice = v,
        "Skip listings cheaper than this per unit. 0 for no floor.");

      IntInput(
        "Most per unit",
        width,
        AsInt(draft.MaxUnitPrice),
        v => draft.MaxUnitPrice = v,
        "Skip listings dearer than this per unit. 0 for no ceiling.");

      IntInput(
        "Most per listing",
        width,
        AsInt(draft.MaxListingTotal),
        v => draft.MaxListingTotal = v,
        "Skip a listing whose whole stack costs more than this. 0 for no ceiling.");

      IntInput(
        "Smallest stack",
        width,
        AsInt(draft.MinQuantity),
        v => draft.MinQuantity = v,
        "Skip stacks smaller than this. 0 for any size.");

      IntInput(
        "Largest stack",
        width,
        AsInt(draft.MaxQuantity),
        v => draft.MaxQuantity = v,
        "Skip stacks larger than this. 0 for any size.");

      var quality = (int)draft.Quality;

      if (EnumCombo("Quality", width, QualityNames, ref quality))
      {
        draft.Quality = (QualityFilter)quality;
      }

      Utilities.HoverTooltip("Which qualities of the item will do.");
    }

    /// <summary>
    /// Draws who a listing may be bought from.
    /// </summary>
    /// <param name="scale">The font scale the popup is drawn at.</param>
    /// <param name="width">How wide the combos are drawn.</param>
    /// <param name="textWidth">How wide the name boxes are drawn.</param>
    private void DrawSeller(float scale, float width, float textWidth)
    {
      if (!ImGui.CollapsingHeader("Seller##conditionSeller"))
      {
        return;
      }

      var draft = this.draft!;

      ImGui.SetNextItemWidth(width);
      ImGui.SetNextWindowSizeConstraints(Vector2.Zero, new Vector2(float.MaxValue, 320 * scale));

      if (ImGui.BeginCombo("Worlds", this.WorldsPreview()))
      {
        if (ImGui.Selectable(AnyWorld, draft.Worlds.Count == 0))
        {
          draft.Worlds.Clear();
        }

        ImGui.Separator();

        foreach (var world in this.scopeWorlds)
        {
          var picked = draft.Worlds.Contains(world, StringComparer.OrdinalIgnoreCase);

          if (!ImGui.Checkbox(world, ref picked))
          {
            continue;
          }

          draft.Worlds.RemoveAll(w => string.Equals(w, world, StringComparison.OrdinalIgnoreCase));

          if (picked)
          {
            draft.Worlds.Add(world);
          }
        }

        ImGui.EndCombo();
      }

      Utilities.HoverTooltip("Only buy from these worlds inside the scope. None ticked means any of them.");

      var match = (int)draft.RetainerMatch;

      if (EnumCombo("##conditionRetainerMatch", 110 * scale, RetainerMatchNames, ref match))
      {
        draft.RetainerMatch = (RetainerMatch)match;
      }

      Utilities.HoverTooltip("Whether the retainer name only has to hold the text, or has to be exactly it.");

      ImGui.SameLine();

      TextInput(
        "Retainer name",
        textWidth,
        draft.RetainerName,
        v => draft.RetainerName = v,
        "Only buy from retainers whose name matches. Empty for any retainer.");
    }

    /// <summary>
    /// Draws what a listing has to have been made and melded and dyed as.
    /// </summary>
    /// <param name="width">How wide the boxes and combos are drawn.</param>
    /// <param name="textWidth">How wide the name boxes are drawn.</param>
    private void DrawCrafted(float width, float textWidth)
    {
      if (!ImGui.CollapsingHeader("Crafted##conditionCrafted"))
      {
        return;
      }

      var draft = this.draft!;
      var crafted = (int)draft.Crafted;

      if (EnumCombo("Crafted", width, CraftedNames, ref crafted))
      {
        draft.Crafted = (CraftedFilter)crafted;
      }

      Utilities.HoverTooltip("Whether the listing has to have been put up by a crafter.");

      TextInput(
        "Crafted by",
        textWidth,
        draft.CreatorName,
        v => draft.CreatorName = v,
        "Only buy listings whose crafter name holds this. Empty for any crafter.");

      IntInput(
        "Fewest materia",
        width,
        draft.MinMateria,
        v => draft.MinMateria = v,
        "Skip listings with fewer melded materia than this. 0 for any.");

      IntInput(
        "Most materia",
        width,
        draft.MaxMateria,
        v => draft.MaxMateria = v,
        "Skip listings with more melded materia than this. 0 for any.");

      var dye = (int)draft.Dye;

      if (EnumCombo("Dye", width, DyeNames, ref dye))
      {
        draft.Dye = (DyeFilter)dye;
      }

      Utilities.HoverTooltip("Whether the listing has to be carrying a dye.");

      if (draft.Dye != DyeFilter.Specific)
      {
        return;
      }

      IntInput(
        "Dye id",
        width,
        AsInt(draft.StainId),
        v => draft.StainId = (uint)Math.Max(0, v),
        "The stain row id of the one dye that will do.");
    }

    /// <summary>
    /// Draws how much the whole entry is allowed to take.
    /// </summary>
    /// <param name="width">How wide the boxes are drawn.</param>
    private void DrawCaps(float width)
    {
      if (!ImGui.CollapsingHeader("Caps##conditionCaps"))
      {
        return;
      }

      var draft = this.draft!;

      IntInput(
        "Most listings",
        width,
        draft.MaxListings,
        v => draft.MaxListings = v,
        "Take at most this many matching listings, cheapest first. 0 for no cap.");

      IntInput(
        "Most gil in all",
        width,
        AsInt(draft.MaxSpend),
        v => draft.MaxSpend = v,
        "Stop once the entry costs this much. A listing too dear for what is left is passed over. 0 for no cap.");
    }

    /// <summary>
    /// Draws what the rule as it stands would buy.
    /// </summary>
    /// <param name="theme">The theme to draw in.</param>
    private void DrawSummary(TerrorTheme theme)
    {
      var draft = this.draft!;

      ImGui.PushStyleColor(ImGuiCol.Text, draft.IsSet ? theme.TextDim : theme.BuyFailed);
      ImGui.TextWrapped(draft.IsSet
        ? "Buys " + draft.Summary()
        : "Nothing is set, so this rule would buy nothing.");
      ImGui.PopStyleColor();
    }

    /// <summary>
    /// Names the worlds the rule has been narrowed to, short enough to sit in the combo.
    /// </summary>
    /// <returns>The preview text.</returns>
    private string WorldsPreview()
    {
      var worlds = this.draft!.Worlds;

      return worlds.Count switch
      {
        0 => AnyWorld,
        1 => worlds[0],
        _ => worlds.Count.ToString(CultureInfo.CurrentCulture) + " worlds",
      };
    }

    /// <summary>
    /// Puts the draft on the entry, adding one when the editor was opened on a new rule, then prices it.
    /// </summary>
    private void Save()
    {
      var draft = this.draft;
      var scope = this.scope;

      if (draft == null || scope == null)
      {
        return;
      }

      var store = this.plugin.ShoppingList;
      ListingEntry saved;

      if (this.entry != null)
      {
        saved = this.entry;
        store.SetConditions(saved, draft);
      }
      else
      {
        saved = store.AddConditional(this.item, scope, draft);
      }

      // Only the one entry needs pricing again; the rest of the list still means what it said.
      this.plugin.ShoppingListBulkAdd.StartRefresh(new[] { saved }, saved.SourceItem.Name.ExtractText());
    }

    private void Close()
    {
      this.scope = null;
      this.entry = null;
      this.draft = null;
      this.scopeWorlds.Clear();
      this.opening = false;
    }
  }
}
