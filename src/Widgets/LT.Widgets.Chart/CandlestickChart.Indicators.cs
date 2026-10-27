using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Indicator drawing: lines, histograms, bands, markers and levels in a <see cref="ChartPane"/>,
/// an indicator's own pane (separator, grid, axis), and the legend rows with their buttons
/// (visibility, settings, remove), which show while the row is hovered.
/// </summary>
public partial class CandlestickChart
{
	private const double LegendIconSize = 14;
	private const double LegendIconGap = 4;

	private static readonly ImmutableSolidColorBrush DimBrush = new(Color.Parse("#5C6370"));
	private static readonly ImmutableSolidColorBrush ErrorBrush = new(Color.Parse("#EF5350"));
	private static readonly ImmutableSolidColorBrush LegendHoverBrush = new(
		Color.Parse("#E01E222A")
	);
	private static readonly IPen PaneSeparatorPen = new ImmutablePen(
		new ImmutableSolidColorBrush(Color.Parse("#2E3440")),
		1
	);
	private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

	// Legend rows and their buttons as last drawn: what a click or hover hits
	private readonly List<(Rect Rect, string InstanceId)> _legendRows = [];
	private readonly List<(
		Rect Rect,
		string InstanceId,
		ChartIndicatorAction Action
	)> _legendButtons = [];
	private string? _hoveredLegend;

	/// <summary>The visible bar range and how a bar index maps to x.</summary>
	private readonly record struct Viewport(
		int Start,
		int End,
		double BarSpacing,
		Func<int, double> X
	);

	private void DrawIndicatorPane(
		DrawingContext context,
		ChartPane pane,
		IndicatorResultDto result,
		Viewport view,
		IReadOnlyList<BarDto> bars
	)
	{
		var rect = pane.Rect;
		var separatorY = Math.Round(rect.Top - PaneGap / 2) + 0.5;
		context.DrawLine(
			PaneSeparatorPen,
			new Point(rect.Left, separatorY),
			new Point(rect.Right + _priceAxisWidth, separatorY)
		);
		var step = NiceStep((pane.Max - pane.Min) / Math.Max(1, rect.Height / 32));
		var decimals = GetDecimals(result, pane);
		for (var value = Math.Ceiling(pane.Min / step) * step; value <= pane.Max; value += step)
		{
			var y = Math.Round(pane.Y(value)) + 0.5;
			context.DrawLine(GridPen, new Point(rect.Left, y), new Point(rect.Right, y));
			var label = Text(
				value.ToString("N" + decimals, CultureInfo.CurrentCulture),
				11,
				TextBrush
			);
			if (y - label.Height / 2 >= rect.Top && y + label.Height / 2 <= rect.Bottom)
				context.DrawText(label, new Point(AxisLabelX(rect), y - label.Height / 2));
		}
		DrawIndicatorPlots(context, [result], pane, view, bars, false);
	}

	private void DrawIndicatorPlots(
		DrawingContext context,
		IEnumerable<IndicatorResultDto> results,
		ChartPane pane,
		Viewport view,
		IReadOnlyList<BarDto> bars,
		bool onPrice
	)
	{
		using var clip = context.PushClip(pane.Rect);
		foreach (var result in results)
		{
			if (!result.IsVisible)
				continue;
			foreach (var plot in result.Plots)
				switch (plot.Kind)
				{
					case IndicatorPlotKind.Line:
						DrawLine(
							context,
							plot.Values,
							pane,
							view,
							Pen(plot.Color, plot.Width, plot.LineStyle)
						);
						break;
					case IndicatorPlotKind.Histogram:
						DrawHistogram(context, plot, pane, view);
						break;
					case IndicatorPlotKind.Band:
						DrawBand(context, plot, pane, view);
						break;
					case IndicatorPlotKind.Markers:
						DrawMarkers(context, plot, pane, view, bars, onPrice);
						break;
					case IndicatorPlotKind.Level:
						var y = Math.Round(pane.Y(plot.Level)) + 0.5;
						context.DrawLine(
							Pen(plot.Color, 1, plot.LineStyle),
							new Point(pane.Rect.Left, y),
							new Point(pane.Rect.Right, y)
						);
						break;
				}
		}
	}

