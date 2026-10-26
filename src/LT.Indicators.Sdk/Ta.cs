namespace LT.Indicators.Sdk;

/// <summary>
/// Technical-analysis functions over whole series. Each returns a new series aligned to its
/// input; bars without enough history are NaN.
/// <para>
/// Moving windows (<see cref="Sma"/>, <see cref="Stdev"/>, <see cref="Wma"/>, <see cref="Sum"/>)
/// are NaN while any value in the window is NaN. The recursive averages (<see cref="Ema"/>,
/// <see cref="Rma"/>) start, like TradingView's, with the SMA of the first
/// <c>length</c> values and then carry on recursively; a NaN input later gives a NaN output and
/// leaves the average where it was.
/// </para>
/// </summary>
public static class Ta
{
	/// <summary>Simple moving average.</summary>
	public static Series Sma(Series source, int length)
	{
		var src = Values(source);
		CheckLength(length);
		var result = NaNs(src.Length);
		double sum = 0;
		var missing = 0;
		for (var i = 0; i < src.Length; i++)
		{
			Add(src[i], 1, ref sum, ref missing);
			if (i >= length)
				Add(src[i - length], -1, ref sum, ref missing);
			if (i >= length - 1 && missing == 0)
				result[i] = sum / length;
		}
		return Series.Wrap(result);
	}

	/// <summary>Sum of the last <paramref name="length"/> values.</summary>
	public static Series Sum(Series source, int length) => Sma(source, length) * length;

	/// <summary>
	/// Exponential moving average, alpha = 2 / (length + 1), seeded with the SMA of the first
	/// <paramref name="length"/> values (TradingView's and StockCharts' convention).
	/// </summary>
	public static Series Ema(Series source, int length)
	{
		CheckLength(length);
		return Recursive(source, length, 2.0 / (length + 1));
	}

	/// <summary>
	/// Wilder's moving average (RMA, SMMA), alpha = 1 / length, seeded with the SMA of the first
	/// <paramref name="length"/> values. Used by <see cref="Rsi"/> and <see cref="Atr"/>.
	/// </summary>
	public static Series Rma(Series source, int length)
	{
		CheckLength(length);
		return Recursive(source, length, 1.0 / length);
	}

	/// <summary>Linearly weighted moving average (the newest value weighs <paramref name="length"/>).</summary>
	public static Series Wma(Series source, int length)
	{
		var src = Values(source);
		CheckLength(length);
		var result = NaNs(src.Length);
		var weights = length * (length + 1) / 2.0;
		for (var i = length - 1; i < src.Length; i++)
		{
			double sum = 0;
			for (var k = 0; k < length; k++)
				sum += src[i - k] * (length - k);
			result[i] = sum / weights;
		}
		return Series.Wrap(result);
	}

	/// <summary>Population standard deviation over <paramref name="length"/> values (as TradingView's default).</summary>
	public static Series Stdev(Series source, int length)
	{
		var src = Values(source);
		var mean = Sma(source, length).Values;
		var result = NaNs(src.Length);
		for (var i = length - 1; i < src.Length; i++)
		{
			if (double.IsNaN(mean[i]))
				continue;
			double sum = 0;
			for (var k = i - length + 1; k <= i; k++)
				sum += (src[k] - mean[i]) * (src[k] - mean[i]);
			result[i] = Math.Sqrt(sum / length);
		}
		return Series.Wrap(result);
	}

	/// <summary>Highest value of the last <paramref name="length"/> bars (NaNs skipped).</summary>
	public static Series Highest(Series source, int length) => Extreme(source, length, +1);

	/// <summary>Lowest value of the last <paramref name="length"/> bars (NaNs skipped).</summary>
	public static Series Lowest(Series source, int length) => Extreme(source, length, -1);

	/// <summary>Difference from <paramref name="length"/> bars ago.</summary>
	public static Series Change(Series source, int length = 1)
	{
		CheckLength(length);
		return source - source.Shift(length);
	}

	/// <summary>Rate of change in percent from <paramref name="length"/> bars ago.</summary>
	public static Series Roc(Series source, int length)
	{
		CheckLength(length);
		var previous = source.Shift(length);
		return (source - previous) / previous * 100;
	}

