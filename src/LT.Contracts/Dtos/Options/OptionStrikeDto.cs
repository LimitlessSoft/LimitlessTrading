using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Dtos.Options;

public class OptionStrikeDto
{
	public string Expiration { get; set; } = string.Empty;
	public double Strike { get; set; }
	public ContractDto? Call { get; set; }
	public ContractDto? Put { get; set; }
}
