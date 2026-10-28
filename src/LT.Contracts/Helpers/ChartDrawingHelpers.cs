using System.Globalization;
using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Dtos.Charts;
using LT.Contracts.Entities;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Chart drawings: how many points each kind takes, where a time sits among the bars (and back),
/// the Fibonacci levels, the measure labels, and the screen geometry the chart draws and hit-tests
/// with. Screen points are (X, Y) in DIPs; <c>bounds</c> is the price chart's rectangle.
/// <para>
/// A bar position is in bar units like <see cref="ChartViewportHelpers"/>: bar <c>i</c>'s centre
/// is position <c>i</c>. Between two bars a time is interpolated, before the first and after the
/// last one it is extrapolated at the timeframe's bar length — so a drawing can reach into the
/// right margin, and keeps its place when older bars are put in front or new ones arrive.
/// </para>
/// </summary>
public static partial class ChartDrawingHelpers
{
	/// <summary>Points the kind takes; 0 for a freehand stroke (two or more).</summary>
	public static int GetPointCount(ChartDrawingKind kind) =>
		kind switch
		{
			ChartDrawingKind.HorizontalLine
			or ChartDrawingKind.HorizontalRay
			or ChartDrawingKind.VerticalLine
			or ChartDrawingKind.Text => 1,
			ChartDrawingKind.ParallelChannel
			or ChartDrawingKind.Triangle
			or ChartDrawingKind.FibExtension => 3,
			ChartDrawingKind.Brush => 0,
			_ => 2,
		};

	/// <summary>The kind takes this many points (a stroke 2 … <see cref="ChartDrawingConstants.MaxBrushPoints"/>).</summary>
	public static bool HasValidPointCount(ChartDrawingKind kind, int count)
	{
		var needed = GetPointCount(kind);
		return needed == 0
			? count is >= 2 and <= ChartDrawingConstants.MaxBrushPoints
			: count == needed;
	}

	/// <summary>Its points sit on bar centres (everything but a freehand stroke).</summary>
	public static bool SnapsToBars(ChartDrawingKind kind) => kind != ChartDrawingKind.Brush;

	/// <summary>Bar position of a time (fractional between bars, extrapolated outside them).</summary>
	public static double GetBarPosition(
		IReadOnlyList<BarDto> bars,
		DateTimeOffset time,
		TimeSpan barLength
	)
	{
		if (bars.Count == 0)
			return 0;
		var length = (double)Math.Max(1, barLength.Ticks);
		var first = bars[0].Time;
		var last = bars[^1].Time;
		if (time <= first)
			return (time - first).Ticks / length;
		if (time >= last)
			return bars.Count - 1 + (time - last).Ticks / length;
		int low = 0,
			high = bars.Count - 1;
		while (high - low > 1)
		{
			var middle = (low + high) / 2;
			if (bars[middle].Time <= time)
				low = middle;
			else
				high = middle;
		}
		var span = (bars[high].Time - bars[low].Time).Ticks;
		return low + (span > 0 ? (time - bars[low].Time).Ticks / (double)span : 0);
	}

	/// <summary>The time at a bar position (the inverse of <see cref="GetBarPosition"/>).</summary>
	public static DateTimeOffset GetTimeAt(
		IReadOnlyList<BarDto> bars,
		double position,
		TimeSpan barLength
	)
	{
		if (bars.Count == 0 || !double.IsFinite(position))
			return bars.Count > 0 ? bars[^1].Time : default;
		var length = Math.Max(1, barLength.Ticks);
		if (position <= 0)
			return bars[0].Time.AddTicks((long)Math.Round(position * length));
		if (position >= bars.Count - 1)
			return bars[^1].Time.AddTicks((long)Math.Round((position - (bars.Count - 1)) * length));
		var index = (int)Math.Floor(position);
		var span = (bars[index + 1].Time - bars[index].Time).Ticks;
		return bars[index].Time.AddTicks((long)Math.Round((position - index) * span));
	}

	/// <summary>A retracement level's price: 1 at the first point, 0 at the second.</summary>
	public static double GetFibRetracementPrice(double first, double second, double level) =>
		second + (first - second) * level;

	/// <summary>An extension level's price: the move first → second, from the third point.</summary>
	public static double GetFibExtensionPrice(
		double first,
		double second,
		double third,
		double level
	) => third + (second - first) * level;

