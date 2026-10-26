using Avalonia;

namespace LT.App.Widgets.Chart;

/// <summary>
/// A horizontal band of the chart with its own vertical scale (price, volume, one indicator).
/// Every pane shares the chart's horizontal viewport (bar spacing, pan, right margin).
/// </summary>
internal readonly record struct ChartPane(Rect Rect, double Min, double Max)
{
	public double Y(double value) => Rect.Bottom - (value - Min) / (Max - Min) * Rect.Height;

	public double Value(double y) => Min + (Rect.Bottom - y) / Rect.Height * (Max - Min);

	public bool Contains(double y) => y >= Rect.Top && y <= Rect.Bottom;
}
