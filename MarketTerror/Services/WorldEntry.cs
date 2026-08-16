// <copyright file="WorldEntry.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  /// <summary>
  /// A world and the data centre and region it sits in.
  /// </summary>
  public sealed class WorldEntry
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldEntry"/> class.
    /// </summary>
    /// <param name="name">The world name.</param>
    /// <param name="dataCentre">The name of the data centre the world belongs to.</param>
    /// <param name="region">The name of the region the data centre belongs to.</param>
    public WorldEntry(string name, string dataCentre, string region)
    {
      this.Name = name;
      this.DataCentre = dataCentre;
      this.Region = region;
    }

    /// <summary>
    /// Gets the world name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name of the data centre the world belongs to.
    /// </summary>
    public string DataCentre { get; }

    /// <summary>
    /// Gets the name of the region the data centre belongs to.
    /// </summary>
    public string Region { get; }
  }
}
