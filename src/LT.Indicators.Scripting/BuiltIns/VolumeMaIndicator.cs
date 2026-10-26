using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

/// <summary>A moving average of volume, drawn over the volume bars.</summary>
public sealed class VolumeMaIndicator : Indicator
{
	public VolumeMaIndicator()
	{
		Name = "Volume moving average";
		ShortName = "Vol MA";
		Pane = PaneKind.Volume;
		Decimals = 0;
	}

	protected override void Compute() =>
		Plot("Volume MA", Ta.Sma(Volume, Input("Length", 20, 1, 1000)), Colors.Yellow, 1);
}
