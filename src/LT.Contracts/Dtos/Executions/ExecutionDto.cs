namespace LT.Contracts.Dtos.Executions;

public class ExecutionDto
{
	public string ExecId { get; set; } = string.Empty;
	public DateTimeOffset? Time { get; set; }
	public string Account { get; set; } = string.Empty;

	/// <summary>IB's contract id; tells apart option strikes / future months of one symbol.</summary>
	public int ConId { get; set; }
	public string Symbol { get; set; } = string.Empty;

	/// <summary>The listing's own symbol (option or future code); empty when IB sends none.</summary>
	public string LocalSymbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;

	/// <summary>Contract multiplier (100 for US equity options); null means 1.</summary>
	public double? Multiplier { get; set; }
	public string Side { get; set; } = string.Empty;
	public decimal Shares { get; set; }
	public double Price { get; set; }
	public string Exchange { get; set; } = string.Empty;
	public int OrderId { get; set; }
	public double? Commission { get; set; }
	public string CommissionCurrency { get; set; } = string.Empty;
	public double? RealizedPnl { get; set; }
}