	// A polyline over the visible bars (one bar beyond each side so it runs off the edge),
	// broken where there is no value
	private static void DrawLine(
		DrawingContext context,
		double[] values,
		ChartPane pane,
		Viewport view,
		IPen pen
	)
	{
		var (from, to) = GetDrawRange(values.Length, view);
		if (from > to)
			return;
		var geometry = new StreamGeometry();
		using (var g = geometry.Open())
		{
			var open = false;
			for (var i = from; i <= to; i++)
			{
				var value = values[i];
				if (!double.IsFinite(value))
				{
					if (open)
						g.EndFigure(false);
					open = false;
					continue;
				}
				var point = new Point(view.X(i), pane.Y(value));
				if (open)
					g.LineTo(point);
				else
				{
					g.BeginFigure(point, false);
					open = true;
				}
			}
			if (open)
				g.EndFigure(false);
		}
		context.DrawGeometry(null, pen, geometry);
	}

	private static void DrawHistogram(
		DrawingContext context,
		IndicatorPlotDto plot,
		ChartPane pane,
		Viewport view
	)
	{
		var up = Brush(plot.Color);
		var down = plot.Color2 is { } negative ? Brush(negative) : up;
		var width = Math.Max(1, view.BarSpacing * 0.7);
		var zero = pane.Y(Math.Clamp(0, pane.Min, pane.Max));
		for (var i = view.Start; i < Math.Min(view.End, plot.Values.Length); i++)
		{
			var value = plot.Values[i];
			if (!double.IsFinite(value))
				continue;
			var y = pane.Y(value);
			context.DrawRectangle(
				value >= 0 ? up : down,
				null,
				new Rect(
					view.X(i) - width / 2,
					Math.Min(y, zero),
					width,
					Math.Max(1, Math.Abs(zero - y))
				)
			);
		}
	}

	// The fill between both lines for every stretch where both have values, then the lines
	private static void DrawBand(
		DrawingContext context,
		IndicatorPlotDto plot,
		ChartPane pane,
		Viewport view
	)
	{
		var upper = plot.Values;
		var lower = plot.Values2 ?? [];
		var (from, to) = GetDrawRange(Math.Min(upper.Length, lower.Length), view);
		if (from > to)
			return;
		var fill = new StreamGeometry();
		using (var g = fill.Open())
		{
			var i = from;
			while (i <= to)
			{
				if (!double.IsFinite(upper[i]) || !double.IsFinite(lower[i]))
				{
					i++;
					continue;
				}
				var runStart = i;
				while (i <= to && double.IsFinite(upper[i]) && double.IsFinite(lower[i]))
					i++;
				g.BeginFigure(new Point(view.X(runStart), pane.Y(upper[runStart])), true);
				for (var k = runStart + 1; k < i; k++)
					g.LineTo(new Point(view.X(k), pane.Y(upper[k])));
				for (var k = i - 1; k >= runStart; k--)
					g.LineTo(new Point(view.X(k), pane.Y(lower[k])));
				g.EndFigure(true);
			}
		}
		context.DrawGeometry(
			Brush(plot.Color2 ?? (plot.Color & 0x00FFFFFF | 0x20000000)),
			null,
			fill
		);
		var pen = Pen(plot.Color, 1, plot.LineStyle);
		DrawLine(context, upper, pane, view, pen);
		DrawLine(context, lower, pane, view, pen);
	}

