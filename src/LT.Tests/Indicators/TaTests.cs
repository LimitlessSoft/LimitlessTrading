using LT.Indicators.Sdk;

namespace LT.Tests.Indicators;

/// <summary>
/// Reference values: StockCharts' published RSI (Wilder, 14) and 10-day EMA / SMA examples,
/// recomputed independently; the rest by hand.
/// </summary>
public class TaTests
{
	// StockCharts "RSI" ChartSchool table
	private static readonly Series RsiCloses = new double[]
	{
		44.3389,
		44.0902,
		44.1497,
		43.6124,
		44.3278,
		44.8264,
		45.0955,
		45.4245,
		45.8433,
		46.0826,
		45.8931,
		46.0328,
		45.6140,
		46.2820,
		46.2820,
		46.0028,
		46.0328,
		46.4116,
		46.2222,
		45.6439,
		46.2122,
		46.2521,
		45.7137,
		46.4515,
		45.7835,
		45.3548,
		44.0288,
		44.1783,
		44.2181,
		44.5672,
		43.4205,
		42.6628,
		43.1314,
	};

	// StockCharts "Moving averages" ChartSchool table
	private static readonly Series EmaCloses = new double[]
	{
		22.27,
		22.19,
		22.08,
		22.17,
		22.18,
		22.13,
		22.23,
		22.43,
		22.24,
		22.29,
		22.15,
		22.39,
		22.38,
		22.61,
		23.36,
		24.05,
		23.75,
		23.83,
		23.95,
		23.63,
		23.82,
		23.87,
		23.65,
		23.19,
		23.10,
		23.33,
		22.68,
		23.10,
		22.40,
		22.17,
	};

	// Reference tables are rounded: equal within half a unit of their last digit
	private static void AssertValues(double[] expected, Series actual, int from, int digits)
	{
		for (var i = 0; i < from; i++)
			Assert.True(double.IsNaN(actual[i]), $"bar {i} should have no value");
		Assert.Equal(expected.Length, actual.Length - from);
		var tolerance = 0.5 * Math.Pow(10, -digits) + 1e-9;
		for (var i = 0; i < expected.Length; i++)
			Assert.True(
				Math.Abs(expected[i] - actual[from + i]) <= tolerance,
				$"bar {from + i}: expected {expected[i]}, got {actual[from + i]}"
			);
	}

	[Fact]
	public void Rsi_matches_wilders_reference_values()
	{
		AssertValues(
			[
				70.53,
				66.32,
				66.55,
				69.41,
				66.36,
				57.97,
				62.93,
				63.26,
				56.06,
				62.38,
				54.71,
				50.42,
				39.99,
				41.46,
				41.87,
				45.46,
				37.30,
				33.08,
				37.77,
			],
			Ta.Rsi(RsiCloses, 14),
			14,
			2
		);
	}

	[Fact]
	public void Rsi_is_100_without_losses_and_0_without_gains()
	{
		Assert.Equal(100, Ta.Rsi(new double[] { 1, 2, 3, 4 }, 2).Last);
		Assert.Equal(0, Ta.Rsi(new double[] { 4, 3, 2, 1 }, 2).Last);
	}

	[Fact]
	public void Ema_is_seeded_with_the_sma_of_its_first_values()
	{
		AssertValues(
			[
				22.22,
				22.21,
				22.24,
				22.27,
				22.33,
				22.52,
				22.80,
				22.97,
				23.13,
				23.28,
				23.34,
				23.43,
				23.51,
				23.53,
				23.47,
				23.40,
				23.39,
				23.26,
				23.23,
				23.08,
				22.92,
			],
			Ta.Ema(EmaCloses, 10),
			9,
			2
		);
	}

	[Fact]
	public void Sma_matches_the_reference_values()
	{
		AssertValues(
			[
				22.22,
				22.21,
				22.23,
				22.26,
				22.30,
				22.42,
				22.61,
				22.77,
				22.90,
				23.08,
				23.21,
				23.38,
				23.52,
				23.65,
				23.71,
				23.68,
				23.61,
				23.51,
				23.43,
				23.28,
				23.13,
			],
			Ta.Sma(EmaCloses, 10),
			9,
			2
		);
	}

	[Fact]
	public void Rma_uses_wilders_smoothing()
	{
		// Seed (1+2+3)/3 = 2, then 2 + (6 - 2) / 3
		AssertValues([2, 3.3333], Ta.Rma(new double[] { 1, 2, 3, 6 }, 3), 2, 4);
	}

	[Fact]
	public void Wma_weighs_the_newest_value_most()
	{
		// (1*1 + 2*2 + 3*3) / 6, (2*1 + 3*2 + 7*3) / 6
		AssertValues([2.3333, 4.8333], Ta.Wma(new double[] { 1, 2, 3, 7 }, 3), 2, 4);
	}

