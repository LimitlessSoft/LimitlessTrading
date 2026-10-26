using LT.Contracts.Dtos.Performance;
using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

public static class PnlCurveHelpers
{
	/// <summary>
	/// Adds <paramref name="value"/> to <paramref name="account"/>'s curve in
	/// <paramref name="samples"/>, at most one point per <paramref name="interval"/>: within the
	/// interval of the account's last point, that point takes the newer value (its time stays).
	/// Returns true when a new point was added.
	/// </summary>
	public static bool AddSample(
		List<PnlSampleEntity> samples,
		string account,
		DateTimeOffset time,
		double value,
		TimeSpan interval
	)
	{
		var last = samples.FindLast(x => x.Account == account);
		if (last != null && time - last.Time < interval)
		{
			last.DailyPnl = value;
			return false;
		}

		samples.Add(
			new PnlSampleEntity
			{
				Account = account,
				Time = time,
				DailyPnl = value,
			}
		);
		return true;
	}

	/// <summary>Last value, high, low and the largest fall from a running peak of a curve.</summary>
	public static PnlCurveSummaryDto Summarize(IReadOnlyList<PnlSampleDto> samples)
	{
		if (samples.Count == 0)
			return new PnlCurveSummaryDto();

		var peak = samples[0].Value;
		var high = peak;
		var low = peak;
		var drawdown = 0d;
		foreach (var sample in samples)
		{
			peak = Math.Max(peak, sample.Value);
			high = Math.Max(high, sample.Value);
			low = Math.Min(low, sample.Value);
			drawdown = Math.Max(drawdown, peak - sample.Value);
		}

		return new PnlCurveSummaryDto
		{
			Last = samples[^1].Value,
			High = high,
			Low = low,
			MaxDrawdown = drawdown,
		};
	}
}
