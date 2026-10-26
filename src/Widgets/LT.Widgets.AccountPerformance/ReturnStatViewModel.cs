using LT.App.Helpers;
using LT.Contracts.Dtos.AccountPerformance;

namespace LT.App.Widgets.AccountPerformance;

/// <summary>One row of the return stats: a period's P&amp;L in money and percent.</summary>
public class ReturnStatViewModel(string label, PeriodReturnDto period)
{
	public string Label { get; } = label;
	public string Money { get; } = FormatHelpers.SignedMoney(period.Pnl);
	public string Percent { get; } = FormatHelpers.SignedPercent(period.Percent);
	public bool IsUp { get; } = period.Pnl > 0;
	public bool IsDown { get; } = period.Pnl < 0;
}
