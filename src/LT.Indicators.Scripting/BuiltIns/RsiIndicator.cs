using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

/// <summary>Wilder's RSI in its own 0 … 100 pane with the 70 / 30 levels.</summary>
public sealed class RsiIndicator : Indicator
{
	public RsiIndicator()
	{
		Name = "Relative strength index (RSI)";
		ShortName = "RSI";
		Pane = PaneKind.Separate;
		ScaleMin = 0;
		ScaleMax = 100;
		Decimals = 2;
	}

	protected override void Compute()
	{
		Plot("RSI", Ta.Rsi(Close, Input("Length", 14, 1, 1000)), Colors.Purple);
		Level("Overbought", 70);
		Level("Middle", 50, Colors.Gray.WithOpacity(0.5), LineStyle.Dotted);
		Level("Oversold", 30);
	}
}
