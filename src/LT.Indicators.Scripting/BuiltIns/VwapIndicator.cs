using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

/// <summary>Session VWAP of the typical price, restarting every New York trading day.</summary>
public sealed class VwapIndicator : Indicator
{
	public VwapIndicator()
	{
		Name = "VWAP";
		ShortName = "VWAP";
	}

	protected override void Compute() =>
		Plot("VWAP", Ta.Vwap(Hlc3, Volume, NewSession), Colors.Pink, 2);
}
