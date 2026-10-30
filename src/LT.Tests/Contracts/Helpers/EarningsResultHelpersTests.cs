using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

public class EarningsResultHelpersTests
{
	private static readonly DateOnly Thursday = new(2026, 10, 29);
	private static readonly TimeZoneInfo NewYork = TradeJournalHelpers.GetTradeDayTimeZone();

	private static EarningsResultDto Figures(string symbol, decimal eps, string source) =>
		new()
		{
			Symbol = symbol,
			Date = Thursday,
			EpsActual = eps,
			Source = source,
		};

	private static EarningsResultDto Filing(string symbol, int hour, int minute) =>
		new()
		{
			Symbol = symbol,
			Date = Thursday,
			ReportedAt = new DateTimeOffset(2026, 10, 29, hour, minute, 0, TimeSpan.FromHours(-4)),
			Source = EarningsResultConstants.SecSourceName,
			FilingUrl = $"https://www.sec.gov/{symbol}-index.htm",
		};

	private static DateTimeOffset At(int hour, int minute) =>
		new(2026, 10, 29, hour, minute, 0, TimeSpan.FromHours(-4));

	[Fact]
	public void The_first_sources_figures_win_and_a_later_source_never_replaces_them()
	{
		var merged = EarningsResultHelpers.Merge([
			[Figures("LLY", 7.48m, "Yahoo Finance")],
			[Figures("lly", 7.22m, "Nasdaq")],
		]);

		var lilly = Assert.Single(merged);
		Assert.Equal(7.48m, lilly.EpsActual);
		Assert.Equal("Yahoo Finance", lilly.Source);
		Assert.Equal("LLY", lilly.Symbol);
	}

	[Fact]
	public void A_filing_gives_way_to_figures_but_lends_them_its_link_and_time()
	{
		var merged = EarningsResultHelpers.Merge([
			[Filing("AMZN", 16, 5)],
			[Figures("AMZN", 1.89m, "Yahoo Finance")],
		]);

		var amazon = Assert.Single(merged);
		Assert.Equal(1.89m, amazon.EpsActual);
		Assert.Equal("Yahoo Finance", amazon.Source);
		Assert.Equal("https://www.sec.gov/AMZN-index.htm", amazon.FilingUrl);
		Assert.Equal(At(16, 5), amazon.ReportedAt);
	}

	[Fact]
	public void A_later_result_keeps_the_figures_shown_and_adds_the_filing()
	{
		var kept = Figures("MA", 5.31m, "Yahoo Finance");

		var combined = EarningsResultHelpers.Combine(kept, Filing("MA", 8, 4));

		Assert.Equal(5.31m, combined.EpsActual);
		Assert.Equal("Yahoo Finance", combined.Source);
		Assert.NotNull(combined.FilingUrl);
		Assert.Null(kept.FilingUrl);
	}

	[Fact]
	public void Share_classes_written_differently_match()
	{
		Assert.Equal(
			EarningsResultHelpers.GetSymbolKey("BRK/B"),
			EarningsResultHelpers.GetSymbolKey("brk-b")
		);
		Assert.Equal("BRK-B", EarningsResultHelpers.GetSymbolKey("BRK.B"));
	}

	[Fact]
	public void The_surprise_is_the_sources_else_worked_out_from_the_estimate()
	{
		var given = new EarningsResultDto { EpsActual = 7.48m, EpsSurprisePercent = 17.88m };
		var none = new EarningsResultDto { EpsActual = 1.89m };
		var loss = new EarningsResultDto { EpsActual = -0.5m };

		Assert.Equal(17.88m, EarningsResultHelpers.GetSurprisePercent(6.35m, given));
		Assert.Equal(-3.57m, EarningsResultHelpers.GetSurprisePercent(1.96m, none));
		// A smaller loss than expected is a beat
		Assert.Equal(50m, EarningsResultHelpers.GetSurprisePercent(-1m, loss));
		Assert.Null(EarningsResultHelpers.GetSurprisePercent(null, none));
		Assert.Null(EarningsResultHelpers.GetSurprisePercent(1.96m, new EarningsResultDto()));
	}

	[Fact]
	public void Only_earnings_rows_without_figures_want_a_result()
	{
		var row = new CalendarEventDto
		{
			Date = Thursday,
			Kind = CalendarEventKind.Earnings,
			Symbol = "AMZN",
		};

		Assert.True(EarningsResultHelpers.NeedsResult(row, null));
		Assert.True(EarningsResultHelpers.NeedsResult(row, Filing("AMZN", 16, 5)));
		Assert.False(EarningsResultHelpers.NeedsResult(row, Figures("AMZN", 1.89m, "Yahoo")));
		Assert.False(
			EarningsResultHelpers.NeedsResult(
				new CalendarEventDto
				{
					Kind = CalendarEventKind.Earnings,
					Symbol = "LLY",
					EpsActual = 7.48m,
				},
				null
			)
		);
		Assert.False(
			EarningsResultHelpers.NeedsResult(
				new CalendarEventDto { Kind = CalendarEventKind.Dividend, Symbol = "KO" },
				null
			)
		);
	}

