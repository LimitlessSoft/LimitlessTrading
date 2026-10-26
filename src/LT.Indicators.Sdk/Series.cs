using System.Collections;

namespace LT.Indicators.Sdk;

/// <summary>
/// One value per bar, oldest first, aligned to the chart's bars: <c>series[i]</c> belongs to bar
/// <c>i</c>. <see cref="double.NaN"/> means "no value" (not enough bars yet, a gap); arithmetic
/// on NaN gives NaN. Read-only: every operation returns a new series.
/// <para>
/// Conditions (<c>Close &gt; Open</c>, <c>Ta.CrossOver</c>) are series too: 1 for true,
/// 0 for false, NaN when either side has no value. <c>&amp;</c>, <c>|</c> and <c>!</c> combine
/// them; <see cref="IsTrue"/> reads one.
/// </para>
/// </summary>
public sealed class Series : IReadOnlyList<double>
{
	private readonly double[] _values;

	/// <summary>A series of a copy of <paramref name="values"/>.</summary>
	public Series(IEnumerable<double> values)
	{
		ArgumentNullException.ThrowIfNull(values);
		_values = values.ToArray();
	}

	private Series(double[] values, bool _) => _values = values;

	/// <summary>Takes <paramref name="values"/> without copying; nothing may write to it afterwards.</summary>
	internal static Series Wrap(double[] values) => new(values, true);

	public static Series Empty { get; } = Wrap([]);

	/// <summary><paramref name="length"/> times <paramref name="value"/>.</summary>
	public static Series Constant(int length, double value)
	{
		var values = new double[Math.Max(0, length)];
		Array.Fill(values, value);
		return Wrap(values);
	}

	/// <summary>A series of <paramref name="length"/> values made by <paramref name="valueAt"/>(index).</summary>
	public static Series Create(int length, Func<int, double> valueAt)
	{
		ArgumentNullException.ThrowIfNull(valueAt);
		var values = new double[Math.Max(0, length)];
		for (var i = 0; i < values.Length; i++)
			values[i] = valueAt(i);
		return Wrap(values);
	}

	/// <summary>Number of values (the chart's bar count).</summary>
	public int Length => _values.Length;

	int IReadOnlyCollection<double>.Count => _values.Length;

	/// <summary>Bar <paramref name="index"/>'s value (0 is the oldest); NaN outside the series.</summary>
	public double this[int index] =>
		(uint)index < (uint)_values.Length ? _values[index] : double.NaN;

	/// <summary>The latest bar's value; NaN when empty.</summary>
	public double Last => _values.Length > 0 ? _values[^1] : double.NaN;

	/// <summary>
	/// The series <paramref name="bars"/> bars back: <c>Close.Shift(1)[i]</c> is the previous
	/// bar's close. The first <paramref name="bars"/> values are NaN. Negative values look ahead.
	/// </summary>
	public Series Shift(int bars)
	{
		var values = new double[_values.Length];
		for (var i = 0; i < values.Length; i++)
			values[i] = this[i - bars];
		return Wrap(values);
	}

	/// <summary>True when bar <paramref name="index"/> has a value other than 0 (a condition that holds).</summary>
	public bool IsTrue(int index)
	{
		var value = this[index];
		return !double.IsNaN(value) && value != 0;
	}

	/// <summary>Applies <paramref name="selector"/> to every value.</summary>
	public Series Select(Func<double, double> selector)
	{
		ArgumentNullException.ThrowIfNull(selector);
		var values = new double[_values.Length];
		for (var i = 0; i < values.Length; i++)
			values[i] = selector(_values[i]);
		return Wrap(values);
	}

	/// <summary>Combines this series with <paramref name="other"/> bar by bar.</summary>
	public Series Zip(Series other, Func<double, double, double> selector)
	{
		ArgumentNullException.ThrowIfNull(other);
		ArgumentNullException.ThrowIfNull(selector);
		CheckLength(this, other);
		var values = new double[_values.Length];
		for (var i = 0; i < values.Length; i++)
			values[i] = selector(_values[i], other._values[i]);
		return Wrap(values);
	}

	/// <summary>A copy of the values.</summary>
	public double[] ToArray() => (double[])_values.Clone();

	internal double[] Values => _values;

	public IEnumerator<double> GetEnumerator() => ((IEnumerable<double>)_values).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

