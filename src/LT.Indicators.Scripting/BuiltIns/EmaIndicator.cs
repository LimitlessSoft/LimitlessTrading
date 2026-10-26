using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

public sealed class EmaIndicator : Indicator
{
	public EmaIndicator()
	{
		Name = "Exponential moving average (EMA)";
		ShortName = "EMA";
	}

	protected override void Compute() =>
		Plot("EMA", Ta.Ema(Close, Input("Length", 9, 1, 1000)), Colors.Aqua);
}