	/// <summary>1 on the bar where <paramref name="a"/> moves above <paramref name="b"/>, else 0.</summary>
	public static Series CrossOver(Series a, Series b) =>
		Cross(a, b, static (x, y, px, py) => x > y && px <= py);

	public static Series CrossOver(Series a, double b) =>
		CrossOver(a, Series.Constant(a.Length, b));

	/// <summary>1 on the bar where <paramref name="a"/> moves below <paramref name="b"/>, else 0.</summary>
	public static Series CrossUnder(Series a, Series b) =>
		Cross(a, b, static (x, y, px, py) => x < y && px >= py);

	public static Series CrossUnder(Series a, double b) =>
		CrossUnder(a, Series.Constant(a.Length, b));

	/// <summary>True range; the first bar (no previous close) is high − low.</summary>
	public static Series TrueRange(Series high, Series low, Series close)
	{
		Series.CheckLength(high, low);
		Series.CheckLength(high, close);
		var h = high.Values;
		var l = low.Values;
		var c = close.Values;
		var result = new double[h.Length];
		for (var i = 0; i < h.Length; i++)
		{
			var range = h[i] - l[i];
			var previous = i > 0 ? c[i - 1] : double.NaN;
			result[i] = double.IsNaN(previous)
				? range
				: Math.Max(range, Math.Max(Math.Abs(h[i] - previous), Math.Abs(l[i] - previous)));
		}
		return Series.Wrap(result);
	}

	/// <summary>Average true range: Wilder's average (<see cref="Rma"/>) of <see cref="TrueRange"/>.</summary>
	public static Series Atr(Series high, Series low, Series close, int length) =>
		Rma(TrueRange(high, low, close), length);

	/// <summary>
	/// Wilder's RSI: <see cref="Rma"/> of gains over <see cref="Rma"/> of losses; 100 when there
	/// were no losses, 0 when there were no gains. The first value is at bar <paramref name="length"/>.
	/// </summary>
	public static Series Rsi(Series source, int length)
	{
		CheckLength(length);
		var change = Change(source);
		var up = Rma(change.Select(static x => double.IsNaN(x) ? x : Math.Max(x, 0)), length);
		var down = Rma(change.Select(static x => double.IsNaN(x) ? x : Math.Max(-x, 0)), length);
		return up.Zip(
			down,
			static (u, d) =>
				double.IsNaN(u) || double.IsNaN(d) ? double.NaN
				: d == 0 ? 100
				: u == 0 ? 0
				: 100 - 100 / (1 + u / d)
		);
	}

	/// <summary>
	/// Volume-weighted average of <paramref name="source"/> (usually <c>Hlc3</c>), restarting on
	/// every bar where <paramref name="reset"/> is true (<c>NewSession</c> for a daily VWAP).
	/// NaN until some volume traded.
	/// </summary>
	public static Series Vwap(Series source, Series volume, Series reset)
	{
		Series.CheckLength(source, volume);
		Series.CheckLength(source, reset);
		var src = source.Values;
		var vol = volume.Values;
		var result = NaNs(src.Length);
		double priceVolume = 0;
		double totalVolume = 0;
		for (var i = 0; i < src.Length; i++)
		{
			if (reset.IsTrue(i))
				priceVolume = totalVolume = 0;
			if (!double.IsNaN(src[i]) && !double.IsNaN(vol[i]))
			{
				priceVolume += src[i] * vol[i];
				totalVolume += vol[i];
			}
			if (totalVolume > 0)
				result[i] = priceVolume / totalVolume;
		}
		return Series.Wrap(result);
	}

	/// <summary>MACD line (fast EMA − slow EMA), its signal line (EMA of it) and their difference.</summary>
	public static (Series Macd, Series Signal, Series Histogram) Macd(
		Series source,
		int fastLength = 12,
		int slowLength = 26,
		int signalLength = 9
	)
	{
		var macd = Ema(source, fastLength) - Ema(source, slowLength);
		var signal = Ema(macd, signalLength);
		return (macd, signal, macd - signal);
	}

