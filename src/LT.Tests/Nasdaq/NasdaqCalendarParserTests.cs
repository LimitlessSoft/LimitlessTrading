using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Nasdaq;

namespace LT.Tests.Nasdaq;

/// <summary>Nasdaq's calendar answers, as recorded (<see cref="NasdaqFixtures"/>).</summary>
public class NasdaqCalendarParserTests
{
	private static readonly DateOnly Day = new(2026, 10, 27);

	private static IReadOnlyList<CalendarEventDto> ParsePast() =>
		NasdaqCalendarParser.Parse(
			NasdaqCalendar.Earnings,
			NasdaqFixtures.EarningsPast,
			new DateOnly(2026, 10, 28)
		);

	[Fact]
	public void Earnings_have_the_time_of_day_and_the_EPS_estimate()
	{
		var events = NasdaqCalendarParser.Parse(
			NasdaqCalendar.Earnings,
			NasdaqFixtures.Earnings,
			Day
		);

		Assert.Equal(["V", "KO", "HSBC"], events.Select(x => x.Symbol));
		Assert.Equal(
			[CalendarEventTime.AfterClose, CalendarEventTime.BeforeOpen, CalendarEventTime.Unknown],
			events.Select(x => x.Time)
		);
		var visa = events[0];
		Assert.Equal(
			(Day, CalendarEventKind.Earnings, "Visa Inc.", "EPS est. $3.43", CalendarSource.Nasdaq),
			(visa.Date, visa.Kind, visa.Company, visa.Detail, visa.Source)
		);
		Assert.Equal(string.Empty, events[2].TimeText);
	}

	[Fact]
	public void Todays_earnings_have_the_estimate_and_no_result_yet()
	{
		var events = NasdaqCalendarParser.Parse(
			NasdaqCalendar.Earnings,
			NasdaqFixtures.Earnings,
			Day
		);

		Assert.Equal(
			[(3.43m, null, null), (0.87m, null, null), (2.13m, null, null)],
			events.Select(x => (x.EpsEstimate, x.EpsActual, x.EpsSurprisePercent))
		);
	}

	[Fact]
	public void A_past_days_earnings_have_the_reported_EPS_and_a_beat()
	{
		var msft = ParsePast().Single(x => x.Symbol == "MSFT");

		Assert.Equal(
			(4.71m, 4.89m, 3.82m, "EPS $4.89 · est. $4.71 · +3.8%"),
			(msft.EpsEstimate, msft.EpsActual, msft.EpsSurprisePercent, msft.Detail)
		);
	}

	[Fact]
	public void A_past_days_earnings_have_a_miss_as_a_negative_surprise()
	{
		var goog = ParsePast().Single(x => x.Symbol == "GOOG");

		Assert.Equal(
			(2.94m, 2.12m, -27.89m, "EPS $2.12 · est. $2.94 · -27.9%"),
			(goog.EpsEstimate, goog.EpsActual, goog.EpsSurprisePercent, goog.Detail)
		);
	}

	[Fact]
	public void EPS_in_parentheses_is_negative()
	{
		var stla = ParsePast().Single(x => x.Symbol == "STLA");

		Assert.Equal(
			(-0.07m, -0.01m, 85.71m, "EPS -$0.01 · est. -$0.07 · +85.7%"),
			(stla.EpsEstimate, stla.EpsActual, stla.EpsSurprisePercent, stla.Detail)
		);
	}

	[Fact]
	public void A_missing_surprise_or_estimate_is_no_figure()
	{
		var events = ParsePast();

		var bbva = events.Single(x => x.Symbol == "BBVA");
		Assert.Equal(
			(0.62m, 0.49m, null, "EPS $0.49 · est. $0.62", CalendarEventTime.AfterClose),
			(bbva.EpsEstimate, bbva.EpsActual, bbva.EpsSurprisePercent, bbva.Detail, bbva.Time)
		);
		var morn = events.Single(x => x.Symbol == "MORN");
		Assert.Equal(
			(null, 3.48m, null, "EPS $3.48"),
			(morn.EpsEstimate, morn.EpsActual, morn.EpsSurprisePercent, morn.Detail)
		);
		// "&nbsp;" figures and an empty surprise: still an earnings date, without figures
		var none = events.Single(x => x.Symbol == "ZZZZ");
		Assert.Equal(
			(null, null, null, string.Empty, CalendarEventKind.Earnings),
			(none.EpsEstimate, none.EpsActual, none.EpsSurprisePercent, none.Detail, none.Kind)
		);
	}

