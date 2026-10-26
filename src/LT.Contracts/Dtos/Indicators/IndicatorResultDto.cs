using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Indicators;

/// <summary>An indicator instance computed over a chart's bars, or why it couldn't be.</summary>
public class IndicatorResultDto
{
	public string InstanceId { get; set; } = string.Empty;
	public string DefinitionId { get; set; } = string.Empty;

	/// <summary>Legend text: short name and input values ("RSI 14").</summary>
	public string Label { get; set; } = string.Empty;

	public IndicatorPane Pane { get; set; }
	public bool IsVisible { get; set; } = true;
	public double? ScaleMin { get; set; }
	public double? ScaleMax { get; set; }
	public int? Decimals { get; set; }

	/// <summary>Bars it was computed over (values beyond them don't exist).</summary>
	public int BarCount { get; set; }

	public List<IndicatorPlotDto> Plots { get; set; } = [];

	/// <summary>Compile or runtime error; null when it computed.</summary>
	public string? Error { get; set; }
}
