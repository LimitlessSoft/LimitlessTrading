using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

public sealed class SmaIndicator : Indicator
{
	public SmaIndicator()
	{
		Name = "Moving average (SMA)";
		ShortName = "SMA";
	}

	protected override void Compute() =>
		Plot("SMA", Ta.Sma(Close, Input("Length", 20, 1, 1000)), Colors.Orange);
}
