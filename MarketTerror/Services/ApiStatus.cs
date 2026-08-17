// <copyright file="ApiStatus.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Services
{
  using System;
  using System.Threading;
  using System.Threading.Tasks;

  /// <summary>
  /// Polls Universalis and FFXIVMT so every window can tell whether they are answering.
  /// </summary>
  /// <remarks>There is one of these however many boards are open; the poll is not per window.</remarks>
  public sealed class ApiStatus : IDisposable
  {
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);

    private readonly MarketTerrorPlugin plugin;

    private readonly CancellationTokenSource cancellation = new();

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiStatus"/> class and starts polling.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    public ApiStatus(MarketTerrorPlugin plugin)
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.StartPolling(this.cancellation.Token);
    }

    /// <summary>
    /// Gets a value indicating whether the Universalis API answered the last check,
    /// or null while the first check is still running.
    /// </summary>
    public bool? IsUniversalisUp { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the FFXIVMT API answered the last check,
    /// or null while the first check is still running.
    /// </summary>
    public bool? IsFFXIVMTUp { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
      if (this.isDisposed)
      {
        return;
      }

      this.cancellation.Cancel();
      this.cancellation.Dispose();
      this.isDisposed = true;
    }

    private void StartPolling(CancellationToken cancellationToken)
    {
      Task.Run(
        async () =>
        {
          while (!cancellationToken.IsCancellationRequested)
          {
            this.IsUniversalisUp = await this.plugin.UniversalisClient.CheckStatus(cancellationToken).ConfigureAwait(false);
            this.IsFFXIVMTUp = await this.plugin.FFXIVMTClient.CheckStatus(cancellationToken).ConfigureAwait(false);
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
          }
        },
        cancellationToken);
    }
  }
}
