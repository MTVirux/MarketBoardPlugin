// <copyright file="ShoppingListScope.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using MarketTerror.Models;

  /// <summary>
  /// The world the shopping list shops from and how wide it prices around it.
  /// </summary>
  /// <remarks>
  /// Kept apart from <see cref="WorldSelection"/> on purpose: the buy list can be priced somewhere
  /// else entirely without moving the market board window's own world combo.
  /// </remarks>
  public sealed class ShoppingListScope : MarketScopeSelection
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingListScope"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public ShoppingListScope(MarketTerrorPlugin plugin)
      : base(plugin)
    {
    }

    /// <inheritdoc/>
    protected override string StoredWorld
    {
      get => this.Plugin.Config.ShoppingListScopeWorld;
      set => this.Plugin.Config.ShoppingListScopeWorld = value;
    }

    /// <inheritdoc/>
    protected override MarketScope StoredScope
    {
      get => this.Plugin.Config.ShoppingListScopeLevel;
      set => this.Plugin.Config.ShoppingListScopeLevel = value;
    }
  }
}