	private static void DrawMarkers(
		DrawingContext context,
		IndicatorPlotDto plot,
		ChartPane pane,
		Viewport view,
		IReadOnlyList<BarDto> bars,
		bool onPrice
	)
	{
		var brush = Brush(plot.Color);
		var size = Math.Clamp(view.BarSpacing * 0.8, 6, 12);
		var pen = new ImmutablePen(brush, 1.5);
		for (var i = view.Start; i < Math.Min(view.End, plot.Values.Length); i++)
		{
			var value = plot.Values[i];
			if (double.IsNaN(value) || value == 0)
				continue;
			double y;
			switch (plot.Position)
			{
				case IndicatorMarkerPosition.AtValue:
					var at =
						plot.Values2 is { } values && i < values.Length ? values[i] : double.NaN;
					if (!double.IsFinite(at))
						continue;
					y = pane.Y(at);
					break;
				case IndicatorMarkerPosition.AboveBar:
					y =
						onPrice && i < bars.Count
							? pane.Y(bars[i].High) - 4 - size / 2
							: pane.Rect.Top + size;
					break;
				default:
					y =
						onPrice && i < bars.Count
							? pane.Y(bars[i].Low) + 4 + size / 2
							: pane.Rect.Bottom - size;
					break;
			}
			DrawMarker(context, plot.Shape, new Point(view.X(i), y), size, brush, pen);
		}
	}

	private static void DrawMarker(
		DrawingContext context,
		IndicatorMarkerShape shape,
		Point center,
		double size,
		IBrush brush,
		IPen pen
	)
	{
		var half = size / 2;
		switch (shape)
		{
			case IndicatorMarkerShape.Circle:
				context.DrawEllipse(brush, null, center, half, half);
				return;
			case IndicatorMarkerShape.Square:
				context.DrawRectangle(
					brush,
					null,
					new Rect(center.X - half, center.Y - half, size, size)
				);
				return;
			case IndicatorMarkerShape.Cross:
				context.DrawLine(
					pen,
					new Point(center.X - half, center.Y - half),
					new Point(center.X + half, center.Y + half)
				);
				context.DrawLine(
					pen,
					new Point(center.X - half, center.Y + half),
					new Point(center.X + half, center.Y - half)
				);
				return;
		}
		var up = shape == IndicatorMarkerShape.TriangleUp;
		var geometry = new StreamGeometry();
		using (var g = geometry.Open())
		{
			g.BeginFigure(new Point(center.X, up ? center.Y - half : center.Y + half), true);
			g.LineTo(new Point(center.X - half, up ? center.Y + half : center.Y - half));
			g.LineTo(new Point(center.X + half, up ? center.Y + half : center.Y - half));
			g.EndFigure(true);
		}
		context.DrawGeometry(brush, null, geometry);
	}

	/// <summary>
	/// One row per indicator from <paramref name="origin"/> down: name and inputs, the value of
	/// each plot at the hovered bar in its colour, or the error. The hovered row gets a
	/// background and its buttons.
	/// </summary>
	private void DrawLegend(
		DrawingContext context,
		IReadOnlyList<IndicatorResultDto> results,
		Point origin,
		double maxWidth,
		int hovered,
		Func<IndicatorResultDto, int> decimals
	)
	{
		var y = origin.Y;
		foreach (var result in results)
		{
			var label = Text(result.Label, 11, result.IsVisible ? LabelBrush : DimBrush);
			var parts = new List<(FormattedText Text, double Gap)> { (label, 0) };
			if (result.Error != null)
			{
				var error = Text($"⚠ {result.Error}", 11, ErrorBrush);
				error.MaxTextWidth = Math.Max(40, maxWidth * 0.75 - label.Width);
				error.MaxLineCount = 1;
				error.Trimming = TextTrimming.CharacterEllipsis;
				parts.Add((error, 8));
			}
			else if (result.IsVisible)
				foreach (var plot in result.Plots.Where(IndicatorChartHelpers.HasLegendValue))
				{
					var brush = Brush(plot.Color | 0xFF000000);
					parts.Add(
						(Text(FormatValue(plot.Values, hovered, decimals(result)), 11, brush), 6)
					);
					// A band shows its lower line too
					if (plot.Values2 is { } lower)
						parts.Add(
							(Text(FormatValue(lower, hovered, decimals(result)), 11, brush), 6)
						);
				}

			var height = parts.Max(x => x.Text.Height);
			var width = parts.Sum(x => x.Text.Width + x.Gap);
			var isHovered = _hoveredLegend == result.InstanceId;
			var buttons = 3 * (LegendIconSize + LegendIconGap) + 6;
			var row = new Rect(origin.X - 3, y - 1, width + 6 + buttons, height + 2);
			_legendRows.Add((row, result.InstanceId));

			if (isHovered)
				context.DrawRectangle(LegendHoverBrush, null, row, 3, 3);
			var x = origin.X;
			foreach (var (text, gap) in parts)
			{
				x += gap;
				context.DrawText(text, new Point(x, y));
				x += text.Width;
			}
			if (isHovered)
			{
				x += 8;
				var iconTop = y + (height - LegendIconSize) / 2;
				foreach (
					var (icon, action) in new[]
					{
						(
							result.IsVisible ? ChartIcons.Eye : ChartIcons.EyeOff,
							ChartIndicatorAction.ToggleVisibility
						),
						(ChartIcons.Settings, ChartIndicatorAction.OpenSettings),
						(ChartIcons.Close, ChartIndicatorAction.Remove),
					}
				)
				{
					var rect = new Rect(x, iconTop, LegendIconSize, LegendIconSize);
					DrawIcon(context, icon, rect, _pointer is { } p && rect.Inflate(2).Contains(p));
					_legendButtons.Add((rect.Inflate(2), result.InstanceId, action));
					x += LegendIconSize + LegendIconGap;
				}
			}
			y += height + 3;
		}
	}

