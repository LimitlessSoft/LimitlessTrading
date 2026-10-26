using LT.Contracts.Enums;

namespace LT.App.Widgets.Settings;

/// <summary>One entry of the Chart live updates picker.</summary>
public sealed record ChartLiveUpdateOption(ChartLiveUpdateMode Mode, string Label)
{
	public override string ToString() => Label;

	/// <summary>The modes offered, slowest first.</summary>
	public static IReadOnlyList<ChartLiveUpdateOption> All { get; } =
	[
		new(ChartLiveUpdateMode.RealtimeBars, "5-second bars"),
		new(ChartLiveUpdateMode.Quotes, "Every trade (quotes)"),
		new(ChartLiveUpdateMode.QuotesThrottled, "Every trade (quotes), throttled redraw"),
		new(ChartLiveUpdateMode.TickByTick, "Every trade (tick-by-tick)"),
		new(
			ChartLiveUpdateMode.TickByTickThrottled,
			"Every trade (tick-by-tick), throttled redraw"
		),
	];

	/// <summary>The saved mode's entry; an unknown one shows as the default.</summary>
	public static ChartLiveUpdateOption Find(ChartLiveUpdateMode mode) =>
		All.FirstOrDefault(x => x.Mode == mode) ?? All[0];
}
