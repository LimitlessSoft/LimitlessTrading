namespace LT.Contracts.Entities;

public class BarEntity
{
	public DateTimeOffset Time { get; set; }
	public double Open { get; set; }
	public double High { get; set; }
	public double Low { get; set; }
	public double Close { get; set; }
	public decimal Volume { get; set; }
}
