// <copyright file="ShoppingListButton.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using Dalamud.Interface.Windowing;

  /// <summary>
  /// The title bar button showing and hiding the shopping list window.
  /// </summary>
  public static class ShoppingListButton
  {
    private static readonly Vector4 EmptyColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    private static readonly Vector4 FilledColor = new(0.35f, 0.85f, 0.45f, 1.0f);

    /// <summary>
    /// Builds the shopping list button.
    /// </summary>
    /// <param name="context">The shared market board state the shopping list is read from.</param>
    /// <returns>The title bar button.</returns>
    public static TitleBarButton Build(MarketBoardContext context)
    {
      ArgumentNullException.ThrowIfNull(context);

      return new TitleBarButton
      {
        Icon = FontAwesomeIcon.ShoppingCart,
        IconOffset = new Vector2(2, 1),
        IconColor = IconColor(context),
        Click = _ => context.Plugin.ToggleShoppingList(),
        ShowTooltip = () => DrawTooltip(context),
      };
    }

    /// <summary>
    /// Repaints the button for the current shopping list.
    /// </summary>
    /// <remarks>
    /// IconColor is a property, not a callback, so the owning window has to refresh it from PreDraw,
    /// before ImGui lays the title bar out.
    /// </remarks>
    /// <param name="button">The button built by <see cref="Build"/>.</param>
    /// <param name="context">The shared market board state the shopping list is read from.</param>
    public static void Refresh(TitleBarButton button, MarketBoardContext context)
    {
      ArgumentNullException.ThrowIfNull(button);
      ArgumentNullException.ThrowIfNull(context);

      button.IconColor = IconColor(context);
    }

    private static Vector4 IconColor(MarketBoardContext context)
    {
      return context.Plugin.ShoppingList.Count > 0 ? FilledColor : EmptyColor;
    }

    private static void DrawTooltip(MarketBoardContext context)
    {
      var count = context.Plugin.ShoppingList.Count;

      ImGui.BeginTooltip();
      ImGui.Text(count > 0 ? $"Shopping list ({count})" : "Shopping list");
      ImGui.EndTooltip();
    }
  }
}
