namespace LT.Indicators.Sdk;

/// <summary>
/// An indicator: reads the bars (<see cref="Close"/>, …) and its inputs (<see cref="Input(string, int, int, int)"/>),
/// computes series with <see cref="Ta"/> and declares what to draw (<see cref="Plot"/>,
/// <see cref="Histogram"/>, <see cref="Band"/>, <see cref="Markers"/>, <see cref="Level"/>).
/// <para>
/// <see cref="Compute"/> runs once over all bars (series-based, not once per bar). It also runs
/// once on zero bars when the indicator is loaded: that is how its inputs and plots are learnt,
/// so it must not fail on empty series (every <see cref="Ta"/> function handles them).
/// </para>
/// A script file's top-level statements become the body of <see cref="Compute"/>.
/// </summary>
public abstract class Indicator
{
	[ThreadStatic]
	private static Indicator? _current;

	private BarData _bars = BarData.Empty;
	private IReadOnlyDictionary<string, double> _inputValues = new Dictionary<string, double>();
	private readonly List<InputInfo> _inputs = [];
	private readonly List<PlotOutput> _plots = [];
	private Series? _open,
		_high,
		_low,
		_close,
		_volume,
		_newSession;

	/// <summary>The indicator computing on this thread (what <see cref="Script"/> forwards to).</summary>
	internal static Indicator Current =>
		_current
		?? throw new InvalidOperationException(
			"Bars, inputs and plots are only available while an indicator computes."
		);

	/// <summary>Name in the indicator list; a script defaults to its file name.</summary>
	public string? Name { get; set; }

	/// <summary>Name in the chart legend, before the input values (defaults to <see cref="Name"/>).</summary>
	public string? ShortName { get; set; }

	/// <summary>Where it is drawn (default: on the price chart).</summary>
	public PaneKind Pane { get; set; } = PaneKind.Overlay;

	/// <summary>Fixed bottom of its own pane's scale (e.g. 0 for RSI); null: autoscale.</summary>
	public double? ScaleMin { get; set; }

	/// <summary>Fixed top of its own pane's scale (e.g. 100 for RSI); null: autoscale.</summary>
	public double? ScaleMax { get; set; }

	/// <summary>Decimals shown in the legend; null: as many as the price needs.</summary>
	public int? Decimals { get; set; }

	/// <summary>Number of bars (the length of every series).</summary>
	protected int BarCount => _bars.Count;

	protected Series Open => _open ??= Series.Wrap(_bars.Open);
	protected Series High => _high ??= Series.Wrap(_bars.High);
	protected Series Low => _low ??= Series.Wrap(_bars.Low);
	protected Series Close => _close ??= Series.Wrap(_bars.Close);
	protected Series Volume => _volume ??= Series.Wrap(_bars.Volume);

	/// <summary>(High + Low) / 2.</summary>
	protected Series Hl2 => (High + Low) / 2;

	/// <summary>(High + Low + Close) / 3, the "typical price".</summary>
	protected Series Hlc3 => (High + Low + Close) / 3;

	/// <summary>(Open + High + Low + Close) / 4.</summary>
	protected Series Ohlc4 => (Open + High + Low + Close) / 4;

	/// <summary>Start time of each bar (UTC). Daily and weekly bars carry their date at midnight UTC.</summary>
	protected IReadOnlyList<DateTimeOffset> Time => _bars.Time;

	/// <summary>True for minute and hour bars.</summary>
	protected bool IsIntraday => _bars.IsIntraday;

	/// <summary>
	/// Zone of the trading day (New York): its calendar date starts a new session
	/// (<see cref="NewSession"/>) and <c>TimeZoneInfo.ConvertTime(Time[i], SessionTimeZone)</c>
	/// gives a bar's exchange time of day.
	/// </summary>
	protected TimeZoneInfo SessionTimeZone => _bars.SessionTimeZone;

