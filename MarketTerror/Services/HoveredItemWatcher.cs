// <copyright file="HoveredItemWatcher.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using Dalamud.Bindings.ImGui;
  using Lumina.Excel.Sheets;

  /// <summary>
  /// Watches the item hovered in the game's inventory windows and selects it once it has been hovered long enough.
  /// </summary>
  public sealed class HoveredItemWatcher : IDisposable
  {
    private readonly MarketTerrorPlugin plugin;

    private readonly ItemCatalog catalog;

    private readonly Action<uint> onDwellComplete;

    private ulong itemBeingHovered;

    private float progress;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HoveredItemWatcher"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="catalog">The item catalogue, used to ignore items the market board cannot show.</param>
    /// <param name="onDwellComplete">Invoked with the item row id once the dwell completes.</param>
    public HoveredItemWatcher(MarketTerrorPlugin plugin, ItemCatalog catalog, Action<uint> onDwellComplete)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
      this.onDwellComplete = onDwellComplete ?? throw new ArgumentNullException(nameof(onDwellComplete));

      this.plugin.GameGui.HoveredItemChanged += this.HandleHoveredItemChange!;
    }

    /// <summary>
    /// Gets how far the current dwell has progressed, from 0 to 1.
    /// </summary>
    public float Progress => this.progress;

    /// <summary>
    /// Advances the dwell timer by one frame and fires the callback when it completes.
    /// </summary>
    public void Tick()
    {
      if (this.itemBeingHovered != 0)
      {
        if (this.progress < 1.0f)
        {
          this.progress += ImGui.GetIO().DeltaTime;
        }
        else
        {
          this.progress = 0;
          var itemId = this.plugin.GameGui.HoveredItem;
          this.onDwellComplete(Convert.ToUInt32(itemId % 500000));
          this.itemBeingHovered = 0;
        }
      }
      else
      {
        this.progress = 0.0f;
      }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.plugin.GameGui.HoveredItemChanged -= this.HandleHoveredItemChange!;
      this.isDisposed = true;
    }

    private void HandleHoveredItemChange(object? sender, ulong itemId)
    {
      if (!this.plugin.Config.WatchForHovered || this.itemBeingHovered == itemId)
      {
        return;
      }

      this.progress = 0.0f;

      if (itemId == 0 || itemId >= 2000000)
      {
        this.itemBeingHovered = 0;
        return;
      }

      var item = this.plugin.DataManager.Excel.GetSheet<Item>().GetRowOrDefault((uint)itemId % 500000);

      if (item != null && this.catalog.Contains(item.Value.RowId))
      {
        this.itemBeingHovered = itemId;
      }
      else
      {
        this.itemBeingHovered = 0;
      }
    }
  }
}
