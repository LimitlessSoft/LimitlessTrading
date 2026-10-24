namespace LT.Contracts.Constants;

public static class AccountConstants
{
	public const string NetLiquidation = "NetLiquidation";
	public const string TotalCashValue = "TotalCashValue";
	public const string BuyingPower = "BuyingPower";
	public const string AvailableFunds = "AvailableFunds";
	public const string ExcessLiquidity = "ExcessLiquidity";
	public const string InitMarginReq = "InitMarginReq";
	public const string MaintMarginReq = "MaintMarginReq";
	public const string GrossPositionValue = "GrossPositionValue";

	public static readonly IReadOnlyList<string> SummaryTags =
	[
		NetLiquidation,
		TotalCashValue,
		BuyingPower,
		AvailableFunds,
		ExcessLiquidity,
		InitMarginReq,
		MaintMarginReq,
		GrossPositionValue,
	];

	public static readonly TimeSpan OrdersRefreshInterval = TimeSpan.FromSeconds(10);
	public static readonly TimeSpan ExecutionsRefreshInterval = TimeSpan.FromSeconds(30);
}
