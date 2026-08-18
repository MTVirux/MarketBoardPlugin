// <copyright file="BuyResult.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.Models.ShoppingList
{
  /// <summary>
  /// How a buy attempt ended.
  /// </summary>
  public sealed class BuyResult
  {
    private BuyResult(bool success, double unitPrice, string reason, bool stale = false)
    {
      this.Success = success;
      this.UnitPrice = unitPrice;
      this.Reason = reason;
      this.Stale = stale;
    }

    /// <summary>Gets a value indicating whether the item was bought.</summary>
    public bool Success { get; }

    /// <summary>Gets the per-unit price that was paid, or 0 when nothing was bought.</summary>
    public double UnitPrice { get; }

    /// <summary>Gets the reason nothing was bought, or an empty string on success.</summary>
    public string Reason { get; }

    /// <summary>
    /// Gets a value indicating whether the attempt was given up on because the board was still
    /// holding the listings from before the last purchase, which is worth another go.
    /// </summary>
    public bool Stale { get; }

    /// <summary>
    /// Builds the result of a purchase that went through.
    /// </summary>
    /// <param name="unitPrice">The per-unit price that was paid.</param>
    /// <returns>The result.</returns>
    public static BuyResult Bought(double unitPrice) => new BuyResult(true, unitPrice, string.Empty);

    /// <summary>
    /// Builds the result of a purchase that did not happen.
    /// </summary>
    /// <param name="reason">Why nothing was bought, phrased to be printed in chat.</param>
    /// <returns>The result.</returns>
    public static BuyResult Failed(string reason) => new BuyResult(false, 0, reason);

    /// <summary>
    /// Builds the result of a purchase that never started because the listings on the board were the
    /// ones from before the last purchase.
    /// </summary>
    /// <returns>The result.</returns>
    public static BuyResult StaleListings() => new BuyResult(false, 0, "the board's listings were out of date", true);
  }
}
