namespace LT.Contracts.Constants;

public static class OrderConstants
{
	/// <summary>
	/// Orders use their own connection (IBKR.Net places orders at protocol version 178 only); its
	/// client id is the main one plus this.
	/// </summary>
	public const int OrderClientIdOffset = 100;

	/// <summary>Bounds of a cash amount order (e.g. $10 of BTC); a guard against typos.</summary>
	public const decimal CashAmountMin = 1;
	public const decimal CashAmountMax = 10_000;

	/// <summary>
	/// One-click orders from the option chain: the contracts per click until the user sets another
	/// quantity in the widget, and the most it accepts (a fat-finger guard).
	/// </summary>
	public const int QuickOrderQuantityDefault = 1;
	public const int QuickOrderQuantityMax = 100;

	/// <summary>
	/// Repeat clicks on the same option chain cell within this window are ignored, so a double
	/// click never sends two orders.
	/// </summary>
	public static readonly TimeSpan QuickOrderRepeatWindow = TimeSpan.FromMilliseconds(500);

	/// <summary>How long the option chain shows that a one-click order was sent.</summary>
	public static readonly TimeSpan QuickOrderStatusDuration = TimeSpan.FromSeconds(5);
}
