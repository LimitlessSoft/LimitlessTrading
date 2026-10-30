using LT.Contracts.Constants;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>Chart-side math of indicators: pane heights, value ranges, legend values.</summary>
public static class IndicatorChartHelpers
{
	/// <summary>
	/// Height of each of <paramref name="paneCount"/> indicator panes in a chart
	/// <paramref name="height"/> tall: <see cref="ChartConstants.IndicatorPaneFraction"/> each
	/// while they fit in <see cref="ChartConstants.MaxIndicatorPanesFraction"/>, then shared.
	/// </summary>
	public static double GetPaneHeight(double height, int paneCount)
	{
		if (paneCount <= 0 || height <= 0)
			return 0;
		return Math.Min(
			height * ChartConstants.IndicatorPaneFraction,
			height * ChartConstants.MaxIndicatorPanesFraction / paneCount
		);
	}

	/// <summary>
	/// Lowest and highest value of the plots over bars <c>[start, end)</c>: lines, both sides of
	/// bands, histograms (with 0), levels. Markers and NaN don't count. Null when nothing does.
	/// </summary>
	public static (double Min, double Max)? GetRange(
		IEnumerable<IndicatorPlotDto> plots,
		int start,
		int end
	)
	{
		var min = double.PositiveInfinity;
		var max = double.NegativeInfinity;
		void Add(double value)
		{
			if (!double.IsFinite(value))
				return;
			min = Math.Min(min, value);
			max = Math.Max(max, value);
		}
		foreach (var plot in plots)
		{
			switch (plot.Kind)
			{
				case IndicatorPlotKind.Markers:
					continue;
				case IndicatorPlotKind.Level:
					Add(plot.Level);
					continue;
				case IndicatorPlotKind.Histogram:
					Add(0);
					break;
			}
			for (var i = Math.Max(0, start); i < Math.Min(end, plot.Values.Length); i++)
			{
				Add(plot.Values[i]);
				if (plot.Values2 is { } lower && i < lower.Length)
					Add(lower[i]);
			}
		}
		return min <= max ? (min, max) : null;
	}

	/// <summary>
	/// The scale of an indicator's own pane: its fixed range where it has one, else the visible
	/// values with <see cref="ChartConstants.IndicatorPanePadding"/> around them; (0, 1) when empty.
	/// </summary>
	public static (double Min, double Max) GetPaneScale(
		IndicatorResultDto result,
		int start,
		int end
	)
	{
		var range = GetRange(result.Plots, start, end);
		var min = result.ScaleMin ?? range?.Min ?? 0;
		var max = result.ScaleMax ?? range?.Max ?? 1;
		if (max <= min)
		{
			var half = Math.Max(Math.Abs(min) * 0.01, 0.5);
			return (min - half, max + half);
		}
		var padding = (max - min) * ChartConstants.IndicatorPanePadding;
		return (result.ScaleMin ?? min - padding, result.ScaleMax ?? max + padding);
	}

	/// <summary>The same result without plots, for the legend while new bars are computed.</summary>
	public static IndicatorResultDto WithoutPlots(IndicatorResultDto result) =>
		new()
		{
			InstanceId = result.InstanceId,
			DefinitionId = result.DefinitionId,
			Label = result.Label,
			Pane = result.Pane,
			IsVisible = result.IsVisible,
			ScaleMin = result.ScaleMin,
			ScaleMax = result.ScaleMax,
			Decimals = result.Decimals,
			Error = result.Error,
		};

	/// <summary>
	/// Applies the user's colours (by plot name) to <paramref name="plots"/>; a chosen colour
	/// replaces a histogram's per-bar colours too.
	/// </summary>
	public static void ApplyPlotColors(
		IEnumerable<IndicatorPlotDto> plots,
		IReadOnlyDictionary<string, uint>? colors
	)
	{
		if (colors is not { Count: > 0 })
			return;
		foreach (var plot in plots)
			if (colors.TryGetValue(plot.Name, out var color))
			{
				plot.Color = color;
				plot.BarColors = null;
			}
	}

	/// <summary>
	/// Colour of a plot's value at bar <paramref name="index"/> in the legend: the bar's own
	/// (histograms with per-bar colours) or the plot's, always opaque.
	/// </summary>
	public static uint GetLegendColor(IndicatorPlotDto plot, int index) =>
		(
			plot.BarColors is { } colors && index >= 0 && index < colors.Length
				? colors[index]
				: plot.Color
		) | 0xFF000000;

	/// <summary>Plots whose value shows in the legend (lines, histograms, bands).</summary>
	public static bool HasLegendValue(IndicatorPlotDto plot) =>
		plot.Kind
			is IndicatorPlotKind.Line
				or IndicatorPlotKind.Histogram
				or IndicatorPlotKind.Band;

	/// <summary>
	/// The plots whose value shows in the legend, each with the name to put before its value:
	/// the plot's name when there are two or more (so their values can be told apart), else null.
	/// </summary>
	public static IReadOnlyList<(IndicatorPlotDto Plot, string? Name)> GetLegendPlots(
		IEnumerable<IndicatorPlotDto> plots
	)
	{
		var shown = plots.Where(HasLegendValue).ToList();
		var named = shown.Count > 1;
		return shown
			.Select(x => (x, named && !string.IsNullOrWhiteSpace(x.Name) ? x.Name : null))
			.ToList();
	}
}
