using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.ChartTemplates;

/// <summary>
/// The chart settings a template holds; null: the template doesn't set it and applying it leaves
/// the chart's own.
/// </summary>
public class ChartTemplateSettingsDto
{
	public ChartTimeframe? Timeframe { get; set; }

	/// <summary>Empty bars after the latest one.</summary>
	public double? RightMargin { get; set; }

	/// <summary>The indicators in legend order, with their inputs, visibility and colours.</summary>
	public List<IndicatorInstanceDto>? Indicators { get; set; }
}