	/// <summary>
	/// 1 on the first bar of each trading day for intraday bars (a new date in the session time
	/// zone, New York), else 0; on daily and longer bars only the first bar is 1.
	/// </summary>
	protected Series NewSession => _newSession ??= Series.Wrap(_bars.GetNewSession());

	/// <summary>A whole-number input with its default and allowed range.</summary>
	protected int Input(
		string name,
		int defaultValue,
		int min = int.MinValue,
		int max = int.MaxValue
	) => GetInt(name, defaultValue, min, max);

	/// <summary>A decimal input with its default, allowed range and the step of its spin box.</summary>
	protected double Input(
		string name,
		double defaultValue,
		double min = double.MinValue,
		double max = double.MaxValue,
		double step = 0
	) => GetDouble(name, defaultValue, min, max, step);

	/// <summary>An on / off input.</summary>
	protected bool Input(string name, bool defaultValue) => GetBool(name, defaultValue);

	/// <summary>A line.</summary>
	protected void Plot(
		string name,
		Series series,
		Color? color = null,
		double width = 1.5,
		LineStyle style = LineStyle.Solid
	) => AddLine(name, series, color, width, style);

	/// <summary>Columns from 0; <paramref name="negativeColor"/> (if given) for values below 0.</summary>
	protected void Histogram(
		string name,
		Series series,
		Color? color = null,
		Color? negativeColor = null
	) => AddHistogram(name, series, color, negativeColor);

	/// <summary>Two lines with the space between them filled.</summary>
	protected void Band(
		string name,
		Series upper,
		Series lower,
		Color? color = null,
		double fillOpacity = 0.12
	) => AddBand(name, upper, lower, color, fillOpacity);

	/// <summary>
	/// A shape on every bar where <paramref name="condition"/> is true. <paramref name="at"/>
	/// places it with <see cref="MarkerPosition.AtValue"/>.
	/// </summary>
	protected void Markers(
		string name,
		Series condition,
		MarkerShape shape = MarkerShape.TriangleUp,
		Color? color = null,
		MarkerPosition position = MarkerPosition.BelowBar,
		Series? at = null
	) => AddMarkers(name, condition, shape, color, position, at);

	/// <summary>A horizontal line at <paramref name="value"/> (e.g. RSI 70 / 30).</summary>
	protected void Level(
		string name,
		double value,
		Color? color = null,
		LineStyle style = LineStyle.Dashed
	) => AddLevel(name, value, color, style);

	/// <summary>Computes the indicator over all bars.</summary>
	protected abstract void Compute();

	internal IndicatorOutput Run(BarData bars, IReadOnlyDictionary<string, double> inputs)
	{
		_bars = bars;
		_inputValues = inputs;
		_inputs.Clear();
		_plots.Clear();
		_open = _high = _low = _close = _volume = _newSession = null;
		var previous = _current;
		_current = this;
		try
		{
			Compute();
		}
		finally
		{
			_current = previous;
		}
		return new IndicatorOutput(
			Name ?? GetType().Name,
			ShortName,
			Pane,
			ScaleMin,
			ScaleMax,
			Decimals,
			[.. _inputs],
			[.. _plots]
		);
	}

	internal int GetInt(string name, int defaultValue, int min, int max)
	{
		if (min > max)
			throw new ArgumentException($"Input '{name}': min is above max.");
		var value = Math.Clamp(defaultValue, min, max);
		Declare(new InputInfo(name, InputKind.Int, value, Nullable(min), Nullable(max), 1));
		return _inputValues.TryGetValue(name, out var given) && !double.IsNaN(given)
			? (int)Math.Clamp(Math.Round(given), min, max)
			: value;
	}