	[Fact]
	public void Dividends_go_on_their_ex_date_with_the_amount_and_payment_date()
	{
		var events = NasdaqCalendarParser.Parse(
			NasdaqCalendar.Dividends,
			NasdaqFixtures.Dividends,
			Day
		);

		var asml = events[0];
		Assert.Equal(
			("ASML", Day, CalendarEventKind.Dividend, "Ex-dividend", "2.1077 · pays 4 Nov"),
			(asml.Symbol, asml.Date, asml.Kind, asml.TypeName, asml.Detail)
		);
		Assert.Equal("0.21 · pays 29 Oct", events[1].Detail);
	}

	[Fact]
	public void Splits_keep_their_own_effective_dates_and_a_tidy_ratio()
	{
		var events = NasdaqCalendarParser.Parse(NasdaqCalendar.Splits, NasdaqFixtures.Splits, Day);

		Assert.Equal(
			[
				("MMT", new DateOnly(2026, 11, 9), "1:4"),
				("SOXX", new DateOnly(2026, 11, 5), "3:1"),
				("NUWE", Day, "1:9"),
			],
			events.Select(x => (x.Symbol, x.Date, x.Detail))
		);
		Assert.All(events, x => Assert.Equal(CalendarEventKind.Split, x.Kind));
	}

	[Fact]
	public void Economic_releases_have_no_symbol_and_show_their_figures()
	{
		var events = NasdaqCalendarParser.Parse(
			NasdaqCalendar.Economic,
			NasdaqFixtures.Economic,
			Day
		);

		Assert.Equal(5, events.Count);
		Assert.All(
			events,
			x =>
			{
				Assert.Equal(CalendarEventKind.Economic, x.Kind);
				Assert.Equal(string.Empty, x.Symbol);
				Assert.Equal(Day, x.Date);
			}
		);
		var chicago = events[3];
		Assert.Equal(
			(
				"Chicago Fed National Activity",
				"United States",
				"08:30",
				"Act. 0.07 · Cons. -0.10 · Prev. -0.08"
			),
			(chicago.Company, chicago.TypeName, chicago.TimeText, chicago.Detail)
		);
		// "&nbsp;" and blanks are no figures; "All Day" reads as a time
		Assert.Equal(string.Empty, events[2].Detail);
		Assert.Equal(("All day", string.Empty), (events[4].TimeText, events[4].Detail));
	}

	[Theory]
	[InlineData(NasdaqCalendar.Earnings, NasdaqFixtures.EarningsWeekend)]
	[InlineData(NasdaqCalendar.Dividends, NasdaqFixtures.DividendsWeekend)]
	[InlineData(NasdaqCalendar.Earnings, NasdaqFixtures.NoRecord)]
	[InlineData(NasdaqCalendar.Splits, NasdaqFixtures.NoRecord)]
	public void A_day_without_events_is_an_empty_list(NasdaqCalendar calendar, string json) =>
		Assert.Empty(NasdaqCalendarParser.Parse(calendar, json, Day));

	[Fact]
	public void A_refusal_says_what_Nasdaq_said()
	{
		var error = Assert.Throws<NasdaqServiceException>(() =>
			NasdaqCalendarParser.Parse(NasdaqCalendar.Earnings, NasdaqFixtures.BadParameter, Day)
		);

		Assert.Contains("Bad or No parameter garbage", error.Message);
	}

	[Theory]
	[InlineData("<html><body>Access Denied</body></html>")]
	[InlineData("""{"unexpected":true}""")]
	[InlineData("""{"data":{"table":[]},"status":{"rCode":200}}""")]
	[InlineData("""{"data":{"rows":[{"ticker":"AAPL"}]},"status":{"rCode":200}}""")]
	public void An_answer_in_another_shape_is_an_error_not_an_empty_list(string json)
	{
		var error = Assert.Throws<NasdaqServiceException>(() =>
			NasdaqCalendarParser.Parse(NasdaqCalendar.Earnings, json, Day)
		);

		Assert.Contains("can't read", error.Message);
	}
}
