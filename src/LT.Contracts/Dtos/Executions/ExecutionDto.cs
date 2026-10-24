namespace LT.Contracts.Dtos.Executions;

public class ExecutionDto
{
	public string ExecId { get; set; } = string.Empty;
	public DateTimeOffset? Time { get; set; }
	public string Account { get; set; } = string.Empty;
	public string Symbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;
	public string Side { get; set; } = string.Empty;
	public decimal Shares { get; set; }
	public double Price { get; set; }
	public string Exchange { get; set; } = string.Empty;
	public int OrderId { get; set; }
	public double? Commission { get; set; }
	public string CommissionCurrency { get; set; } = string.Empty;
	public double? RealizedPnl { get; set; }
}
