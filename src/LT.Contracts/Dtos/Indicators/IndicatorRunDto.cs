namespace LT.Contracts.Dtos.Indicators;

/// <summary>One indicator for the engine to compute: which and with what inputs (all resolved).</summary>
public class IndicatorRunDto
{
	public string DefinitionId { get; set; } = string.Empty;
	public Dictionary<string, double> Inputs { get; set; } = [];
}