	/// <summary>
	/// The price range tool's label: the change from the first point to the second and its share
	/// of the first price, e.g. "+1.25 (+0.84%)".
	/// </summary>
	public static string GetPriceRangeLabel(
		double from,
		double to,
		int decimals,
		IFormatProvider? culture = null
	)
	{
		culture ??= CultureInfo.CurrentCulture;
		var change = to - from;
		var sign = change > 0 ? "+" : "";
		var text = sign + change.ToString("N" + decimals, culture);
		if (from != 0)
			text += $" ({sign}{(change / Math.Abs(from) * 100).ToString("N2", culture)}%)";
		return text;
	}

	/// <summary>
	/// The date range tool's label: bars from the first point to the second (negative going
	/// back) and the time between them, e.g. "12 bars, 1h".
	/// </summary>
	public static string GetDateRangeLabel(
		double fromPosition,
		double toPosition,
		DateTimeOffset from,
		DateTimeOffset to
	)
	{
		var bars = (int)Math.Round(toPosition - fromPosition);
		var duration = to - from;
		var sign = duration < TimeSpan.Zero ? "-" : "";
		return $"{bars} {(Math.Abs(bars) == 1 ? "bar" : "bars")}, {sign}{FormatDuration(duration.Duration())}";
	}

	/// <summary>"3d 4h", "1h 30m", "45m", "20s".</summary>
	public static string FormatDuration(TimeSpan duration)
	{
		duration = duration.Duration();
		if (duration.TotalDays >= 1)
			return duration.Hours > 0
				? $"{(int)duration.TotalDays}d {duration.Hours}h"
				: $"{(int)duration.TotalDays}d";
		if (duration.TotalHours >= 1)
			return duration.Minutes > 0
				? $"{duration.Hours}h {duration.Minutes}m"
				: $"{duration.Hours}h";
		if (duration.TotalMinutes >= 1)
			return $"{duration.Minutes}m";
		return $"{duration.Seconds}s";
	}