	public override string ToString() => $"Series[{Length}] last {Last}";

	public static implicit operator Series(double[] values) => new(values);

	public static Series operator +(Series a, Series b) => Binary(a, b, static (x, y) => x + y);

	public static Series operator +(Series a, double b) => a.Select(x => x + b);

	public static Series operator +(double a, Series b) => b.Select(x => a + x);

	public static Series operator -(Series a, Series b) => Binary(a, b, static (x, y) => x - y);

	public static Series operator -(Series a, double b) => a.Select(x => x - b);

	public static Series operator -(double a, Series b) => b.Select(x => a - x);

	public static Series operator -(Series a) => a.Select(static x => -x);

	public static Series operator *(Series a, Series b) => Binary(a, b, static (x, y) => x * y);

	public static Series operator *(Series a, double b) => a.Select(x => x * b);

	public static Series operator *(double a, Series b) => b.Select(x => a * x);

	/// <summary>Division; dividing by 0 gives NaN (no value) rather than infinity.</summary>
	public static Series operator /(Series a, Series b) => Binary(a, b, Divide);

	public static Series operator /(Series a, double b) => a.Select(x => Divide(x, b));

	public static Series operator /(double a, Series b) => b.Select(x => Divide(a, x));

	public static Series operator %(Series a, Series b) => Binary(a, b, static (x, y) => x % y);

	public static Series operator %(Series a, double b) => a.Select(x => x % b);

	public static Series operator >(Series a, Series b) =>
		Binary(a, b, static (x, y) => Bool(x > y, x, y));

	public static Series operator >(Series a, double b) => a.Select(x => Bool(x > b, x, b));

	public static Series operator >(double a, Series b) => b.Select(x => Bool(a > x, a, x));

	public static Series operator <(Series a, Series b) =>
		Binary(a, b, static (x, y) => Bool(x < y, x, y));

	public static Series operator <(Series a, double b) => a.Select(x => Bool(x < b, x, b));

	public static Series operator <(double a, Series b) => b.Select(x => Bool(a < x, a, x));

	public static Series operator >=(Series a, Series b) =>
		Binary(a, b, static (x, y) => Bool(x >= y, x, y));

	public static Series operator >=(Series a, double b) => a.Select(x => Bool(x >= b, x, b));

	public static Series operator >=(double a, Series b) => b.Select(x => Bool(a >= x, a, x));

	public static Series operator <=(Series a, Series b) =>
		Binary(a, b, static (x, y) => Bool(x <= y, x, y));

	public static Series operator <=(Series a, double b) => a.Select(x => Bool(x <= b, x, b));

	public static Series operator <=(double a, Series b) => b.Select(x => Bool(a <= x, a, x));

	/// <summary>Logical and of two conditions.</summary>
	public static Series operator &(Series a, Series b) =>
		Binary(a, b, static (x, y) => Bool(x != 0 && y != 0, x, y));

	/// <summary>Logical or of two conditions.</summary>
	public static Series operator |(Series a, Series b) =>
		Binary(a, b, static (x, y) => Bool(x != 0 || y != 0, x, y));

	/// <summary>Logical not of a condition.</summary>
	public static Series operator !(Series a) => a.Select(static x => Bool(x == 0, x, 0));

	/// <summary>1 where this series equals <paramref name="b"/> (exactly), else 0.</summary>
	public Series EqualTo(Series b) => Binary(this, b, static (x, y) => Bool(x == y, x, y));

	public Series EqualTo(double b) => Select(x => Bool(x == b, x, b));

	internal static void CheckLength(Series a, Series b)
	{
		if (a.Length != b.Length)
			throw new ArgumentException(
				$"The series have different lengths ({a.Length} and {b.Length})."
			);
	}

	private static Series Binary(Series a, Series b, Func<double, double, double> op)
	{
		ArgumentNullException.ThrowIfNull(a);
		ArgumentNullException.ThrowIfNull(b);
		CheckLength(a, b);
		var values = new double[a.Length];
		for (var i = 0; i < values.Length; i++)
			values[i] = op(a._values[i], b._values[i]);
		return Wrap(values);
	}

	private static double Divide(double x, double y) => y == 0 ? double.NaN : x / y;

	private static double Bool(bool value, double x, double y) =>
		double.IsNaN(x) || double.IsNaN(y) ? double.NaN
		: value ? 1
		: 0;
}
