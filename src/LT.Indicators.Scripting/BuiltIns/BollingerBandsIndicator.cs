using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

public sealed class BollingerBandsIndicator : Indicator
{
	public BollingerBandsIndicator()
	{
		Name = "Bollinger Bands";
		ShortName = "BB";
	}

	protected override void Compute()
	{
		var length = Input("Length", 20, 1, 1000);
		var multiplier = Input("Deviations", 2.0, 0.1, 10, 0.1);
		var (middle, upper, lower) = Ta.BollingerBands(Close, length, multiplier);
		Band("Bands", upper, lower, Colors.Blue);
		Plot("Basis", middle, Colors.Orange, 1);
	}
}
