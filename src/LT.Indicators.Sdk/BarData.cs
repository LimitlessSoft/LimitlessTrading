namespace LT.Indicators.Sdk;

/// <summary>The bars an indicator computes over, oldest first. Built by the app.</summary>
public sealed class BarData
{
	public BarData(
		DateTimeOffset[] time,
		double[] open,
		double[] high,
		double[] low,
		double[] close,
		double[] volume,
		bool isIntraday,
		TimeZoneInfo? sessionTimeZone = null
	)
	{
		var count = time.Length;
		if (
			open.Length != count
			|| high.Length != count
			|| low.Length != count
			|| close.Length != count
			|| volume.Length != count
		)
			throw new ArgumentException("Every bar field needs one value per bar.");
		Time = time;
		Open = open;
		High = high;
		Low = low;
		Close = close;
		Volume = volume;
		IsIntraday = isIntraday;
		SessionTimeZone = sessionTimeZone ?? TimeZoneInfo.Utc;
	}

	public static BarData Empty { get; } = new([], [], [], [], [], [], true);

	public int Count => Time.Length;
	public DateTimeOffset[] Time { get; }
	public double[] Open { get; }
	public double[] High { get; }
	public double[] Low { get; }
	public double[] Close { get; }
	public double[] Volume { get; }
	public bool IsIntraday { get; }

	/// <summary>Zone whose calendar date starts a new session (<c>Indicator.NewSession</c>).</summary>
	public TimeZoneInfo SessionTimeZone { get; }

	internal double[] GetNewSession()
	{
		var result = new double[Count];
		DateTime? previous = null;
		for (var i = 0; i < Count; i++)
		{
			var date = IsIntraday
				? TimeZoneInfo.ConvertTime(Time[i], SessionTimeZone).Date
				: DateTime.MinValue;
			result[i] = previous != date ? 1 : 0;
			previous = date;
		}
		return result;
	}
}