	/// <summary>Bollinger Bands: SMA ± <paramref name="multiplier"/> standard deviations.</summary>
	public static (Series Middle, Series Upper, Series Lower) BollingerBands(
		Series source,
		int length = 20,
		double multiplier = 2
	)
	{
		var middle = Sma(source, length);
		var deviation = Stdev(source, length) * multiplier;
		return (middle, middle + deviation, middle - deviation);
	}

	/// <summary>Stochastic %K: where the close sits in the high-low range of <paramref name="length"/> bars (0 … 100).</summary>
	public static Series Stoch(Series close, Series high, Series low, int length)
	{
		var lowest = Lowest(low, length);
		return (close - lowest) / (Highest(high, length) - lowest) * 100;
	}

	public static Series Max(Series a, Series b) => a.Zip(b, Math.Max);

	public static Series Max(Series a, double b) => a.Select(x => Math.Max(x, b));

	public static Series Min(Series a, Series b) => a.Zip(b, Math.Min);

	public static Series Min(Series a, double b) => a.Select(x => Math.Min(x, b));

	public static Series Abs(Series source) => source.Select(Math.Abs);

	/// <summary><paramref name="source"/> with NaN replaced by <paramref name="replacement"/>.</summary>
	public static Series Nz(Series source, double replacement = 0) =>
		source.Select(x => double.IsNaN(x) ? replacement : x);

	/// <summary><paramref name="whenTrue"/> where <paramref name="condition"/> holds, else <paramref name="whenFalse"/>.</summary>
	public static Series Iff(Series condition, Series whenTrue, Series whenFalse)
	{
		Series.CheckLength(condition, whenTrue);
		Series.CheckLength(condition, whenFalse);
		return Series.Create(
			condition.Length,
			i =>
				double.IsNaN(condition[i]) ? double.NaN
				: condition.IsTrue(i) ? whenTrue[i]
				: whenFalse[i]
		);
	}

	public static Series Iff(Series condition, double whenTrue, double whenFalse) =>
		Iff(
			condition,
			Series.Constant(condition.Length, whenTrue),
			Series.Constant(condition.Length, whenFalse)
		);

	private static Series Recursive(Series source, int length, double alpha)
	{
		var src = Values(source);
		var seed = Sma(source, length).Values;
		var result = NaNs(src.Length);
		var average = double.NaN;
		for (var i = 0; i < src.Length; i++)
		{
			if (double.IsNaN(average))
				average = seed[i];
			else if (!double.IsNaN(src[i]))
				average = alpha * src[i] + (1 - alpha) * average;
			else
				continue;
			result[i] = average;
		}
		return Series.Wrap(result);
	}

	private static Series Extreme(Series source, int length, int sign)
	{
		var src = Values(source);
		CheckLength(length);
		var result = NaNs(src.Length);
		for (var i = length - 1; i < src.Length; i++)
		{
			var best = double.NaN;
			for (var k = i - length + 1; k <= i; k++)
				if (
					!double.IsNaN(src[k])
					&& (double.IsNaN(best) || (sign > 0 ? src[k] > best : src[k] < best))
				)
					best = src[k];
			result[i] = best;
		}
		return Series.Wrap(result);
	}

	private static Series Cross(Series a, Series b, Func<double, double, double, double, bool> test)
	{
		Series.CheckLength(a, b);
		var result = NaNs(a.Length);
		for (var i = 1; i < a.Length; i++)
		{
			double x = a[i],
				y = b[i],
				px = a[i - 1],
				py = b[i - 1];
			if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsNaN(px) && !double.IsNaN(py))
				result[i] = test(x, y, px, py) ? 1 : 0;
		}
		return Series.Wrap(result);
	}

	private static void Add(double value, int sign, ref double sum, ref int missing)
	{
		if (double.IsNaN(value))
			missing += sign;
		else
			sum += sign * value;
	}

	private static double[] Values(Series source)
	{
		ArgumentNullException.ThrowIfNull(source);
		return source.Values;
	}

	private static double[] NaNs(int length)
	{
		var values = new double[length];
		Array.Fill(values, double.NaN);
		return values;
	}

	private static void CheckLength(int length)
	{
		if (length < 1)
			throw new ArgumentOutOfRangeException(
				nameof(length),
				length,
				"The length must be at least 1."
			);
	}
}
