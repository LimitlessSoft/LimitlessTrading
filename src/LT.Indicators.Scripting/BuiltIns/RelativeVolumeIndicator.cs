using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

/// <summary>
/// Relative volume: each bar's volume against the average of the previous "Days" sessions at the
/// same New York time of day, in its own pane.
/// <para>
/// Intraday, bars are matched by their time of day in the session time zone (so a DST change
/// or a late first bar doesn't shift the match the way counting bars from the open would). The
/// average covers the previous "Days" sessions on the chart; a session without a bar at that
/// time (a half day, a bar without trades) is left out, and the average uses whatever of those
/// sessions have one — NaN only while none does.
/// </para>
/// <para>
/// Cumulative (intraday): today's volume since the session start against the average of the
/// same running total in those sessions, two lines. Otherwise, and always on daily or longer
/// bars (one bar per session, nothing to accumulate): the bar's volume as a histogram — green
/// above its average — and the average of the previous "Days" bars as a line.
/// </para>
/// </summary>
public sealed class RelativeVolumeIndicator : Indicator
{
	public RelativeVolumeIndicator()
	{
		Name = "Relative volume";
		ShortName = "RVOL";
		Pane = PaneKind.Separate;
		Decimals = 0;
	}

	protected override void Compute()
	{
		var days = Input("Days", 10, 1, 100);
		var cumulative = Input("Cumulative", true) && IsIntraday;
		var (value, average) = Calculate(
			Time,
			Volume.ToArray(),
			IsIntraday,
			SessionTimeZone,
			days,
			cumulative
		);

		if (cumulative)
		{
			Plot("Today", value, Colors.Teal);
			Plot("Average", average, Colors.Orange);
			return;
		}
		var above = new double[value.Length];
		var rest = new double[value.Length];
		for (var i = 0; i < value.Length; i++)
		{
			var isAbove = value[i] > average[i];
			above[i] = isAbove ? value[i] : double.NaN;
			rest[i] = isAbove ? double.NaN : value[i];
		}
		Histogram("Volume", rest, Colors.Gray.WithOpacity(0.6));
		Histogram("Above average", above, Colors.Green.WithOpacity(0.8));
		Plot("Average", average, Colors.Orange);
	}

	/// <summary>
	/// Per bar: the value compared (the bar's volume, or the session's running total when
	/// <paramref name="cumulative"/>) and the average of the same value over the previous
	/// <paramref name="days"/> sessions at the same time of day. Daily bars are one session
	/// each. A missing (NaN or negative) volume counts as no value. O(bars): every time of day
	/// keeps a running sum of its last sessions.
	/// </summary>
	public static (double[] Value, double[] Average) Calculate(
		IReadOnlyList<DateTimeOffset> time,
		double[] volume,
		bool isIntraday,
		TimeZoneInfo sessionTimeZone,
		int days,
		bool cumulative
	)
	{
		var count = volume.Length;
		var value = new double[count];
		var average = new double[count];
		var slots = new Dictionary<long, Slot>();
		// This session's values by time of day, added to the slots once it is over
		var pending = new Dictionary<long, double>();
		var session = -1;
		DateTime? date = null;
		double total = 0;

		for (var i = 0; i < count; i++)
		{
			var local = isIntraday ? TimeZoneInfo.ConvertTime(time[i], sessionTimeZone) : time[i];
			if (!isIntraday || local.Date != date)
			{
				foreach (var (key, v) in pending)
				{
					if (!slots.TryGetValue(key, out var s))
						slots[key] = s = new Slot();
					s.Add(session, v);
				}
				pending.Clear();
				session++;
				date = local.Date;
				total = 0;
			}

			var volumeAt = volume[i] >= 0 ? volume[i] : double.NaN;
			if (cumulative)
			{
				if (!double.IsNaN(volumeAt))
					total += volumeAt;
				value[i] = total;
			}
			else
				value[i] = volumeAt;

			var slotKey = isIntraday ? local.TimeOfDay.Ticks : 0;
			if (!double.IsNaN(value[i]))
				pending.TryAdd(slotKey, value[i]);
			average[i] = slots.TryGetValue(slotKey, out var slot)
				? slot.Average(session - days)
				: double.NaN;
		}
		return (value, average);
	}

	// One time of day's values in the sessions before the current one, oldest first
	private sealed class Slot
	{
		private readonly Queue<(int Session, double Value)> _values = new();
		private double _sum;

		public void Add(int session, double value)
		{
			_values.Enqueue((session, value));
			_sum += value;
		}

		// Average of the sessions from firstSession on; drops older ones for good (sessions only grow)
		public double Average(int firstSession)
		{
			while (_values.Count > 0 && _values.Peek().Session < firstSession)
				_sum -= _values.Dequeue().Value;
			return _values.Count > 0 ? _sum / _values.Count : double.NaN;
		}
	}
}
