// <copyright file="ShoppingListNodes.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.ShoppingList
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using MarketTerror.Models;
  using MarketTerror.Models.ShoppingList;
  using MarketTerror.Services;

  /// <summary>
  /// Turns the flat shopping list into the two levels of groups the window draws it as.
  /// </summary>
  /// <remarks>
  /// This is a plain projection over the store, so the window rebuilds it whenever the list changes
  /// or the grouping setting does, and reads the frame off what it got back.
  /// </remarks>
  public static class ShoppingListNodes
  {
    /// <summary>
    /// Groups the shopping list into the tree the window draws.
    /// </summary>
    /// <param name="store">The shopping list.</param>
    /// <param name="catalogue">The world catalogue, used to name the scopes.</param>
    /// <param name="grouping">What the top level groups by.</param>
    /// <returns>The top level groups, in the order they are drawn.</returns>
    public static IReadOnlyList<ShoppingListNode> Build(ShoppingListStore store, WorldCatalogue catalogue, ShoppingListGrouping grouping)
    {
      ArgumentNullException.ThrowIfNull(store);
      ArgumentNullException.ThrowIfNull(catalogue);

      if (grouping == ShoppingListGrouping.ScopeFirst)
      {
        return Ordered(ByScope(store).Select(g => ScopeNode(g, catalogue, ItemLeaves(g))));
      }

      return Ordered(ByItem(store).Select(g => ItemNode(g, ScopeLeaves(g, catalogue))));
    }

    /// <summary>
    /// Splits entries into one bundle per item.
    /// </summary>
    /// <param name="entries">The entries to split.</param>
    /// <returns>The bundles, each holding the entries for one item.</returns>
    private static IEnumerable<ListingEntry[]> ByItem(IReadOnlyList<ListingEntry> entries)
    {
      return entries.GroupBy(e => e.SourceItem.RowId).Select(g => g.ToArray());
    }

    /// <summary>
    /// Splits entries into one bundle per scope.
    /// </summary>
    /// <param name="entries">The entries to split.</param>
    /// <returns>The bundles, each holding the entries for one scope.</returns>
    private static IEnumerable<ListingEntry[]> ByScope(IReadOnlyList<ListingEntry> entries)
    {
      return entries.GroupBy(e => e.Scope).Select(g => g.ToArray());
    }

    /// <summary>
    /// Puts a scope's entries under one item node each.
    /// </summary>
    /// <param name="entries">The entries of one scope.</param>
    /// <returns>The item nodes, in the order they are drawn.</returns>
    private static ShoppingListNode[] ItemLeaves(IReadOnlyList<ListingEntry> entries)
    {
      return Ordered(ByItem(entries).Select(g => ItemLeaf(g)));
    }

    /// <summary>
    /// Puts an item's entries under one scope node each.
    /// </summary>
    /// <param name="entries">The entries for one item.</param>
    /// <param name="catalogue">The world catalogue, used to name the scopes.</param>
    /// <returns>The scope nodes, in the order they are drawn.</returns>
    private static ShoppingListNode[] ScopeLeaves(IReadOnlyList<ListingEntry> entries, WorldCatalogue catalogue)
    {
      return Ordered(ByScope(entries).Select(g => ScopeLeaf(g, catalogue)));
    }

    /// <summary>
    /// Builds an item node holding other groups.
    /// </summary>
    /// <param name="entries">The entries for one item.</param>
    /// <param name="children">The groups to hang under it.</param>
    /// <returns>The node.</returns>
    private static ShoppingListNode ItemNode(IReadOnlyList<ListingEntry> entries, IReadOnlyList<ShoppingListNode> children)
    {
      var item = entries[0].SourceItem;

      return new ShoppingListNode(item.Name.ExtractText(), item, null, children);
    }

    /// <summary>
    /// Builds an item node holding entries.
    /// </summary>
    /// <param name="entries">The entries for one item.</param>
    /// <returns>The node.</returns>
    private static ShoppingListNode ItemLeaf(IReadOnlyList<ListingEntry> entries)
    {
      var item = entries[0].SourceItem;

      return new ShoppingListNode(item.Name.ExtractText(), item, null, entries);
    }

    /// <summary>
    /// Builds a scope node holding other groups.
    /// </summary>
    /// <param name="entries">The entries of one scope.</param>
    /// <param name="catalogue">The world catalogue, used to name the scope.</param>
    /// <param name="children">The groups to hang under it.</param>
    /// <returns>The node.</returns>
    private static ShoppingListNode ScopeNode(IReadOnlyList<ListingEntry> entries, WorldCatalogue catalogue, IReadOnlyList<ShoppingListNode> children)
    {
      var scope = entries[0].Scope;

      return new ShoppingListNode(scope.Display(catalogue), null, scope, children);
    }

    /// <summary>
    /// Builds a scope node holding entries.
    /// </summary>
    /// <param name="entries">The entries of one scope.</param>
    /// <param name="catalogue">The world catalogue, used to name the scope.</param>
    /// <returns>The node.</returns>
    private static ShoppingListNode ScopeLeaf(IReadOnlyList<ListingEntry> entries, WorldCatalogue catalogue)
    {
      var scope = entries[0].Scope;

      return new ShoppingListNode(scope.Display(catalogue), null, scope, entries);
    }

    /// <summary>
    /// Puts nodes in the order they are drawn, which is by the label they were built with.
    /// </summary>
    /// <param name="nodes">The nodes to order.</param>
    /// <returns>The ordered nodes.</returns>
    private static ShoppingListNode[] Ordered(IEnumerable<ShoppingListNode> nodes)
    {
      return nodes.OrderBy(n => n.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
  }
}