	private static void DrawIcon(DrawingContext context, Geometry icon, Rect rect, bool active)
	{
		var scale = rect.Width / 24;
		using (
			context.PushTransform(
				Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(rect.X, rect.Y)
			)
		)
			context.DrawGeometry(active ? LabelBrush : TextBrush, null, icon);
	}

	/// <summary>Legend hit under <paramref name="position"/>: a button, or the row itself (settings).</summary>
	private (string InstanceId, ChartIndicatorAction Action)? HitLegend(Point position)
	{
		foreach (var (rect, id, action) in _legendButtons)
			if (rect.Contains(position))
				return (id, action);
		foreach (var (rect, id) in _legendRows)
			if (rect.Contains(position))
				return (id, ChartIndicatorAction.OpenSettings);
		return null;
	}

	/// <summary>Follows the hovered legend row; true when the pointer is on one.</summary>
	private bool UpdateLegendHover(Point? position)
	{
		var row = position is { } p
			? _legendRows.FirstOrDefault(x => x.Rect.Contains(p)).InstanceId
			: null;
		_hoveredLegend = row;
		return row != null;
	}

	private void RaiseIndicatorAction(string instanceId, ChartIndicatorAction action) =>
		IndicatorAction?.Invoke(instanceId, action);

	private static int GetDecimals(IndicatorResultDto result, ChartPane pane)
	{
		if (result.Decimals is { } decimals)
			return Math.Clamp(decimals, 0, 8);
		var range = Math.Abs(pane.Max - pane.Min);
		return range >= 100 ? 0
			: range >= 1 ? 2
			: Math.Min(6, (int)Math.Ceiling(-Math.Log10(Math.Max(range, 1e-9))) + 2);
	}

	private static string FormatValue(double[] values, int index, int decimals)
	{
		var value = index < values.Length ? values[index] : double.NaN;
		return double.IsFinite(value)
			? value.ToString("N" + decimals, CultureInfo.CurrentCulture)
			: "–";
	}

	private static (int From, int To) GetDrawRange(int length, Viewport view) =>
		(Math.Max(0, view.Start - 1), Math.Min(length - 1, view.End));

	private static ImmutableSolidColorBrush Brush(uint argb) => new(Color.FromUInt32(argb));

	private static IPen Pen(uint argb, double width, IndicatorLineStyle style) =>
		new ImmutablePen(
			Brush(argb),
			width,
			style switch
			{
				IndicatorLineStyle.Dashed => new ImmutableDashStyle([4, 3], 0),
				IndicatorLineStyle.Dotted => new ImmutableDashStyle([1, 2], 0),
				_ => null,
			},
			PenLineCap.Round,
			PenLineJoin.Round
		);
}