	internal double GetDouble(string name, double defaultValue, double min, double max, double step)
	{
		if (min > max)
			throw new ArgumentException($"Input '{name}': min is above max.");
		var value = Math.Clamp(defaultValue, min, max);
		Declare(
			new InputInfo(
				name,
				InputKind.Double,
				value,
				min == double.MinValue ? null : min,
				max == double.MaxValue ? null : max,
				step > 0 ? step : null
			)
		);
		return _inputValues.TryGetValue(name, out var given) && double.IsFinite(given)
			? Math.Clamp(given, min, max)
			: value;
	}

	internal bool GetBool(string name, bool defaultValue)
	{
		Declare(new InputInfo(name, InputKind.Bool, defaultValue ? 1 : 0, 0, 1, 1));
		return _inputValues.TryGetValue(name, out var given) && !double.IsNaN(given)
			? given != 0
			: defaultValue;
	}

	internal void AddLine(
		string name,
		Series series,
		Color? color,
		double width,
		LineStyle style
	) =>
		AddPlot(
			new PlotOutput(name, PlotKind.Line, PickColor(color), Check(name, series))
			{
				Width = Math.Clamp(width, 0.5, 8),
				LineStyle = style,
			}
		);

	internal void AddHistogram(string name, Series series, Color? color, Color? negativeColor) =>
		AddPlot(
			new PlotOutput(name, PlotKind.Histogram, PickColor(color), Check(name, series))
			{
				Color2 = negativeColor,
			}
		);

	internal void AddBand(string name, Series upper, Series lower, Color? color, double fillOpacity)
	{
		var picked = PickColor(color);
		AddPlot(
			new PlotOutput(name, PlotKind.Band, picked, Check(name, upper))
			{
				Values2 = Check(name, lower),
				Color2 = picked.WithOpacity(fillOpacity),
			}
		);
	}

	internal void AddMarkers(
		string name,
		Series condition,
		MarkerShape shape,
		Color? color,
		MarkerPosition position,
		Series? at
	)
	{
		if (position == MarkerPosition.AtValue && at == null)
			throw new ArgumentException($"Markers '{name}': AtValue needs the 'at' series.");
		AddPlot(
			new PlotOutput(name, PlotKind.Markers, PickColor(color), Check(name, condition))
			{
				Values2 = at != null ? Check(name, at) : null,
				Shape = shape,
				Position = position,
			}
		);
	}

	internal void AddLevel(string name, double value, Color? color, LineStyle style) =>
		AddPlot(
			new PlotOutput(name, PlotKind.Level, color ?? Colors.Gray, [])
			{
				Level = value,
				LineStyle = style,
			}
		);

	internal Series GetOpen() => Open;

	internal Series GetHigh() => High;

	internal Series GetLow() => Low;

	internal Series GetClose() => Close;

	internal Series GetVolume() => Volume;

	internal Series GetNewSession() => NewSession;

	internal BarData Bars => _bars;

	private void Declare(InputInfo input)
	{
		if (string.IsNullOrWhiteSpace(input.Name))
			throw new ArgumentException("An input needs a name.");
		if (_inputs.All(x => x.Name != input.Name))
			_inputs.Add(input);
	}

	private void AddPlot(PlotOutput plot)
	{
		if (string.IsNullOrWhiteSpace(plot.Name))
			throw new ArgumentException("A plot needs a name.");
		if (_plots.Any(x => x.Name == plot.Name))
			throw new InvalidOperationException($"There are two plots named '{plot.Name}'.");
		_plots.Add(plot);
	}

	private Color PickColor(Color? color) =>
		color ?? Colors.Palette[_plots.Count(x => x.Kind != PlotKind.Level) % Colors.Palette.Count];

	private double[] Check(string name, Series series)
	{
		ArgumentNullException.ThrowIfNull(series, name);
		if (series.Length != _bars.Count)
			throw new ArgumentException(
				$"Plot '{name}' has {series.Length} values for {_bars.Count} bars."
			);
		return series.Values;
	}

	private static double? Nullable(int value) =>
		value is int.MinValue or int.MaxValue ? null : value;
}
