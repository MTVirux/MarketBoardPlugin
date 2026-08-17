// <copyright file="DetachedBoardState.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using MarketTerror.GUI;

  /// <summary>
  /// A detached item list window, as it is stored between sessions.
  /// </summary>
  /// <remarks>Position and size are not here - ImGui keeps those against the window's name.</remarks>
  public class DetachedBoardState
  {
    /// <summary>
    /// Gets or sets the list this window is locked to.
    /// </summary>
    public ItemListTab Tab { get; set; }

    /// <summary>
    /// Gets or sets the world this window prices around, empty until one is resolved.
    /// </summary>
    public string ScopeWorld { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how wide this window prices around <see cref="ScopeWorld"/>.
    /// </summary>
    public MarketScope Scope { get; set; } = MarketScope.World;

    /// <summary>
    /// Gets or sets this window's search string.
    /// </summary>
    public string SearchString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row id of the item this window had selected, or 0 for none.
    /// </summary>
    public uint SelectedItem { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this window's advanced search options were open.
    /// </summary>
    public bool AdvancedSearchOpen { get; set; }

    /// <summary>
    /// Gets the item search categories this window is limited to.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Rewritten wholesale when the window is saved")]
    public List<uint> SelectedCategories { get; } = new List<uint>();

    /// <summary>
    /// Gets the item rarities this window is limited to.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Rewritten wholesale when the window is saved")]
    public List<byte> SelectedRarities { get; } = new List<byte>();

    /// <summary>
    /// Gets or sets the row id of the class job filter, or null for all classes.
    /// </summary>
    public uint? SelectedClassJob { get; set; }

    /// <summary>
    /// Gets or sets the minimum equip level filter.
    /// </summary>
    public int MinLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum equip level filter.
    /// </summary>
    public int MaxLevel { get; set; } = MarketBoardContext.DefaultMaxLevel;

    /// <summary>
    /// Gets or sets the minimum item level filter.
    /// </summary>
    public int MinItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum item level filter.
    /// </summary>
    public int MaxItemLevel { get; set; } = MarketBoardContext.DefaultMaxItemLevel;

    /// <summary>
    /// Gets or sets the unlock state this window is limited to, or null for every item.
    /// </summary>
    public bool? UnlockFilter { get; set; }
  }
}
