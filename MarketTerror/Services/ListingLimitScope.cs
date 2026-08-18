// <copyright file="ListingLimitScope.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using MarketTerror.Models;
  using MarketTerror.Models.ShoppingList;

  /// <summary>
  /// The world a listing limit sweeps around and how wide it reaches, as the two pickers its editor shows.
  /// </summary>
  /// <remarks>
  /// Nothing is written to the configuration when this moves: a limit being edited is a draft until the
  /// popup saves it, so the caller is told instead and decides whether the change is worth keeping.
  /// </remarks>
  public sealed class ListingLimitScope : MarketScopeSelection
  {
    private readonly ListingLimit limit;

    private readonly Action? onChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListingLimitScope"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="limit">The limit whose scope is being picked.</param>
    /// <param name="onChanged">Called when the scope moved, or null when nothing needs telling.</param>
    public ListingLimitScope(MarketTerrorPlugin plugin, ListingLimit limit, Action? onChanged = null)
      : base(plugin)
    {
      this.limit = limit ?? throw new ArgumentNullException(nameof(limit));
      this.onChanged = onChanged;
    }

    /// <inheritdoc/>
    protected override string StoredWorld
    {
      get => this.limit.ScopeWorld;
      set => this.limit.ScopeWorld = value;
    }

    /// <inheritdoc/>
    protected override MarketScope StoredScope
    {
      get => this.limit.Scope;
      set => this.limit.Scope = value;
    }

    /// <summary>
    /// Names the markets a row is priced against, which is the shopping list's scope unless the row's
    /// limit has been pointed somewhere else.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="row">The row being priced.</param>
    /// <param name="fallback">The shopping list's own targets.</param>
    /// <returns>The world, data centre or region names to fetch from.</returns>
    public static IReadOnlyList<string> TargetsFor(MarketTerrorPlugin plugin, SavedItem row, IReadOnlyList<string> fallback)
    {
      ArgumentNullException.ThrowIfNull(row);

      if (row.Limit is not { HasOwnScope: true } limit)
      {
        return fallback;
      }

      var targets = new ListingLimitScope(plugin, limit).QueryTargets;

      return targets.Count > 0 ? targets : fallback;
    }

    /// <inheritdoc/>
    protected override void Save()
    {
      this.InvalidateOptions();
      this.onChanged?.Invoke();
    }
  }
}
