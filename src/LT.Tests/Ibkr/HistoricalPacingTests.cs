using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Ibkr;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Ibkr;

public class HistoricalPacingTests
{
	// A chart period switch (e.g. to "This week") was queued for minutes behind the 60 / 10 min
	// window once a few charts had reloaded, showing the old period's bars under "Loading…"
	[Fact]
	public void Chart_bars_are_not_held_back_past_sixty_requests()
	{
		var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero));
		var pacing = new HistoricalPacing(time);
		var barSize = ChartTimeframeHelpers.GetBarSize(ChartTimeframe.FifteenMinutes);

		for (var i = 0; i < MarketDataConstants.MaxHistoricalRequests; i++)
			Assert.True(
				pacing.WaitAsync(barSize, TestContext.Current.CancellationToken).IsCompleted
			);

		Assert.True(pacing.WaitAsync(barSize, TestContext.Current.CancellationToken).IsCompleted);
	}

	[Fact]
	public void Small_bars_above_sixty_requests_wait_for_the_window()
	{
		var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero));
		var pacing = new HistoricalPacing(time);

		for (var i = 0; i < MarketDataConstants.MaxHistoricalRequests; i++)
			Assert.True(
				pacing.WaitAsync("30 secs", TestContext.Current.CancellationToken).IsCompleted
			);

		var next = pacing.WaitAsync("5 secs", TestContext.Current.CancellationToken);
		Assert.False(next.IsCompleted);
		time.Advance(MarketDataConstants.HistoricalWindow);
		Assert.True(next.IsCompleted);
	}

	[Fact]
	public void Every_chart_timeframe_is_outside_the_small_bar_pacing()
	{
		foreach (var timeframe in Enum.GetValues<ChartTimeframe>())
			Assert.False(
				HistoricalPacing.IsSmallBarSize(ChartTimeframeHelpers.GetBarSize(timeframe))
			);
		Assert.True(HistoricalPacing.IsSmallBarSize("1 secs"));
		Assert.True(HistoricalPacing.IsSmallBarSize("30 secs"));
	}
}
