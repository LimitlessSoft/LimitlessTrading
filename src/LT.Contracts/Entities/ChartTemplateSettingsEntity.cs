using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>What a chart template sets; null: not part of the template.</summary>
public class ChartTemplateSettingsEntity
{
	public ChartTimeframe? Timeframe { get; set; }

	/// <summary>Empty bars after the latest one.</summary>
	public double? RightMargin { get; set; }

	public List<IndicatorInstanceEntity>? Indicators { get; set; }
}
