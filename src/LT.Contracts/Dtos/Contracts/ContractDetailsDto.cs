namespace LT.Contracts.Dtos.Contracts;

public class ContractDetailsDto
{
	public ContractDto Contract { get; set; } = new();
	public string LongName { get; set; } = string.Empty;
	public double MinTick { get; set; }
	public string TimeZoneId { get; set; } = string.Empty;

	/// <summary>
	/// IB's sessions in <see cref="TimeZoneId"/>, e.g. "20261026:0400-20261026:2000;20261027:CLOSED"
	/// (TradingHoursHelpers parses them). Trading: every session, extended hours included.
	/// </summary>
	public string TradingHours { get; set; } = string.Empty;

	/// <summary>Regular (liquid) hours, same format as <see cref="TradingHours"/>.</summary>
	public string LiquidHours { get; set; } = string.Empty;
	public string Industry { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string ContractMonth { get; set; } = string.Empty;
}