	[Fact]
	public void Stdev_is_the_population_deviation()
	{
		AssertValues([2], Ta.Stdev(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }, 8), 7, 9);
	}

	[Fact]
	public void Moving_windows_are_nan_while_a_value_in_them_is_nan()
	{
		var sma = Ta.Sma(new double[] { 1, double.NaN, 3, 4, 5 }, 2);

		Assert.True(double.IsNaN(sma[1]));
		Assert.True(double.IsNaN(sma[2]));
		Assert.Equal(3.5, sma[3]);
		Assert.Equal(4.5, sma[4]);
	}

	[Fact]
	public void Ema_starts_after_leading_nans_and_carries_over_a_gap()
	{
		var ema = Ta.Ema(new double[] { double.NaN, 2, 4, double.NaN, 6 }, 2);

		Assert.True(double.IsNaN(ema[1]));
		Assert.Equal(3, ema[2]);
		Assert.True(double.IsNaN(ema[3]));
		// alpha 2/3: 2/3 * 6 + 1/3 * 3
		Assert.Equal(5, ema[4], 9);
	}

	[Fact]
	public void Highest_and_lowest_look_back_over_the_window()
	{
		Series values = new double[] { 3, 1, 4, 1, 5, 9, 2 };

		AssertValues([4, 4, 5, 9, 9], Ta.Highest(values, 3), 2, 9);
		AssertValues([1, 1, 1, 1, 2], Ta.Lowest(values, 3), 2, 9);
	}

	[Fact]
	public void Change_and_shift_look_back()
	{
		Series values = new double[] { 1, 4, 9 };

		Assert.Equal([double.NaN, 1, 4], values.Shift(1));
		AssertValues([3, 5], Ta.Change(values), 1, 9);
		AssertValues([800], Ta.Roc(values, 2), 2, 9);
	}

	[Fact]
	public void Crosses_fire_on_the_bar_that_crosses_only()
	{
		Series fast = new double[] { 1, 2, 3, 2, 1 };
		Series slow = new double[] { 2, 2, 2, 2, 2 };

		Assert.Equal([double.NaN, 0, 1, 0, 0], Ta.CrossOver(fast, slow));
		Assert.Equal([double.NaN, 0, 0, 0, 1], Ta.CrossUnder(fast, slow));
		Assert.Equal([double.NaN, 0, 1, 0, 0], Ta.CrossOver(fast, 2));
	}

	[Fact]
	public void True_range_and_atr()
	{
		Series high = new double[] { 10, 12, 11 };
		Series low = new double[] { 9, 10, 8 };
		Series close = new double[] { 9.5, 11, 9 };

		// First bar high - low; then the gap to the previous close counts
		Assert.Equal([1, 2.5, 3], Ta.TrueRange(high, low, close));
		AssertValues([1.75, 2.375], Ta.Atr(high, low, close, 2), 1, 9);
	}

	[Fact]
	public void Vwap_restarts_on_each_session()
	{
		Series price = new double[] { 10, 20, 30, 40 };
		Series volume = new double[] { 1, 3, 1, 1 };
		Series reset = new double[] { 1, 0, 1, 0 };

		Assert.Equal([10, 17.5, 30, 35], Ta.Vwap(price, volume, reset));
	}

	[Fact]
	public void Vwap_has_no_value_before_any_volume()
	{
		var vwap = Ta.Vwap(new double[] { 10, 20 }, new double[] { 0, 2 }, new double[] { 1, 0 });

		Assert.True(double.IsNaN(vwap[0]));
		Assert.Equal(20, vwap[1]);
	}

	[Fact]
	public void Macd_is_fast_minus_slow_ema_with_its_signal_and_histogram()
	{
		Series values = Enumerable.Range(1, 40).Select(x => (double)(x * x % 17)).ToArray();

		var (macd, signal, histogram) = Ta.Macd(values, 3, 6, 4);

		var expected = Ta.Ema(values, 3) - Ta.Ema(values, 6);
		Assert.Equal(expected.ToArray(), macd.ToArray());
		Assert.Equal(Ta.Ema(expected, 4).ToArray(), signal.ToArray());
		Assert.Equal((macd - signal).Last, histogram.Last, 12);
		// The slow EMA starts at bar 5, the signal 3 bars later
		Assert.True(double.IsNaN(signal[7]));
		Assert.False(double.IsNaN(signal[8]));
	}

	[Fact]
	public void Bollinger_bands_are_sma_plus_minus_deviations()
	{
		Series values = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };

		var (middle, upper, lower) = Ta.BollingerBands(values, 8, 2);

		Assert.Equal((5d, 9d, 1d), (middle.Last, upper.Last, lower.Last));
	}

	[Fact]
	public void Series_arithmetic_and_conditions()
	{
		Series a = new double[] { 1, 2, double.NaN, 4 };
		Series b = new double[] { 2, 2, 2, 0 };

		Assert.Equal([3, 4, double.NaN, 4], a + b);
		Assert.Equal([0.5, 1, double.NaN, double.NaN], a / b);
		Assert.Equal([0, 0, double.NaN, 1], a > b);
		Assert.Equal([1, 1, double.NaN, 0], a <= b);
		Assert.Equal([0, 1, double.NaN, 0], (a >= b) & (a < 3));
		Assert.Equal([1, 0, double.NaN, 1], !(a >= b) | (a > 3));
		Assert.True((a > b).IsTrue(3));
		Assert.False((a > b).IsTrue(2));
		Assert.True(double.IsNaN(a[10]));
	}

	[Fact]
	public void Series_of_different_lengths_dont_combine()
	{
		Assert.Throws<ArgumentException>(() => new Series([1, 2]) + new Series([1]));
	}

	[Fact]
	public void A_length_below_one_is_rejected()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Ta.Sma(new double[] { 1 }, 0));
	}

	[Fact]
	public void Every_function_handles_empty_series()
	{
		var empty = Series.Empty;

		Assert.Equal(0, Ta.Sma(empty, 5).Length);
		Assert.Equal(0, Ta.Ema(empty, 5).Length);
		Assert.Equal(0, Ta.Rsi(empty, 14).Length);
		Assert.Equal(0, Ta.Atr(empty, empty, empty, 14).Length);
		Assert.Equal(0, Ta.Vwap(empty, empty, empty).Length);
		Assert.Equal(0, Ta.Macd(empty).Histogram.Length);
		Assert.Equal(0, Ta.Stdev(empty, 3).Length);
	}
}
