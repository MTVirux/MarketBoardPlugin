// <copyright file="ItemList.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ItemLists
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;

  /// <summary>
  /// A named collection of items the user put together, as it is stored between sessions.
  /// </summary>
  public class ItemList
  {
    /// <summary>
    /// Gets or sets what identifies this list, so a rename never breaks a reference to it.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name the list is shown under. Names are not required to be unique.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the row ids of the items on this list, in the order the user put them in.
    /// </summary>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Reordered and rewritten in place by the store")]
    public List<uint> ItemIds { get; } = new List<uint>();
  }
}
