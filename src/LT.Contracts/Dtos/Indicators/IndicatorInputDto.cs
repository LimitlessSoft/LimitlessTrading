using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Indicators;

/// <summary>An input an indicator declares. Bool inputs are 0 / 1.</summary>
public class IndicatorInputDto
{
	public string Name { get; set; } = string.Empty;
	public IndicatorInputType Type { get; set; }
	public double DefaultValue { get; set; }
	public double? Min { get; set; }
	public double? Max { get; set; }
	public double? Step { get; set; }
}
