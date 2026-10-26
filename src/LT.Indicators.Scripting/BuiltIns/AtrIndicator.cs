using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

public sealed class AtrIndicator : Indicator
{
	public AtrIndicator()
	{
		Name = "Average true range (ATR)";
		ShortName = "ATR";
		Pane = PaneKind.Separate;
	}

	protected override void Compute() =>
		Plot("ATR", Ta.Atr(High, Low, Close, Input("Length", 14, 1, 1000)), Colors.Red);
}
