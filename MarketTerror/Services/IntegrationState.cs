// <copyright file="IntegrationState.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  /// <summary>
  /// How an optional plugin or service MarketTerror works with is currently behaving.
  /// </summary>
  public enum IntegrationState
  {
    /// <summary>The integration is installed or answering.</summary>
    Ok,

    /// <summary>The integration is absent, or has not been contacted yet.</summary>
    Idle,

    /// <summary>The integration is expected to answer but did not.</summary>
    Down,
  }
}
