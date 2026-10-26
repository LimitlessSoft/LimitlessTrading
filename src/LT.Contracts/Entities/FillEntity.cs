namespace LT.Contracts.Entities;

/// <summary>A fill kept in the local trade journal (IB only returns the current day's).</summary>
public class FillEntity
{
	public string ExecId { get; set; } = string.Empty;
	public DateTimeOffset Time { get; set; }
	public string Account { get; set; } = string.Empty;
	public int ConId { get; set; }
	public string Symbol { get; set; } = string.Empty;
	public string LocalSymbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;
	public double? Multiplier { get; set; }
	public string Side { get; set; } = string.Empty;
	public decimal Shares { get; set; }
	public double Price { get; set; }
	public string Exchange { get; set; } = string.Empty;
	public int OrderId { get; set; }
	public double? Commission { get; set; }
	public string CommissionCurrency { get; set; } = string.Empty;
	public double? RealizedPnl { get; set; }

	/// <summary>IB's Flex tradeID when the fill came from (or was matched by) a Flex statement.</summary>
	public string TradeId { get; set; } = string.Empty;

	/// <summary>When the app first saw the fill.</summary>
	public DateTimeOffset RecordedAt { get; set; }
}
