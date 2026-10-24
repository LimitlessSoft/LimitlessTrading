using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Dtos.Positions;

public class PositionDto
{
	public string Account { get; set; } = string.Empty;
	public int ContractId { get; set; }
	public string Symbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;
	public string Currency { get; set; } = string.Empty;
	public string Exchange { get; set; } = string.Empty;
	public decimal Quantity { get; set; }
	public double AverageCost { get; set; }
	public ContractDto Contract { get; set; } = new();
}
