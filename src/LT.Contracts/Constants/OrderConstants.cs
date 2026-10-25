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
}
