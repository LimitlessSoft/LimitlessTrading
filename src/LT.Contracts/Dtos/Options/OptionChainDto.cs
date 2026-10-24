using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Dtos.Options;

public class OptionChainDto
{
	public ContractDto Underlying { get; set; } = new();
	public double? UnderlyingPrice { get; set; }

	/// <summary>Expiration dates (yyyyMMdd) found for the requested month / day.</summary>
	public IReadOnlyList<string> Expirations { get; set; } = [];
	public IReadOnlyList<OptionStrikeDto> Strikes { get; set; } = [];
}
