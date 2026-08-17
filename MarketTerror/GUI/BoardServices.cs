// <copyright file="BoardServices.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI
{
  using System;
  using Dalamud.Interface.ManagedFontAtlas;
  using MarketTerror.GUI.Theme;
  using MarketTerror.Services;

  /// <summary>
  /// Everything the boards share: one catalogue, one market data cache, one status poll, one skin.
  /// </summary>
  /// <remarks>
  /// The font handles come from outside because the atlas builds them once for the whole plugin.
  /// </remarks>
  public sealed class BoardServices : IDisposable
  {
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardServices"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="defaultFont">The body font.</param>
    /// <param name="titleFont">The 1.5x font used for headings.</param>
    public BoardServices(MarketTerrorPlugin plugin, IFontHandle defaultFont, IFontHandle titleFont)
    {
      this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.DefaultFont = defaultFont ?? throw new ArgumentNullException(nameof(defaultFont));
      this.TitleFont = titleFont ?? throw new ArgumentNullException(nameof(titleFont));

      this.Theme = new TerrorTheme(plugin.Config);
      this.Catalog = new ItemCatalog(plugin.DataManager, plugin.Log);
      this.MarketDataCache = new MarketDataCache(plugin);
      this.ApiStatus = new ApiStatus(plugin);
    }

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public MarketTerrorPlugin Plugin { get; }

    /// <summary>
    /// Gets the Terror skin.
    /// </summary>
    public TerrorTheme Theme { get; }

    /// <summary>
    /// Gets the item catalogue every board reads from.
    /// </summary>
    public ItemCatalog Catalog { get; }

    /// <summary>
    /// Gets the market data every board shares.
    /// </summary>
    public MarketDataCache MarketDataCache { get; }

    /// <summary>
    /// Gets the API status poll every board reads.
    /// </summary>
    public ApiStatus ApiStatus { get; }

    /// <summary>
    /// Gets the body font.
    /// </summary>
    public IFontHandle DefaultFont { get; }

    /// <summary>
    /// Gets the 1.5x font used for headings.
    /// </summary>
    public IFontHandle TitleFont { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.ApiStatus.Dispose();
      this.isDisposed = true;
    }
  }
}
