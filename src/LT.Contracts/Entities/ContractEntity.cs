namespace LT.Contracts.Entities;

public class ContractEntity
{
	public int ConId { get; set; }
	public string Symbol { get; set; } = string.Empty;
	public string LocalSymbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;
	public string Exchange { get; set; } = string.Empty;
	public string PrimaryExchange { get; set; } = string.Empty;
	public string Currency { get; set; } = string.Empty;
	public string LastTradeDateOrContractMonth { get; set; } = string.Empty;
	public double Strike { get; set; }
	public string Right { get; set; } = string.Empty;
	public string Multiplier { get; set; } = string.Empty;
	public string TradingClass { get; set; } = string.Empty;
}
