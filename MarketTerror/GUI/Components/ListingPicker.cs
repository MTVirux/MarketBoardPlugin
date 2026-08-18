// <copyright file="ListingPicker.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Net.Http;
  using System.Numerics;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Game.Text;
  using MarketTerror.Extensions;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Helpers;
  using MarketTerror.Models.ShoppingList;
  using MarketTerror.Models.Universalis;
  using MarketTerror.Services;

  /// <summary>
  /// The popup that chooses which Market Board listings a shopping list row buys.
  /// </summary>
  /// <remarks>
  /// Nothing is ticked for you: the row buys the listings that are ticked here and nothing else, and
  /// its price, stack size, total and world are read back off them. A row can hand that job to a
  /// listing limit instead, which ticks everything under a price for as long as it fits under a total
  /// and does so again every time the row is priced.
  /// </remarks>
  public sealed class ListingPicker : IDisposable
  {
    /// <summary>
    /// The popup's ImGui id. Only one row is picked at a time, so it does not need the row in it.
    /// </summary>
    private const string PopupId = "shoppingListPicker";

    /// <summary>
    /// How many listings to ask Universalis for. More than a market board can show in one go.
    /// </summary>
    private const int ListingCount = 100;

    private readonly MarketTerrorPlugin plugin;

    private readonly List<Candidate> candidates = new List<Candidate>();

    private SavedItem? row;

    private bool opening;

    private bool loading;

    private string failure = string.Empty;

    private CancellationTokenSource? cancellation;

    /// <summary>
    /// The rule being edited, or null while the listings are being ticked by hand. A copy, so a popup
    /// that is cancelled leaves the row's own rule alone.
    /// </summary>
    private ListingLimit? draft;

    private ListingLimitScope? draftScope;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListingPicker"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public ListingPicker(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    /// <summary>
    /// Gets the markets the popup fetches from, which is the shopping list's scope unless the rule
    /// being edited has been pointed somewhere else.
    /// </summary>
    private IReadOnlyList<string> Targets =>
      this.draft is { HasOwnScope: true } && this.draftScope != null
        ? this.draftScope.QueryTargets
        : this.plugin.ShoppingListScope.QueryTargets;

    /// <summary>
    /// Gets the label of the scope the popup fetches from.
    /// </summary>
    private string ScopeLabel =>
      this.draft is { HasOwnScope: true } && this.draftScope != null
        ? this.draftScope.SelectedDisplayName
        : this.plugin.ShoppingListScope.SelectedDisplayName;

    /// <summary>
    /// Opens the popup for a row and starts fetching the listings to choose from.
    /// </summary>
    /// <param name="item">The row to pick listings for.</param>
    public void Open(SavedItem item)
    {
      ArgumentNullException.ThrowIfNull(item);

      this.CancelFetch();

      this.row = item;
      this.opening = true;
      this.failure = string.Empty;
      this.candidates.Clear();

      this.draft = item.Limit?.Clone();
      this.draftScope = this.draft == null ? null : new ListingLimitScope(this.plugin, this.draft);

      // What the row already buys, so closing the popup without a fetch landing changes nothing.
      this.candidates.AddRange(item.Picks.Select(pick => new Candidate(pick, true)));

      this.StartFetch();
    }

    /// <summary>
    /// Draws the popup when a row has it open.
    /// </summary>
    /// <param name="theme">The theme to draw in.</param>
    public void Draw(TerrorTheme theme)
    {
      ArgumentNullException.ThrowIfNull(theme);

      if (this.row == null)
      {
        this.opening = false;
        return;
      }

      var item = this.row;

      // The name is what the title bar shows; the id after it is what ImGui matches the popup on,
      // so the title can change with the row without the popup counting as a different one.
      var label = $"{item.SourceItem.Name.ExtractText()} Listings##{PopupId}";

      if (this.opening)
      {
        ImGui.OpenPopup(label);
        this.opening = false;
      }

      var scale = ImGui.GetIO().FontGlobalScale;
      ImGui.SetNextWindowSize(new Vector2(720 * scale, 480 * scale), ImGuiCond.Appearing);

      var open = true;

      if (!ImGui.BeginPopupModal(label, ref open, ImGuiWindowFlags.NoSavedSettings))
      {
        // The popup is gone, so nothing is being picked any more.
        this.Close();
        return;
      }

      this.DrawLimit(theme);

      ImGui.Separator();

      var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;

      this.DrawTable(theme, footerHeight);

      ImGui.Separator();
      this.DrawFooter(theme, item);

      ImGui.EndPopup();

      if (!open)
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

      this.CancelFetch();
      this.isDisposed = true;
    }

    private static void RightAligned(string text)
    {
      var padding = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X;

      if (padding > 0)
      {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
      }

      ImGui.Text(text);
    }

    private void CancelFetch()
    {
      this.cancellation?.Cancel();
      this.cancellation?.Dispose();
      this.cancellation = null;
    }

    /// <summary>
    /// Fetches the listings of the open row across the scope in force, dropping any fetch still in flight.
    /// </summary>
    private void StartFetch()
    {
      if (this.row == null)
      {
        return;
      }

      this.CancelFetch();

      var item = this.row;
      var targets = this.Targets.ToArray();

      this.loading = true;
      this.cancellation = new CancellationTokenSource();
      var token = this.cancellation.Token;

      _ = Task.Run(() => this.Fetch(item, targets, token), token);
    }

    private void Close()
    {
      this.CancelFetch();
      this.row = null;
      this.loading = false;
      this.candidates.Clear();
      this.failure = string.Empty;
      this.draft = null;
      this.draftScope = null;
    }

    /// <summary>
    /// Fetches every listing of the row's item across the scope in force.
    /// </summary>
    /// <param name="item">The row being picked for.</param>
    /// <param name="targets">The worlds, data centres or regions to fetch from.</param>
    /// <param name="token">Cancels the fetch when the popup closes.</param>
    /// <returns>A task that completes once the candidates have been handed to the framework thread.</returns>
    private async Task Fetch(SavedItem item, IReadOnlyList<string> targets, CancellationToken token)
    {
      var found = new List<(MarketDataListing Listing, string Target)>();
      var error = string.Empty;

      foreach (var target in targets)
      {
        try
        {
          var data = await this.plugin.UniversalisClient
            .GetMarketData(item.SourceItem.RowId, target, ListingCount, 0, token)
            .ConfigureAwait(false);

          foreach (var listing in data.Listings)
          {
            found.Add((listing, data.WorldName ?? target));
          }
        }
        catch (OperationCanceledException)
        {
          return;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
          this.plugin.Log.Warning(ex, $"Could not fetch the listings of item {item.SourceItem.RowId} on {target}.");
          error = "Universalis could not be reached for part of the scope.";
        }
      }

      if (token.IsCancellationRequested)
      {
        return;
      }

      // The candidates are read while the popup draws, so they may only be swapped on the framework thread.
      await this.plugin.Framework
        .RunOnFrameworkThread(() => this.Apply(item, found, error))
        .ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the fetched listings into candidates, keeping the row's existing picks ticked.
    /// </summary>
    /// <param name="item">The row being picked for.</param>
    /// <param name="found">The listings, paired with the target they came back from.</param>
    /// <param name="error">What went wrong, or an empty string when nothing did.</param>
    private void Apply(SavedItem item, List<(MarketDataListing Listing, string Target)> found, string error)
    {
      if (!ReferenceEquals(this.row, item))
      {
        // The popup moved on to another row while the fetch was in flight.
        return;
      }

      var withTax = !this.plugin.Config.NoGilSalesTax;
      var seen = new HashSet<string>(StringComparer.Ordinal);
      var fresh = new List<Candidate>();

      // A scope can ask a world and its data centre in the same breath, so the same listing comes twice.
      foreach (var (listing, target) in found.OrderBy(f => PickedListing.UnitPrice(f.Listing, withTax)))
      {
        if (listing.ListingId.Length > 0 && !seen.Add(listing.ListingId))
        {
          continue;
        }

        fresh.Add(new Candidate(PickedListing.FromListing(listing, withTax, target), false));
      }

      // A rule picks the row again from what is on sale now, so what it used to buy says nothing.
      if (this.draft == null)
      {
        // Everything the row already buys stays on the list, ticked, whether or not it is still on sale.
        // One retainer can have two stacks that look alike, so a candidate only answers for one pick.
        var claimed = new HashSet<Candidate>();

        foreach (var pick in item.Picks)
        {
          var match = fresh.Find(c => !claimed.Contains(c) && pick.SameAs(c.Pick));

          if (match != null)
          {
            // The fresh candidate carries the listing's current price, which is the one that gets paid.
            claimed.Add(match);
            match.Ticked = true;
            continue;
          }

          // A copy, so a popup that is cancelled leaves the row exactly as it found it.
          var missing = new PickedListing(pick.Price, pick.Quantity, pick.Hq, pick.World, pick.RetainerName, pick.ListingId)
          {
            Gone = true,
          };

          fresh.Add(new Candidate(missing, true));
        }
      }

      this.candidates.Clear();
      this.candidates.AddRange(fresh);
      this.loading = false;
      this.failure = error;
      this.ApplyDraft();
    }

    /// <summary>
    /// Ticks the listings the rule being edited buys and unticks the rest, leaving the ticks alone
    /// while the listings are being picked by hand.
    /// </summary>
    private void ApplyDraft()
    {
      if (this.draft == null)
      {
        return;
      }

      var chosen = this.draft.Apply(this.candidates.Select(c => c.Pick)).ToHashSet();

      foreach (var candidate in this.candidates)
      {
        candidate.Ticked = chosen.Contains(candidate.Pick);
      }
    }

    /// <summary>
    /// Hands the row's listings over to a rule, or takes them back to being ticked by hand.
    /// </summary>
    /// <param name="limited">True to pick the listings by rule.</param>
    private void ToggleLimit(bool limited)
    {
      if (limited == (this.draft != null))
      {
        return;
      }

      if (!limited)
      {
        this.draft = null;
        this.draftScope = null;
        return;
      }

      this.draft = this.row?.Limit?.Clone() ?? new ListingLimit();
      this.draftScope = new ListingLimitScope(this.plugin, this.draft);

      // A rule only ever picks what is on sale, so the sold out listings have nothing left to say.
      this.candidates.RemoveAll(c => c.Pick.Gone);
      this.ApplyDraft();

      if (this.draft.HasOwnScope)
      {
        this.StartFetch();
      }
    }

    /// <summary>
    /// Points the rule at a market of its own, or back at the shopping list's.
    /// </summary>
    /// <param name="own">True to give the rule its own scope.</param>
    private void SetOwnScope(bool own)
    {
      if (this.draft == null || this.draft.HasOwnScope == own)
      {
        return;
      }

      this.draft.HasOwnScope = own;

      if (own && this.draft.ScopeWorld.Length == 0)
      {
        // Start where the shopping list is, so ticking the box alone changes nothing.
        this.draft.Scope = this.plugin.ShoppingListScope.Scope;
        this.draft.ScopeWorld = this.plugin.ShoppingListScope.SelectedWorld;
      }

      this.StartFetch();
    }

    /// <summary>
    /// Draws the listing limit editor, or the scope the listings came from when there is no rule.
    /// </summary>
    /// <param name="theme">The theme to draw in.</param>
    private void DrawLimit(TerrorTheme theme)
    {
      var limited = this.draft != null;

      if (ImGui.Checkbox("Limit listings", ref limited))
      {
        this.ToggleLimit(limited);
      }

      Utilities.HoverTooltip(
        "Buy every listing at or under a price, cheapest first, for as long as they fit under a total."
        + "\nThe row is picked again from scratch every time it is priced.");

      ImGui.SameLine();

      if (this.draft == null)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
        ImGui.Text(this.ScopeLabel);
        ImGui.PopStyleColor();
        return;
      }

      var scale = ImGui.GetIO().FontGlobalScale;
      var width = 110 * scale;
      var unit = (int)Math.Clamp(this.draft.MaxUnitPrice, 0, int.MaxValue);
      var total = (int)Math.Clamp(this.draft.MaxTotal, 0, int.MaxValue);

      ImGui.Text("under");
      ImGui.SameLine();
      ImGui.SetNextItemWidth(width);

      if (ImGui.InputInt("##limitUnit", ref unit, 0, 0))
      {
        this.draft.MaxUnitPrice = Math.Max(0, unit);
        this.ApplyDraft();
      }

      Utilities.HoverTooltip("The most that may be paid per unit. 0 for no limit on the price.");

      ImGui.SameLine();
      ImGui.Text("a unit, up to");
      ImGui.SameLine();
      ImGui.SetNextItemWidth(width);

      if (ImGui.InputInt("##limitTotal", ref total, 0, 0))
      {
        this.draft.MaxTotal = Math.Max(0, total);
        this.ApplyDraft();
      }

      Utilities.HoverTooltip("The most that may be spent on this row in all. 0 for no limit on the total.");

      ImGui.SameLine();
      ImGui.Text("gil in all");

      this.DrawLimitScope(theme, scale);
    }

    /// <summary>
    /// Draws the market the rule sweeps, which is the shopping list's until it is given one of its own.
    /// </summary>
    /// <param name="theme">The theme to draw in.</param>
    /// <param name="scale">The font scale the popup is drawn at.</param>
    private void DrawLimitScope(TerrorTheme theme, float scale)
    {
      var own = this.draft!.HasOwnScope;

      if (ImGui.Checkbox("Own scope", ref own))
      {
        this.SetOwnScope(own);
      }

      Utilities.HoverTooltip("Sweep a market of this row's own rather than the one the shopping list is set to.");

      ImGui.SameLine();

      if (!own || this.draftScope == null)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
        ImGui.Text(this.ScopeLabel);
        ImGui.PopStyleColor();
        return;
      }

      var label = this.draftScope.SelectedDisplayName;
      var comboWidth = Math.Max(
        140 * scale,
        ImGui.CalcTextSize(label).X + ImGui.GetFrameHeight() + (ImGui.GetStyle().FramePadding.X * 2));

      ImGui.SetNextItemWidth(Math.Min(comboWidth, Math.Max(120 * scale, ImGui.GetContentRegionAvail().X)));
      ScopePicker.Draw("##limitScope", this.draftScope, this.StartFetch);
    }

    private void DrawTable(TerrorTheme theme, float footerHeight)
    {
      if (this.candidates.Count == 0)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
        ImGui.TextWrapped(this.loading ? "Loading listings..." : "Nothing of this item is on sale in the scope.");
        ImGui.PopStyleColor();
        return;
      }

      var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable
        | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

      if (!ImGui.BeginTable("shoppingListPickerTable", 6, flags, new Vector2(0, -footerHeight)))
      {
        return;
      }

      var tickWidth = ImGui.GetFrameHeight() + (ImGui.GetStyle().CellPadding.X * 2);

      ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, tickWidth);
      ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeightWithSpacing() * 1.5f);
      ImGui.TableSetupColumn("Price");
      ImGui.TableSetupColumn("Qty");
      ImGui.TableSetupColumn("Total");
      ImGui.TableSetupColumn("Retainer");
      ImGui.TableSetupScrollFreeze(0, 1);

      ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
      ImGui.TableHeadersRow();
      ImGui.PopStyleColor();

      for (var index = 0; index < this.candidates.Count; index++)
      {
        this.DrawCandidate(theme, this.candidates[index], index);
      }

      ImGui.EndTable();
    }

    private void DrawCandidate(TerrorTheme theme, Candidate candidate, int index)
    {
      var pick = candidate.Pick;

      ImGui.TableNextRow();

      ImGui.TableSetColumnIndex(0);

      var ticked = candidate.Ticked;

      // The rule owns the ticks while one is on, so they only say what it chose.
      ImGui.BeginDisabled(this.draft != null);

      if (ImGui.Checkbox($"##pick{index}", ref ticked) && this.draft == null)
      {
        candidate.Ticked = ticked;
      }

      ImGui.EndDisabled();

      if (this.draft != null)
      {
        Utilities.HoverTooltip("The listing limit picks this row. Untick it above to choose by hand.", ImGuiHoveredFlags.AllowWhenDisabled);
      }

      ImGui.TableSetColumnIndex(1);

      if (pick.Hq)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, theme.TextBright);
        ImGui.Text(SeIconChar.HighQuality.AsString());
        ImGui.PopStyleColor();
      }

      ImGui.TableSetColumnIndex(2);
      this.DrawGil(theme, pick.Price, pick.Gone);

      ImGui.TableSetColumnIndex(3);
      ImGui.PushStyleColor(ImGuiCol.Text, pick.Gone ? theme.TextDim : theme.Text);
      RightAligned(pick.Quantity.ToString("N0", CultureInfo.CurrentCulture));
      ImGui.PopStyleColor();

      ImGui.TableSetColumnIndex(4);
      this.DrawGil(theme, pick.Total, pick.Gone);

      ImGui.TableSetColumnIndex(5);
      ImGui.PushStyleColor(ImGuiCol.Text, pick.Gone ? theme.TextDim : theme.Text);
      ImGui.Text($"{pick.RetainerName} {SeIconChar.CrossWorld.ToChar()} {pick.World}{(pick.Gone ? " - gone" : string.Empty)}");
      ImGui.PopStyleColor();

      if (pick.Gone)
      {
        Utilities.HoverTooltip("This listing was not there the last time the item was priced. Untick it to drop it.");
      }
    }

    private void DrawGil(TerrorTheme theme, double value, bool dim)
    {
      var text = this.plugin.Config.PriceIconShown
        ? value.ToString("C", this.plugin.NumberFormatInfo)
        : value.ToString("N0", CultureInfo.CurrentCulture);

      ImGui.PushStyleColor(ImGuiCol.Text, dim ? theme.TextDim : theme.GilText);
      RightAligned(text);
      ImGui.PopStyleColor();
    }

    private void DrawFooter(TerrorTheme theme, SavedItem item)
    {
      var ticked = this.candidates.Where(c => c.Ticked).ToArray();
      var units = ticked.Sum(c => c.Pick.Quantity);
      var gil = ticked.Sum(c => c.Pick.Total);

      var summary = ticked.Length switch
      {
        0 when this.draft is { IsSet: false } => "Set a price or a total for the limit to pick anything.",
        0 when this.draft != null => "Nothing in the scope is under the limit - saving leaves this row with nothing to buy.",
        0 => "Nothing selected - saving takes every listing off this row.",
        _ => $"Selected: {units.ToString("N0", CultureInfo.CurrentCulture)} units over {ticked.Length} listings - {gil.ToString("N0", CultureInfo.CurrentCulture)} gil",
      };

      ImGui.PushStyleColor(ImGuiCol.Text, ticked.Length == 0 ? theme.TextDim : theme.GilText);
      ImGui.Text(summary);
      ImGui.PopStyleColor();

      if (this.failure.Length > 0)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, theme.BuyFailed);
        ImGui.Text(this.failure);
        ImGui.PopStyleColor();
      }

      ImGui.BeginDisabled(this.loading);

      if (ImGui.Button("Save"))
      {
        this.plugin.ShoppingList.SetLimit(item, this.draft, ticked.Select(c => c.Pick));
        this.Close();
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndDisabled();

      ImGui.SameLine();

      if (ImGui.Button("Cancel"))
      {
        this.Close();
        ImGui.CloseCurrentPopup();
      }

      ImGui.SameLine();

      ImGui.BeginDisabled(ticked.Length == 0 || this.draft != null);

      if (ImGui.Button("Clear all"))
      {
        foreach (var candidate in this.candidates)
        {
          candidate.Ticked = false;
        }
      }

      ImGui.EndDisabled();
      Utilities.HoverTooltip(
        this.draft != null
          ? "The listing limit picks this row, so there is nothing to untick."
          : "Untick every listing. Save afterwards to take them off the row.",
        ImGuiHoveredFlags.AllowWhenDisabled);

      if (this.loading && this.candidates.Count > 0)
      {
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, theme.TextDim);
        ImGui.Text("Still loading - these are the listings the row already buys.");
        ImGui.PopStyleColor();
      }
    }

    /// <summary>
    /// One listing the popup offers, and whether the row is buying it.
    /// </summary>
    private sealed class Candidate
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="Candidate"/> class.
      /// </summary>
      /// <param name="pick">The listing.</param>
      /// <param name="ticked">True when the row is buying it.</param>
      public Candidate(PickedListing pick, bool ticked)
      {
        this.Pick = pick;
        this.Ticked = ticked;
      }

      /// <summary>Gets the listing.</summary>
      public PickedListing Pick { get; }

      /// <summary>Gets or sets a value indicating whether the row is buying it.</summary>
      public bool Ticked { get; set; }
    }
  }
}
