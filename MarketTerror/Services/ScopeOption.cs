// <copyright file="ScopeOption.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Collections.Generic;
  using MarketTerror.Helpers;
  using MarketTerror.Models;

  /// <summary>
  /// One entry of a scope picker: how wide it reaches and the markets it prices at.
  /// </summary>
  public sealed class ScopeOption
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeOption"/> class.
    /// </summary>
    /// <param name="scope">How wide the entry reaches.</param>
    /// <param name="targets">The world, data centre or region names it prices at.</param>
    /// <param name="display">The label to show, or null to name it after the targets.</param>
    public ScopeOption(MarketScope scope, IReadOnlyList<string> targets, string? display = null)
    {
      ArgumentNullException.ThrowIfNull(targets);

      this.Scope = scope;
      this.Targets = targets;
      this.Display = display ?? MarketScopeLabel.For(scope, targets);
    }

    /// <summary>
    /// Gets how wide the entry reaches.
    /// </summary>
    public MarketScope Scope { get; }

    /// <summary>
    /// Gets the world, data centre or region names the entry prices at.
    /// </summary>
    public IReadOnlyList<string> Targets { get; }

    /// <summary>
    /// Gets the label shown in the picker.
    /// </summary>
    public string Display { get; }

    /// <summary>
    /// Gets the name a single Universalis call is made against.
    /// </summary>
    /// <remarks>Anything reaching past it, such as the Oceania add-on, is fetched separately.</remarks>
    public string Query => this.Targets[0];
  }
}
