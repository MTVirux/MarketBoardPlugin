// <copyright file="ListingScope.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using MarketTerror.Helpers;
  using MarketTerror.Services;

  /// <summary>
  /// The market a shopping list entry shops in: a world to anchor on, and how wide to reach around it.
  /// </summary>
  /// <remarks>
  /// A stored scope names its own region through its anchor world rather than through whoever is logged
  /// in, so an entry keeps meaning the same market wherever the player is standing.
  /// </remarks>
  public sealed class ListingScope : IEquatable<ListingScope>
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ListingScope"/> class.
    /// </summary>
    /// <param name="anchorWorld">The world the scope reaches out from.</param>
    /// <param name="level">How wide it reaches.</param>
    public ListingScope(string anchorWorld, MarketScope level)
    {
      this.AnchorWorld = anchorWorld ?? string.Empty;
      this.Level = level;
    }

    /// <summary>Gets the world the scope reaches out from.</summary>
    public string AnchorWorld { get; }

    /// <summary>Gets how wide the scope reaches.</summary>
    public MarketScope Level { get; }

    /// <summary>
    /// Names the markets a query for this scope is made against.
    /// </summary>
    /// <param name="catalogue">The world catalogue.</param>
    /// <returns>The world, data centre or region names, or an empty list when the anchor is unknown.</returns>
    public IReadOnlyList<string> Targets(WorldCatalogue catalogue)
    {
      ArgumentNullException.ThrowIfNull(catalogue);

      var anchor = catalogue.Find(this.AnchorWorld);

      if (anchor == null)
      {
        return Array.Empty<string>();
      }

      return this.Level switch
      {
        MarketScope.DataCentre => new[] { anchor.DataCentre },
        MarketScope.Region => new[] { anchor.Region },
        MarketScope.RegionWithOceania => anchor.Region == WorldRegions.Oceania
          ? new[] { anchor.Region }
          : new[] { anchor.Region, WorldRegions.Oceania },
        _ => new[] { anchor.Name },
      };
    }

    /// <summary>
    /// Names the scope the way the scope picker names its own entries.
    /// </summary>
    /// <param name="catalogue">The world catalogue.</param>
    /// <returns>The label.</returns>
    public string Display(WorldCatalogue catalogue)
    {
      var targets = this.Targets(catalogue);

      return targets.Count == 0 ? this.AnchorWorld : MarketScopeLabel.For(this.Level, targets);
    }

    /// <inheritdoc/>
    public bool Equals(ListingScope? other)
    {
      return other != null
        && other.Level == this.Level
        && string.Equals(other.AnchorWorld, this.AnchorWorld, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => this.Equals(obj as ListingScope);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
      return HashCode.Combine(this.AnchorWorld.ToUpperInvariant(), this.Level);
    }
  }
}
