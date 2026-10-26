namespace LT.Contracts.Entities;

/// <summary>An indicator on a chart, saved with the chart's placement.</summary>
public class IndicatorInstanceEntity
{
	public string Id { get; set; } = string.Empty;
	public string DefinitionId { get; set; } = string.Empty;
	public Dictionary<string, double> Inputs { get; set; } = [];
	public bool IsVisible { get; set; } = true;
	public Dictionary<string, uint>? PlotColors { get; set; }
}
