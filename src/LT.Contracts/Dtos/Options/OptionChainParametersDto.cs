namespace LT.Contracts.Dtos.Options;

/// <summary>reqSecDefOptParams: the expirations and strikes of one exchange and trading class.</summary>
public class OptionChainParametersDto
{
	public string Exchange { get; set; } = string.Empty;
	public string TradingClass { get; set; } = string.Empty;
	public string Multiplier { get; set; } = string.Empty;

	/// <summary>yyyyMMdd, ascending.</summary>
	public IReadOnlyList<string> Expirations { get; set; } = [];
	public IReadOnlyList<double> Strikes { get; set; } = [];
}
