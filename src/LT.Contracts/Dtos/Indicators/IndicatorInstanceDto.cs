namespace LT.Contracts.Dtos.Indicators;

/// <summary>An indicator on one chart, with its own input values; saved with the chart.</summary>
public class IndicatorInstanceDto
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string DefinitionId { get; set; } = string.Empty;

	/// <summary>Input values by name; inputs not in here use their default.</summary>
	public Dictionary<string, double> Inputs { get; set; } = [];

	public bool IsVisible { get; set; } = true;

	/// <summary>Plot colours chosen by the user (0xAARRGGBB by plot name); none: the indicator's own.</summary>
	public Dictionary<string, uint>? PlotColors { get; set; }
}
