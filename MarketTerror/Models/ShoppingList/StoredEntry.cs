// <copyright file="StoredEntry.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;
  using System.Linq;

  /// <summary>
  /// A shopping list entry as it is written to the plugin configuration.
  /// </summary>
  /// <remarks>The game item itself cannot be serialised, so only its row id is kept.</remarks>
  public class StoredEntry
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="StoredEntry"/> class.
    /// </summary>
    public StoredEntry()
    {
      this.AnchorWorld = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoredEntry"/> class from a live entry.
    /// </summary>
    /// <param name="entry">The entry to write out.</param>
    public StoredEntry(ListingEntry entry)
    {
      ArgumentNullException.ThrowIfNull(entry);

      this.ItemId = entry.SourceItem.RowId;
      this.AnchorWorld = entry.Scope.AnchorWorld;
      this.Level = entry.Scope.Level;
      this.Kind = entry.Kind;
      this.Count = entry.Count;
      this.Quality = entry.Quality;
      this.Target = entry.Target == null ? null : new StoredListing(entry.Target);
      this.Conditions = entry.Conditions?.Clone();
      this.Matches.AddRange(entry.Matches.Select(m => new StoredListing(m)));
    }

    /// <summary>Gets or sets the row id of the item to buy.</summary>
    public uint ItemId { get; set; }

    /// <summary>Gets or sets the world the entry's scope reaches out from.</summary>
    public string AnchorWorld { get; set; }

    /// <summary>Gets or sets how wide the entry's scope reaches.</summary>
    public MarketScope Level { get; set; }

    /// <summary>Gets or sets how the entry chooses its listings.</summary>
    public ListingKind Kind { get; set; }

    /// <summary>Gets or sets how many listings a lowest entry takes.</summary>
    public int Count { get; set; } = 1;

    /// <summary>Gets or sets which qualities a lowest entry takes.</summary>
    public QualityFilter Quality { get; set; }

    /// <summary>Gets or sets the listing a direct entry stands for.</summary>
    public StoredListing? Target { get; set; }

    /// <summary>Gets or sets the rule a conditional entry buys by.</summary>
    public ListingConditions? Conditions { get; set; }

    /// <summary>Gets or sets the listings the entry last resolved to.</summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for JSON deserialization")]
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Rewritten wholesale whenever the shopping list changes")]
    public List<StoredListing> Matches { get; set; } = new List<StoredListing>();
  }
}
