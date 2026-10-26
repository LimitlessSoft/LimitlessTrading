namespace LT.Contracts.Dtos.Flex;

/// <summary>One execution from an Activity (Trades) or Trade Confirmation statement.</summary>
public class FlexTradeDto
{
	public string Account { get; set; } = string.Empty;

	/// <summary>IB's trade id (tradeID); stable across Activity and Trade Confirmation statements.</summary>
	public string TradeId { get; set; } = string.Empty;

	/// <summary>IB's execution id (ibExecID / execID), the same id the socket API reports.</summary>
	public string ExecId { get; set; } = string.Empty;
	public int ConId { get; set; }
	public string Symbol { get; set; } = string.Empty;
	public string UnderlyingSymbol { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string AssetCategory { get; set; } = string.Empty;
	public double? Multiplier { get; set; }

	/// <summary>"BUY" or "SELL".</summary>
	public string Side { get; set; } = string.Empty;

	/// <summary>Always positive; the side says the direction.</summary>
	public decimal Quantity { get; set; }
	public double Price { get; set; }

	/// <summary>What the fill cost, as a positive amount (IB reports it negative).</summary>
	public double? Commission { get; set; }
	public string CommissionCurrency { get; set; } = string.Empty;
	public string Currency { get; set; } = string.Empty;
	public string Exchange { get; set; } = string.Empty;
	public DateTimeOffset? Time { get; set; }
	public DateOnly? TradeDate { get; set; }
	public double? RealizedPnl { get; set; }
}