	[Fact]
	public void Today_and_the_days_just_before_it_in_the_range_are_looked_up()
	{
		var days = EarningsResultHelpers.GetLookupDays(
			new DateOnly(2026, 10, 27),
			new DateOnly(2026, 11, 1),
			Thursday
		);

		Assert.Equal([new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 28), Thursday], days);
		Assert.Empty(
			EarningsResultHelpers.GetLookupDays(
				new DateOnly(2026, 11, 2),
				new DateOnly(2026, 11, 8),
				Thursday
			)
		);
	}

	[Fact]
	public void Polling_runs_between_six_and_ten_in_new_york_on_the_sources_interval()
	{
		var interval = TimeSpan.FromMinutes(5);

		Assert.Equal(interval, EarningsResultHelpers.GetPollDelay(At(16, 10), interval, NewYork));
		Assert.Equal(
			TimeSpan.FromHours(1),
			EarningsResultHelpers.GetPollDelay(At(5, 0), interval, NewYork)
		);
		Assert.Equal(
			TimeSpan.FromMinutes(2),
			EarningsResultHelpers.GetPollDelay(At(21, 58), interval, NewYork)
		);
		Assert.Null(EarningsResultHelpers.GetPollDelay(At(22, 0), interval, NewYork));
		Assert.Equal(
			EarningsResultConstants.MinPollInterval,
			EarningsResultHelpers.GetPollDelay(At(16, 10), TimeSpan.FromSeconds(1), NewYork)
		);
	}

	[Fact]
	public void Edgar_is_read_more_often_before_the_open_and_just_after_the_close()
	{
		Assert.Equal(
			EarningsResultConstants.SecFastInterval,
			EarningsResultHelpers.GetSecInterval(At(7, 0), NewYork)
		);
		Assert.Equal(
			EarningsResultConstants.SecFastInterval,
			EarningsResultHelpers.GetSecInterval(At(16, 5), NewYork)
		);
		Assert.Equal(
			EarningsResultConstants.SecInterval,
			EarningsResultHelpers.GetSecInterval(At(12, 0), NewYork)
		);
		Assert.Equal(
			EarningsResultConstants.SecInterval,
			EarningsResultHelpers.GetSecInterval(At(16, 30), NewYork)
		);
	}

	[Fact]
	public void The_tooltip_says_which_source_gave_the_figure_and_when_the_8k_came()
	{
		var merged = EarningsResultHelpers.Combine(
			Filing("AMZN", 16, 5),
			Figures("AMZN", 1.89m, "Yahoo Finance")
		);

		Assert.Equal(
			"EPS from Yahoo Finance · 8-K filed 16:05 (SEC EDGAR)",
			EarningsResultHelpers.Describe(
				merged,
				x => x.ToOffset(TimeSpan.FromHours(-4)).ToString("HH:mm")
			)
		);
		Assert.Equal(string.Empty, EarningsResultHelpers.Describe(null, x => ""));
	}

	[Fact]
	public void An_eps_coming_in_or_changing_is_an_update_and_one_going_away_is_not()
	{
		Assert.True(EarningsResultHelpers.IsActualUpdate(null, 1.85m));
		Assert.True(EarningsResultHelpers.IsActualUpdate(1.85m, 1.86m));
		Assert.False(EarningsResultHelpers.IsActualUpdate(1.85m, 1.85m));
		Assert.False(EarningsResultHelpers.IsActualUpdate(1.85m, null));
		Assert.False(EarningsResultHelpers.IsActualUpdate(null, null));
	}

	[Fact]
	public void A_highlight_lasts_the_highlight_duration()
	{
		var at = At(16, 5);
		var duration = EarningsResultConstants.ActualHighlightDuration;

		Assert.False(EarningsResultHelpers.IsActualFresh(null, at));
		Assert.True(EarningsResultHelpers.IsActualFresh(at, at));
		Assert.True(
			EarningsResultHelpers.IsActualFresh(at, at + duration - TimeSpan.FromSeconds(1))
		);
		Assert.False(EarningsResultHelpers.IsActualFresh(at, at + duration));
	}

	[Fact]
	public void The_next_highlight_end_is_the_earliest_still_on()
	{
		var duration = EarningsResultConstants.ActualHighlightDuration;
		var now = At(16, 10);

		Assert.Equal(
			At(16, 7) + duration,
			EarningsResultHelpers.GetNextHighlightEnd([At(16, 0), At(16, 9), At(16, 7)], now)
		);
		Assert.Null(EarningsResultHelpers.GetNextHighlightEnd([At(16, 0)], now));
		Assert.Null(EarningsResultHelpers.GetNextHighlightEnd([], now));
	}
}
