namespace LT.Contracts.Dtos.Contracts;

public class ContractDetailsDto
{
	public ContractDto Contract { get; set; } = new();
	public string LongName { get; set; } = string.Empty;
	public double MinTick { get; set; }
	public string TimeZoneId { get; set; } = string.Empty;
	public string Industry { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string ContractMonth { get; set; } = string.Empty;
}
