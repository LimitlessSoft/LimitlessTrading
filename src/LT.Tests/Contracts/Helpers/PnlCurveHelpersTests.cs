using LT.Contracts.Dtos.Performance;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class PnlCurveHelpersTests
{
	private static readonly DateTimeOffset Start = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

	[Fact]
	public void Updates_within_the_interval_replace_the_last_point()
	{
		var samples = new List<PnlSampleEntity>();

		Assert.True(PnlCurveHelpers.AddSample(samples, "DU1", Start, 10, Interval));
		Assert.False(PnlCurveHelpers.AddSample(samples, "DU1", Start.AddSeconds(4), 12, Interval));
		Assert.True(PnlCurveHelpers.AddSample(samples, "DU1", Start.AddSeconds(10), 15, Interval));

		Assert.Equal([12d, 15d], samples.Select(x => x.DailyPnl));
		Assert.Equal(Start, samples[0].Time);
	}

	[Fact]
	public void Each_account_has_its_own_interval()
	{
		var samples = new List<PnlSampleEntity>();
		PnlCurveHelpers.AddSample(samples, "DU1", Start, 1, Interval);

		Assert.True(PnlCurveHelpers.AddSample(samples, "DU2", Start.AddSeconds(1), 2, Interval));
		Assert.Equal(2, samples.Count);
	}

	[Fact]
	public void Summary_has_high_low_and_the_largest_fall_from_a_peak()
	{
		var summary = PnlCurveHelpers.Summarize(Curve(0, 100, 40, 120, 90, -10, 30));

		Assert.Equal(30, summary.Last);
		Assert.Equal(120, summary.High);
		Assert.Equal(-10, summary.Low);
		Assert.Equal(130, summary.MaxDrawdown);
	}

	[Fact]
	public void A_rising_curve_has_no_drawdown() =>
		Assert.Equal(0, PnlCurveHelpers.Summarize(Curve(-50, -20, 10)).MaxDrawdown);

	[Fact]
	public void An_empty_curve_has_no_values() => Assert.Null(PnlCurveHelpers.Summarize([]).Last);

	private static List<PnlSampleDto> Curve(params double[] values) =>
		values
			.Select((x, i) => new PnlSampleDto { Time = Start.AddMinutes(i), Value = x })
			.ToList();
}
