namespace LT.Contracts.Dtos.Account;

public class AccountSummaryDto
{
	public string Account { get; set; } = string.Empty;
	public string Currency { get; set; } = string.Empty;
	public decimal? NetLiquidation { get; set; }
	public decimal? TotalCashValue { get; set; }
	public decimal? BuyingPower { get; set; }
	public decimal? AvailableFunds { get; set; }
	public decimal? ExcessLiquidity { get; set; }
	public decimal? InitMarginReq { get; set; }
	public decimal? MaintMarginReq { get; set; }
	public decimal? GrossPositionValue { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
