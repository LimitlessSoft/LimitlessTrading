using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

public sealed class MacdIndicator : Indicator
{
	public MacdIndicator()
	{
		Name = "MACD";
		ShortName = "MACD";
		Pane = PaneKind.Separate;
	}

	protected override void Compute()
	{
		var fast = Input("Fast", 12, 1, 1000);
		var slow = Input("Slow", 26, 1, 1000);
		var signal = Input("Signal", 9, 1, 1000);
		var (macd, signalLine, histogram) = Ta.Macd(Close, fast, slow, signal);
		Histogram(
			"Histogram",
			histogram,
			Colors.Teal.WithOpacity(0.7),
			Colors.Red.WithOpacity(0.7)
		);
		Plot("MACD", macd, Colors.Blue);
		Plot("Signal", signalLine, Colors.Orange);
		Level("Zero", 0, Colors.Gray.WithOpacity(0.5), LineStyle.Dotted);
	}
}
