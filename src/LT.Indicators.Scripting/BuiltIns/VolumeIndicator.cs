using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

/// <summary>
/// Each bar's volume as a column on the volume bars at the bottom of the price chart: green on a
/// bar that closed at or above its open, red on one below. New charts have it.
/// </summary>
public sealed class VolumeIndicator : Indicator
{
	private static readonly Color Up = Colors.Teal.WithOpacity(1 / 3d);
	private static readonly Color Down = Colors.Red.WithOpacity(1 / 3d);

	public VolumeIndicator()
	{
		Name = "Volume";
		ShortName = "Vol";
		Pane = PaneKind.Volume;
		Decimals = 0;
	}

	protected override void Compute()
	{
		var colors = new Color[BarCount];
		for (var i = 0; i < BarCount; i++)
			colors[i] = Close[i] >= Open[i] ? Up : Down;
		ColoredHistogram("Volume", Volume, colors, Colors.Teal);
	}
}