	/// <summary>Distance from <paramref name="point"/> to the segment a–b.</summary>
	public static double DistanceToSegment(
		(double X, double Y) point,
		(double X, double Y) a,
		(double X, double Y) b
	)
	{
		var dx = b.X - a.X;
		var dy = b.Y - a.Y;
		var lengthSquared = dx * dx + dy * dy;
		var t =
			lengthSquared > 0
				? Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared, 0, 1)
				: 0;
		var x = a.X + t * dx - point.X;
		var y = a.Y + t * dy - point.Y;
		return Math.Sqrt(x * x + y * y);
	}

	/// <summary>
	/// The part of the line through a and b inside <paramref name="bounds"/>: from a to b, or on
	/// past a (<paramref name="extendBack"/>) and / or past b (<paramref name="extendForward"/>)
	/// to the bounds' edges. Null when it misses them; a and b the same point give that point.
	/// </summary>
	public static ((double X, double Y) Start, (double X, double Y) End)? ClipLine(
		(double X, double Y) a,
		(double X, double Y) b,
		bool extendBack,
		bool extendForward,
		(double Left, double Top, double Right, double Bottom) bounds
	)
	{
		var dx = b.X - a.X;
		var dy = b.Y - a.Y;
		if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
			return (a, a);
		var tMin = extendBack ? double.NegativeInfinity : 0;
		var tMax = extendForward ? double.PositiveInfinity : 1;
		(double P, double Q)[] edges =
		[
			(-dx, a.X - bounds.Left),
			(dx, bounds.Right - a.X),
			(-dy, a.Y - bounds.Top),
			(dy, bounds.Bottom - a.Y),
		];
		foreach (var (p, q) in edges)
		{
			if (Math.Abs(p) < 1e-12)
			{
				if (q < 0)
					return null;
				continue;
			}
			var t = q / p;
			if (p < 0)
				tMin = Math.Max(tMin, t);
			else
				tMax = Math.Min(tMax, t);
		}
		if (tMin > tMax || double.IsInfinity(tMin) || double.IsInfinity(tMax))
			return null;
		return ((a.X + tMin * dx, a.Y + tMin * dy), (a.X + tMax * dx, a.Y + tMax * dy));
	}

	/// <summary>
	/// The line a kind draws from its first two points (trend line, ray, extended line, arrow),
	/// within the bounds; null when it is off them.
	/// </summary>
	public static ((double X, double Y) Start, (double X, double Y) End)? GetLine(
		ChartDrawingKind kind,
		(double X, double Y) a,
		(double X, double Y) b,
		(double Left, double Top, double Right, double Bottom) bounds
	) =>
		kind switch
		{
			ChartDrawingKind.Ray => ClipLine(a, b, false, true, bounds),
			ChartDrawingKind.ExtendedLine => ClipLine(a, b, true, true, bounds),
			_ => (a, b),
		};

	/// <summary>The two back corners of an arrow head at <paramref name="to"/>, <paramref name="size"/> long.</summary>
	public static ((double X, double Y) Left, (double X, double Y) Right) GetArrowHead(
		(double X, double Y) from,
		(double X, double Y) to,
		double size
	)
	{
		var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
		const double spread = Math.PI / 7;
		return (
			(to.X - size * Math.Cos(angle - spread), to.Y - size * Math.Sin(angle - spread)),
			(to.X - size * Math.Cos(angle + spread), to.Y - size * Math.Sin(angle + spread))
		);
	}

	/// <summary>
	/// How far (vertically) the channel's parallel line sits from its trend line a–b: it goes
	/// through <paramref name="through"/>. A vertical a–b counts from a.
	/// </summary>
	public static double GetChannelOffset(
		(double X, double Y) a,
		(double X, double Y) b,
		(double X, double Y) through
	)
	{
		var dx = b.X - a.X;
		var lineY = Math.Abs(dx) < 1e-9 ? a.Y : a.Y + (b.Y - a.Y) * (through.X - a.X) / dx;
		return through.Y - lineY;
	}

	/// <summary>The x span a trend-based extension's levels are drawn over: from the third point on, as wide as the move.</summary>
	public static (double Left, double Right) GetFibExtensionSpan(
		(double X, double Y) first,
		(double X, double Y) second,
		(double X, double Y) third
	) => (third.X, third.X + Math.Max(Math.Abs(second.X - first.X), 40));

	public static bool IsInsideTriangle(
		(double X, double Y) point,
		(double X, double Y) a,
		(double X, double Y) b,
		(double X, double Y) c
	)
	{
		static double Cross(
			(double X, double Y) p,
			(double X, double Y) q,
			(double X, double Y) r
		) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
		var d1 = Cross(a, b, point);
		var d2 = Cross(b, c, point);
		var d3 = Cross(c, a, point);
		var negative = d1 < 0 || d2 < 0 || d3 < 0;
		var positive = d1 > 0 || d2 > 0 || d3 > 0;
		return !(negative && positive);
	}

	/// <summary>Inside the ellipse in the box between opposite corners a and b, grown by <paramref name="tolerance"/>.</summary>
	public static bool IsInsideEllipse(
		(double X, double Y) point,
		(double X, double Y) a,
		(double X, double Y) b,
		double tolerance
	)
	{
		var rx = Math.Abs(b.X - a.X) / 2 + tolerance;
		var ry = Math.Abs(b.Y - a.Y) / 2 + tolerance;
		var x = (point.X - (a.X + b.X) / 2) / rx;
		var y = (point.Y - (a.Y + b.Y) / 2) / ry;
		return x * x + y * y <= 1;
	}

	/// <summary>Inside the box between opposite corners a and b, grown by <paramref name="tolerance"/>.</summary>
	public static bool IsInsideBox(
		(double X, double Y) point,
		(double X, double Y) a,
		(double X, double Y) b,
		double tolerance
	) =>
		point.X >= Math.Min(a.X, b.X) - tolerance
		&& point.X <= Math.Max(a.X, b.X) + tolerance
		&& point.Y >= Math.Min(a.Y, b.Y) - tolerance
		&& point.Y <= Math.Max(a.Y, b.Y) + tolerance;

	/// <summary>
	/// The drawing of this kind with its anchors at <paramref name="points"/> (screen) is under
	/// <paramref name="point"/>: on one of its lines, or inside a filled shape. A text drawing is
	/// hit by its label's box, which only the chart knows: always false here. A Fibonacci
	/// drawing's lines are its <paramref name="fibLevels"/> (null: the kind's defaults).
	/// </summary>
	public static bool HitTest(
		ChartDrawingKind kind,
		IReadOnlyList<(double X, double Y)> points,
		(double X, double Y) point,
		(double Left, double Top, double Right, double Bottom) bounds,
		double tolerance,
		IReadOnlyList<double>? fibLevels = null
	)
	{
		if (points.Count == 0)
			return false;
		var a = points[0];
		bool Near((double X, double Y) from, (double X, double Y) to) =>
			DistanceToSegment(point, from, to) <= tolerance;
		bool InBoundsX() => point.X >= bounds.Left && point.X <= bounds.Right;
		switch (kind)
		{
			case ChartDrawingKind.HorizontalLine:
				return Math.Abs(point.Y - a.Y) <= tolerance && InBoundsX();
			case ChartDrawingKind.HorizontalRay:
				return Math.Abs(point.Y - a.Y) <= tolerance
					&& point.X >= a.X - tolerance
					&& point.X <= bounds.Right;
			case ChartDrawingKind.VerticalLine:
				return Math.Abs(point.X - a.X) <= tolerance
					&& point.Y >= bounds.Top
					&& point.Y <= bounds.Bottom;
			case ChartDrawingKind.Text:
				return false;
			case ChartDrawingKind.Brush:
				for (var i = 1; i < points.Count; i++)
					if (Near(points[i - 1], points[i]))
						return true;
				return false;
		}
		if (points.Count < 2)
			return false;
		var b = points[1];
		switch (kind)
		{
			case ChartDrawingKind.TrendLine:
			case ChartDrawingKind.Arrow:
			case ChartDrawingKind.Ray:
			case ChartDrawingKind.ExtendedLine:
				return GetLine(kind, a, b, bounds) is { } line && Near(line.Start, line.End);
			case ChartDrawingKind.Rectangle:
			case ChartDrawingKind.PriceRange:
			case ChartDrawingKind.DateRange:
				return IsInsideBox(point, a, b, tolerance);
			case ChartDrawingKind.Ellipse:
				return IsInsideEllipse(point, a, b, tolerance);
			case ChartDrawingKind.FibRetracement:
			{
				if (Near(a, b))
					return true;
				var left = Math.Min(a.X, b.X);
				var right = Math.Max(a.X, b.X);
				if (point.X < left - tolerance || point.X > right + tolerance)
					return false;
				// Anywhere between level 0 and 1, or on another level's line
				if (point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y))
					return true;
				return (fibLevels ?? ChartDrawingConstants.FibRetracementLevels).Any(level =>
					Math.Abs(point.Y - GetFibRetracementPrice(a.Y, b.Y, level)) <= tolerance
				);
			}
		}
		if (points.Count < 3)
			return Near(a, b);
		var c = points[2];
		switch (kind)
		{
			case ChartDrawingKind.ParallelChannel:
			{
				var offset = GetChannelOffset(a, b, c);
				var a2 = (a.X, a.Y + offset);
				var b2 = (b.X, b.Y + offset);
				return Near(a, b)
					|| Near(a2, b2)
					|| IsInsideTriangle(point, a, b, b2)
					|| IsInsideTriangle(point, a, b2, a2);
			}
			case ChartDrawingKind.Triangle:
				return IsInsideTriangle(point, a, b, c) || Near(a, b) || Near(b, c) || Near(c, a);
			case ChartDrawingKind.FibExtension:
			{
				if (Near(a, b) || Near(b, c))
					return true;
				var (left, right) = GetFibExtensionSpan(a, b, c);
				return point.X >= left - tolerance
					&& point.X <= right + tolerance
					&& (fibLevels ?? ChartDrawingConstants.FibExtensionLevels).Any(level =>
						Math.Abs(point.Y - GetFibExtensionPrice(a.Y, b.Y, c.Y, level)) <= tolerance
					);
			}
			default:
				return false;
		}
	}

	/// <summary>The anchor within <paramref name="tolerance"/> of the point (the last on top); -1 when none.</summary>
	public static int HitAnchor(
		IReadOnlyList<(double X, double Y)> anchors,
		(double X, double Y) point,
		double tolerance
	)
	{
		for (var i = anchors.Count - 1; i >= 0; i--)
		{
			var dx = anchors[i].X - point.X;
			var dy = anchors[i].Y - point.Y;
			if (dx * dx + dy * dy <= tolerance * tolerance)
				return i;
		}
		return -1;
	}

	/// <summary>
	/// The anchors a drawing shows to drag: its points, but a stroke only its ends (its body
	/// moves it whole).
	/// </summary>
	public static IReadOnlyList<int> GetEditableAnchors(ChartDrawingKind kind, int pointCount) =>
		kind == ChartDrawingKind.Brush
			? pointCount > 1
				? [0, pointCount - 1]
				: [.. Enumerable.Range(0, pointCount)]
			: [.. Enumerable.Range(0, pointCount)];

	/// <summary>
	/// A freehand stroke with points closer than <paramref name="minStep"/> to the last kept one
	/// dropped (the last point is always kept), at most
	/// <see cref="ChartDrawingConstants.MaxBrushPoints"/>.
	/// </summary>
	public static List<(double X, double Y)> SimplifyStroke(
		IReadOnlyList<(double X, double Y)> points,
		double minStep
	)
	{
		var kept = new List<(double X, double Y)>();
		for (var i = 0; i < points.Count; i++)
		{
			var point = points[i];
			if (kept.Count > 0 && i < points.Count - 1)
			{
				var last = kept[^1];
				var dx = point.X - last.X;
				var dy = point.Y - last.Y;
				if (dx * dx + dy * dy < minStep * minStep)
					continue;
			}
			kept.Add(point);
			if (kept.Count == ChartDrawingConstants.MaxBrushPoints)
				break;
		}
		return kept;
	}

	// ---- style -----------------------------------------------------------------------------

	/// <summary>
	/// The style of a drawing never restyled (and of every drawing saved before styles): the
	/// palette's blue, solid, 1.5 wide (Fibonacci levels 1); a note's text is light.
	/// </summary>
	public static ChartDrawingStyleDto GetBuiltInStyle(ChartDrawingKind kind) =>
		new()
		{
			Color =
				kind == ChartDrawingKind.Text
					? ChartDrawingConstants.DefaultTextColor
					: ChartDrawingConstants.DefaultColor,
			LineStyle = ChartDrawingLineStyle.Solid,
			LineWidth = IsFib(kind)
				? ChartDrawingConstants.MinLineWidth
				: ChartDrawingConstants.DefaultLineWidth,
		};

	/// <summary>The kind has lines to style (all but a note).</summary>
	public static bool HasLineStyle(ChartDrawingKind kind) => kind != ChartDrawingKind.Text;

	/// <summary>The kind takes a colour (the price range is green up and red down).</summary>
	public static bool HasColor(ChartDrawingKind kind) => kind != ChartDrawingKind.PriceRange;

	public static bool IsFib(ChartDrawingKind kind) =>
		kind is ChartDrawingKind.FibRetracement or ChartDrawingKind.FibExtension;

	/// <summary>"#RRGGBB" or "#AARRGGBB".</summary>
	public static bool IsValidColor(string? color) =>
		color != null && HexColorRegex().IsMatch(color.Trim());

	/// <summary>Trimmed, upper case, with its "#".</summary>
	public static string NormalizeColor(string color)
	{
		color = color.Trim().ToUpperInvariant();
		return color.StartsWith('#') ? color : "#" + color;
	}

	public static bool IsValidLineWidth(double width) =>
		double.IsFinite(width)
		&& width >= ChartDrawingConstants.MinLineWidth
		&& width <= ChartDrawingConstants.MaxLineWidth;

	/// <summary>
	/// The default levels of a Fibonacci kind (<see cref="ChartDrawingConstants.FibRetracementLevels"/>
	/// / <see cref="ChartDrawingConstants.FibExtensionLevels"/>), each in its colour; empty for the
	/// other kinds.
	/// </summary>
	public static List<ChartDrawingFibLevelDto> GetDefaultFibLevels(ChartDrawingKind kind)
	{
		var values = kind switch
		{
			ChartDrawingKind.FibRetracement => ChartDrawingConstants.FibRetracementLevels,
			ChartDrawingKind.FibExtension => ChartDrawingConstants.FibExtensionLevels,
			_ => [],
		};
		return
		[
			.. values.Select(
				(value, i) =>
					new ChartDrawingFibLevelDto
					{
						Value = value,
						IsVisible = true,
						Color = ChartDrawingConstants.FibColors[
							i % ChartDrawingConstants.FibColors.Count
						],
					}
			),
		];
	}

	/// <summary>Within <see cref="ChartDrawingConstants.MinFibLevel"/> … <see cref="ChartDrawingConstants.MaxFibLevel"/>.</summary>
	public static bool IsValidFibLevel(double value) =>
		double.IsFinite(value)
		&& value >= ChartDrawingConstants.MinFibLevel
		&& value <= ChartDrawingConstants.MaxFibLevel;

	/// <summary>No level twice (compared to 6 decimals, as typed values are).</summary>
	public static bool HasUniqueFibLevels(IEnumerable<ChartDrawingFibLevelDto> levels)
	{
		var seen = new HashSet<double>();
		return levels.All(x => seen.Add(Math.Round(x.Value, 6)));
	}

	/// <summary>A typed level: "0.618", "0,618" or "61.8%" (a percentage is divided by 100).</summary>
	public static bool TryParseFibLevel(string? text, out double value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(text))
			return false;
		text = text.Trim();
		var percent = text.EndsWith('%');
		if (percent)
			text = text[..^1].Trim();
		if (
			!double.TryParse(
				text.Replace(',', '.'),
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out value
			)
		)
			return false;
		if (percent)
			value /= 100;
		return double.IsFinite(value);
	}

	public static ChartDrawingStyleEntity ToEntity(ChartDrawingStyleDto style) =>
		new()
		{
			Color = NormalizeColor(style.Color),
			LineStyle = style.LineStyle,
			LineWidth = style.LineWidth,
		};

	public static ChartDrawingStyleDto ToDto(ChartDrawingStyleEntity style) =>
		new()
		{
			Color = style.Color,
			LineStyle = style.LineStyle,
			LineWidth = style.LineWidth,
		};

	public static List<ChartDrawingFibLevelEntity> ToEntities(
		IEnumerable<ChartDrawingFibLevelDto> levels
	) =>
		[
			.. levels.Select(x => new ChartDrawingFibLevelEntity
			{
				Value = x.Value,
				IsVisible = x.IsVisible,
				Color = x.Color is { } color ? NormalizeColor(color) : null,
			}),
		];

	public static List<ChartDrawingFibLevelDto> ToDtos(
		IEnumerable<ChartDrawingFibLevelEntity> levels
	) =>
		[
			.. levels.Select(x => new ChartDrawingFibLevelDto
			{
				Value = x.Value,
				IsVisible = x.IsVisible,
				Color = x.Color,
			}),
		];

	// ---- magnet and paste ------------------------------------------------------------------

	/// <summary>
	/// The magnet: the open, high, low or close of the bar at <paramref name="barPosition"/>
	/// (rounded to a bar) nearest to <paramref name="price"/>; the price itself off the bars.
	/// </summary>
	public static double GetMagnetPrice(
		IReadOnlyList<BarDto> bars,
		double barPosition,
		double price
	)
	{
		if (!double.IsFinite(barPosition))
			return price;
		var index = (int)Math.Floor(barPosition + 0.5);
		if (index < 0 || index >= bars.Count)
			return price;
		var bar = bars[index];
		var nearest = bar.Open;
		foreach (var value in (ReadOnlySpan<double>)[bar.High, bar.Low, bar.Close])
			if (Math.Abs(value - price) < Math.Abs(nearest - price))
				nearest = value;
		return nearest;
	}

	/// <summary>
	/// Points moved by <paramref name="barShift"/> bars (through gaps as the bars run, beyond them
	/// at the bar length) and by <paramref name="priceShift"/>: where a pasted copy goes.
	/// </summary>
	public static List<ChartDrawingPointDto> OffsetPoints(
		IReadOnlyList<ChartDrawingPointDto> points,
		IReadOnlyList<BarDto> bars,
		TimeSpan barLength,
		double barShift,
		double priceShift
	) =>
		[
			.. points.Select(x => new ChartDrawingPointDto
			{
				Time =
					bars.Count > 0
						? GetTimeAt(
							bars,
							GetBarPosition(bars, x.Time, barLength) + barShift,
							barLength
						)
						: x.Time + barLength * barShift,
				Price = x.Price + priceShift,
			}),
		];

	[GeneratedRegex("^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
	private static partial Regex HexColorRegex();
}
