// <copyright file="Integration.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System.Numerics;

  /// <summary>
  /// One optional plugin or service MarketTerror works with but never requires. The wording lives
  /// here so the title bar tooltip and the integrations window cannot drift apart.
  /// </summary>
  /// <param name="Name">The name shown to the user, also the key a dismissal is stored under.</param>
  /// <param name="State">How the integration is currently behaving.</param>
  /// <param name="Status">The short status shown next to the name.</param>
  /// <param name="Detail">What the integration does, or what is lost while it is unavailable.</param>
  /// <param name="CanDismiss">Whether the user is allowed to hide this integration's warning.</param>
  /// <param name="Dismissed">Whether the user has hidden this integration's warning.</param>
  public readonly record struct Integration(
    string Name,
    IntegrationState State,
    string Status,
    string Detail,
    bool CanDismiss,
    bool Dismissed)
  {
    private static readonly Vector4 OkColor = new(0.3f, 0.85f, 0.5f, 1.0f);

    private static readonly Vector4 IdleColor = new(0.45f, 0.45f, 0.45f, 1.0f);

    private static readonly Vector4 DownColor = new(0.9f, 0.35f, 0.3f, 1.0f);

    /// <summary>
    /// Gets a value indicating whether the integration is not doing its job right now.
    /// </summary>
    public bool IsWarning => this.State != IntegrationState.Ok;

    /// <summary>
    /// Gets the colour the integration is listed in.
    /// </summary>
    public Vector4 Color => this.State switch
    {
      IntegrationState.Ok => OkColor,
      IntegrationState.Down => DownColor,
      _ => IdleColor,
    };
  }
}
