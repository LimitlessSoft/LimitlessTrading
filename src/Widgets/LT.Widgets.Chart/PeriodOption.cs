using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

public record PeriodOption(ChartPeriod Value)
{
	public override string ToString() => ChartPeriodHelpers.GetDisplayName(Value);
}
