namespace LT.Contracts.Dtos.Positions;

public class PositionPnlDto
{
	public decimal Position { get; set; }
	public double? DailyPnl { get; set; }
	public double? UnrealizedPnl { get; set; }
	public double? RealizedPnl { get; set; }
	public double? MarketValue { get; set; }
}
