using System.Globalization;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.Tests.Contracts.Helpers;

/// <summary>
/// Calendar ranges, the market-wide WSH filter, reading WSH's JSON and grouping by day. IB's WSH
/// answers were not seen on the wire (no subscription), so the JSON here is made up in the shapes
/// the reader is meant to cope with.
/// </summary>
public class CalendarHelpersTests
{
	private static readonly DateOnly Wednesday = new(2026, 10, 28);
	private static readonly DateOnly Day = new(2026, 10, 29);

	private static readonly IReadOnlyList<CalendarEventTypeDto> Types =
	[
		new()
		{
			Tag = "wshe_ed",
			Name = "Earnings Date",
			Kind = CalendarEventKind.Earnings,
		},
		new()
		{
			Tag = "wshe_div",
			Name = "Dividend",
			Kind = CalendarEventKind.Dividend,
		},
	];

	[Theory]
	[InlineData("2026-10-26")] // Monday
	[InlineData("2026-10-28")]
	[InlineData("2026-11-01")] // Sunday still belongs to the week that started on Monday
	public void This_week_runs_Monday_to_Sunday(string today)
	{
		var (start, end) = CalendarHelpers.GetDates(CalendarRange.ThisWeek, DateOnly.Parse(today));

		Assert.Equal(new DateOnly(2026, 10, 26), start);
		Assert.Equal(new DateOnly(2026, 11, 1), end);
	}

	[Fact]
	public void Next_week_and_this_month()
	{
		Assert.Equal(
			(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)),
			CalendarHelpers.GetDates(CalendarRange.NextWeek, Wednesday)
		);
		Assert.Equal(
			(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
			CalendarHelpers.GetDates(CalendarRange.ThisMonth, Wednesday)
		);
		Assert.Equal(
			(new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29)),
			CalendarHelpers.GetDates(CalendarRange.ThisMonth, new DateOnly(2028, 2, 10))
		);
	}

	[Fact]
	public void Today_is_the_New_York_date()
	{
		// 02:00 UTC on the 29th is still the evening of the 28th in New York
		Assert.Equal(
			Wednesday,
			CalendarHelpers.GetToday(new DateTimeOffset(2026, 10, 29, 2, 0, 0, TimeSpan.Zero))
		);
	}

	[Fact]
	public void Days_are_capped_at_a_month()
	{
		var days = CalendarHelpers.GetDays(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

		Assert.Equal(CalendarConstants.MaxDaysPerLoad, days.Count);
		Assert.Equal(new DateOnly(2026, 1, 1), days[0]);
		Assert.Equal(new DateOnly(2026, 1, 31), days[^1]);
		Assert.Single(CalendarHelpers.GetDays(Day, Day));
	}

	[Fact]
	public void The_filter_is_market_wide_with_the_limit_and_one_flag_per_tag()
	{
		Assert.Equal(
			"{\"country\":\"All\",\"limit\":100,\"wshe_ed\":\"true\",\"wshe_div\":\"true\"}",
			CalendarHelpers.BuildEventFilter(["wshe_ed", "wshe_div"])
		);
	}

	[Fact]
	public void Request_tags_follow_the_filter()
	{
		Assert.Equal(
			["wshe_ed", "wshe_div"],
			CalendarHelpers.GetRequestTags(CalendarEventFilter.All, Types)
		);
		Assert.Equal(
			["wshe_ed"],
			CalendarHelpers.GetRequestTags(CalendarEventFilter.Earnings, Types)
		);
		Assert.Equal(
			["wshe_div"],
			CalendarHelpers.GetRequestTags(CalendarEventFilter.Other, Types)
		);
	}

	[Fact]
	public void Without_types_from_the_metadata_the_tags_of_IBs_examples_are_used()
	{
		Assert.Equal(
			["wshe_ed", "wshe_bod"],
			CalendarHelpers.GetRequestTags(CalendarEventFilter.All, [])
		);
		Assert.Equal(["wshe_bod"], CalendarHelpers.GetRequestTags(CalendarEventFilter.Other, []));
		// Metadata naming earnings only: other events fall back to board meetings
		Assert.Equal(
			["wshe_bod"],
			CalendarHelpers.GetRequestTags(CalendarEventFilter.Other, [Types[0]])
		);
	}

	[Fact]
	public void Event_types_are_read_from_lists_and_from_keys()
	{
		var listed = CalendarHelpers.ParseEventTypes(
			"""
			{"meta_data":{"event_types":[
				{"tag":"wshe_ed","name":"Earnings Date"},
				{"tag":"wshe_div","display_name":"Dividend"},
				{"tag":"wshe_shm","name":"Shareholder Meeting"}
			]}}
			"""
		);
		var keyed = CalendarHelpers.ParseEventTypes(
			"""{"filters":{"wshe_ed":{"name":"Earnings Date"},"wshe_split":"Stock Split","wshe_option":"true"}}"""
		);

		Assert.Equal(
			[
				("wshe_ed", "Earnings Date", CalendarEventKind.Earnings),
				("wshe_div", "Dividend", CalendarEventKind.Dividend),
				("wshe_shm", "Shareholder Meeting", CalendarEventKind.ShareholderMeeting),
			],
			listed.Select(x => (x.Tag, x.Name, x.Kind))
		);
		Assert.Equal(
			[
				("wshe_ed", "Earnings Date", CalendarEventKind.Earnings),
				("wshe_split", "Stock Split", CalendarEventKind.Split),
				("wshe_option", "Option", CalendarEventKind.Other),
			],
			keyed.Select(x => (x.Tag, x.Name, x.Kind))
		);
		Assert.Empty(CalendarHelpers.ParseEventTypes("not json"));
	}

	[Fact]
	public void Events_are_read_from_an_array_with_nested_data()
	{
		var events = CalendarHelpers.ParseEvents(
			"""
			[
				{"event_type":"wshe_ed","index_date":"20261029","data":{"ticker":"aapl","company":"Apple Inc","conid":"265598","earnings_time":"AMC"}},
				{"event_type":"wshe_div","data":{"symbol":"KO","company_name":"Coca-Cola Co","ex_date":"2026-10-30"}}
			]
			""",
			Types,
			Day
		)!;

		Assert.Equal(2, events.Count);
		var apple = events[0];
		Assert.Equal(
			(Day, "AAPL", "Apple Inc", 265598, CalendarEventKind.Earnings, "Earnings Date"),
			(apple.Date, apple.Symbol, apple.Company, apple.ConId, apple.Kind, apple.TypeName)
		);
		Assert.Equal(CalendarEventTime.AfterClose, apple.Time);
		Assert.Equal(new DateOnly(2026, 10, 30), events[1].Date);
		Assert.Equal(CalendarEventKind.Dividend, events[1].Kind);
	}

	[Fact]
	public void Events_are_read_from_a_wrapping_object_and_go_on_the_day_asked_without_a_date()
	{
		var events = CalendarHelpers.ParseEvents(
			"""{"total":1,"events":[{"type":"wshe_bod","ticker":"MSFT","time_of_day":"10:00 ET"}]}""",
			Types,
			Day
		)!;

		var meeting = Assert.Single(events);
		Assert.Equal(Day, meeting.Date);
		Assert.Equal(CalendarEventKind.BoardMeeting, meeting.Kind);
		Assert.Equal("Board meeting", meeting.TypeName);
		Assert.Equal(CalendarEventTime.Unknown, meeting.Time);
		Assert.Equal("10:00 ET", meeting.TimeText);
	}

	[Theory]
	[InlineData("[]")]
	[InlineData("{}")]
	[InlineData("{\"events\":[]}")]
	[InlineData("")]
	public void An_answer_without_events_is_an_empty_list(string json) =>
		Assert.Empty(CalendarHelpers.ParseEvents(json, Types, Day)!);

	[Theory]
	[InlineData("News feed is not allowed.")]
	[InlineData("{\"error\":\"no data\"}")]
	[InlineData("[{\"foo\":1},{\"bar\":2}]")]
	[InlineData("42")]
	public void An_answer_that_cannot_be_read_is_null_not_empty(string json) =>
		Assert.Null(CalendarHelpers.ParseEvents(json, Types, Day));

	[Theory]
	[InlineData("BMO", CalendarEventTime.BeforeOpen)]
	[InlineData("Before Market Open", CalendarEventTime.BeforeOpen)]
	[InlineData("pre-market", CalendarEventTime.BeforeOpen)]
	[InlineData("AMC", CalendarEventTime.AfterClose)]
	[InlineData("After Market Close", CalendarEventTime.AfterClose)]
	[InlineData("DMH", CalendarEventTime.DuringMarket)]
	[InlineData("", CalendarEventTime.Unknown)]
	[InlineData("Unspecified", CalendarEventTime.Unknown)]
	public void Times_of_day(string text, CalendarEventTime expected) =>
		Assert.Equal(expected, CalendarHelpers.ParseTime(text));

	[Theory]
	[InlineData("20261029")]
	[InlineData("2026-10-29")]
	[InlineData("2026-10-29T13:30:00Z")]
	[InlineData("10/29/2026")]
	[InlineData("10/29/2026 ")]
	public void Dates(string text) => Assert.Equal(Day, CalendarHelpers.ParseDate(text));

	[Fact]
	public void Events_are_grouped_by_day_in_trading_day_order_without_duplicates()
	{
		CalendarEventDto Event(int day, string symbol, CalendarEventTime time) =>
			new()
			{
				Date = new DateOnly(2026, 10, day),
				Symbol = symbol,
				TypeTag = "wshe_ed",
				Time = time,
			};

		var days = CalendarHelpers.GroupByDay(
			[
				Event(30, "XOM", CalendarEventTime.BeforeOpen),
				Event(29, "META", CalendarEventTime.AfterClose),
				Event(29, "MSFT", CalendarEventTime.Unknown),
				Event(29, "CAT", CalendarEventTime.BeforeOpen),
				Event(29, "AMZN", CalendarEventTime.AfterClose),
				Event(29, "CAT", CalendarEventTime.BeforeOpen),
			],
			new HashSet<DateOnly> { new(2026, 10, 30) }
		);

		Assert.Equal(
			[new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 30)],
			days.Select(x => x.Date)
		);
		Assert.Equal(["CAT", "AMZN", "META", "MSFT"], days[0].Events.Select(x => x.Symbol));
		Assert.False(days[0].IsTruncated);
		Assert.True(days[1].IsTruncated);
	}

	[Fact]
	public void Only_10276_means_not_subscribed()
	{
		Assert.True(CalendarHelpers.IsNotSubscribed(10276));
		Assert.False(CalendarHelpers.IsNotSubscribed(200));
	}

	[Fact]
	public void Nasdaqs_single_digit_month_dates_are_read() =>
		Assert.Equal(new DateOnly(2026, 9, 5), CalendarHelpers.ParseDate("9/5/2026"));

	[Theory]
	[InlineData(CalendarEventKind.Earnings, CalendarEventFilter.Earnings, true)]
	[InlineData(CalendarEventKind.Economic, CalendarEventFilter.Earnings, false)]
	[InlineData(CalendarEventKind.Economic, CalendarEventFilter.Economic, true)]
	[InlineData(CalendarEventKind.Dividend, CalendarEventFilter.Economic, false)]
	[InlineData(CalendarEventKind.Dividend, CalendarEventFilter.Other, true)]
	[InlineData(CalendarEventKind.Economic, CalendarEventFilter.Other, false)]
	[InlineData(CalendarEventKind.Earnings, CalendarEventFilter.Other, false)]
	[InlineData(CalendarEventKind.Economic, CalendarEventFilter.All, true)]
	public void Filters(CalendarEventKind kind, CalendarEventFilter filter, bool shown) =>
		Assert.Equal(shown, CalendarHelpers.Matches(kind, filter));

	// Saved per widget as integers: the values in use must never move
	[Fact]
	public void Persisted_enum_values_are_stable()
	{
		Assert.Equal(
			[0, 1, 2, 3],
			new[]
			{
				CalendarEventFilter.All,
				CalendarEventFilter.Earnings,
				CalendarEventFilter.Other,
				CalendarEventFilter.Economic,
			}.Select(x => (int)x)
		);
		Assert.Equal(6, (int)CalendarEventKind.Other);
		Assert.Equal(7, (int)CalendarEventKind.Economic);
	}

	[Fact]
	public void The_economic_calendar_is_asked_for_the_next_date()
	{
		Assert.Equal(
			Day.AddDays(1),
			CalendarHelpers.GetNasdaqRequestDate(NasdaqCalendar.Economic, Day)
		);
		Assert.Equal(Day, CalendarHelpers.GetNasdaqRequestDate(NasdaqCalendar.Earnings, Day));
		Assert.Equal(Day, CalendarHelpers.GetNasdaqRequestDate(NasdaqCalendar.Splits, Day));
	}

	[Fact]
	public void Enabled_sources_follow_the_settings() =>
		Assert.Equal(
			[CalendarSource.WallStreetHorizon],
			CalendarHelpers.GetEnabledSources(
				new AppSettingsDto { CalendarWallStreetHorizon = true, CalendarNasdaq = false }
			)
		);

	[Fact]
	public void The_same_symbol_kind_and_day_from_two_sources_is_one_event_filled_from_both()
	{
		var wsh = new CalendarEventDto
		{
			Date = Day,
			Kind = CalendarEventKind.Earnings,
			Symbol = "AAPL",
			ConId = 265598,
			Source = CalendarSource.WallStreetHorizon,
		};
		var nasdaq = new CalendarEventDto
		{
			Date = Day,
			Kind = CalendarEventKind.Earnings,
			Symbol = "aapl",
			Company = "Apple Inc.",
			Time = CalendarEventTime.AfterClose,
			Detail = "EPS est. $1.75",
			Source = CalendarSource.Nasdaq,
		};
		var dividend = new CalendarEventDto
		{
			Date = Day,
			Kind = CalendarEventKind.Dividend,
			Symbol = "AAPL",
		};

		var merged = CalendarHelpers.Merge([wsh, nasdaq, dividend]);

		Assert.Equal(2, merged.Count);
		var earnings = merged[0];
		Assert.Equal(
			(
				CalendarSource.WallStreetHorizon,
				265598,
				"Apple Inc.",
				CalendarEventTime.AfterClose,
				"EPS est. $1.75"
			),
			(earnings.Source, earnings.ConId, earnings.Company, earnings.Time, earnings.Detail)
		);
		// The source's own event is not changed (it is cached)
		Assert.Equal(string.Empty, wsh.Company);
	}

	[Fact]
	public void A_merged_earnings_event_takes_the_EPS_figures_another_source_has()
	{
		var wsh = new CalendarEventDto
		{
			Date = Day,
			Kind = CalendarEventKind.Earnings,
			Symbol = "MSFT",
			Company = "Microsoft",
			Time = CalendarEventTime.AfterClose,
			Detail = "Q1 earnings",
			ConId = 272093,
			Source = CalendarSource.WallStreetHorizon,
		};
		var nasdaq = new CalendarEventDto
		{
			Date = Day,
			Kind = CalendarEventKind.Earnings,
			Symbol = "MSFT",
			EpsEstimate = 4.71m,
			EpsActual = 4.89m,
			EpsSurprisePercent = 3.82m,
			Source = CalendarSource.Nasdaq,
		};

		var merged = Assert.Single(CalendarHelpers.Merge([wsh, nasdaq]));

		Assert.Equal(
			(CalendarSource.WallStreetHorizon, "Q1 earnings", 4.71m, 4.89m, 3.82m),
			(
				merged.Source,
				merged.Detail,
				merged.EpsEstimate,
				merged.EpsActual,
				merged.EpsSurprisePercent
			)
		);
		Assert.Null(wsh.EpsActual);
	}

	[Theory]
	[InlineData("$4.89", "4.89")]
	[InlineData("($0.04)", "-0.04")]
	[InlineData("-$1,234.5", "-1234.5")]
	[InlineData("-27.89", "-27.89")]
	[InlineData("3.82%", "3.82")]
	[InlineData(".5", "0.5")]
	[InlineData("N/A", null)]
	[InlineData("", null)]
	[InlineData(null, null)]
	[InlineData("$", null)]
	[InlineData("()", null)]
	public void Figures_are_read_as_sources_write_them(string? text, string? expected) =>
		Assert.Equal(
			expected is null ? null : decimal.Parse(expected, CultureInfo.InvariantCulture),
			CalendarHelpers.ParseFigure(text)
		);

	[Fact]
	public void EPS_and_surprise_are_formatted_for_the_columns()
	{
		Assert.Equal(
			["$4.89", "-$0.04", "$13.50", "$0.1234", "\u2014"],
			new decimal?[] { 4.89m, -0.04m, 13.5m, 0.1234m, null }.Select(CalendarHelpers.FormatEps)
		);
		Assert.Equal(
			["+3.8%", "-27.9%", "0.0%", "\u2014"],
			new decimal?[] { 3.82m, -27.89m, 0m, null }.Select(CalendarHelpers.FormatSurprise)
		);
		Assert.Equal(string.Empty, CalendarHelpers.FormatEarningsDetail(null, null, null));
		Assert.Equal("EPS est. $1.75", CalendarHelpers.FormatEarningsDetail(1.75m, null, null));
	}

	[Fact]
	public void Other_kinds_merge_only_with_the_same_type()
	{
		CalendarEventDto Other(string type) =>
			new()
			{
				Date = Day,
				Kind = CalendarEventKind.Other,
				Symbol = "AAPL",
				TypeName = type,
			};

		Assert.Equal(
			2,
			CalendarHelpers
				.Merge([Other("Option expiry"), Other("Investor day"), Other("Investor day")])
				.Count
		);
	}

	// Japan's Leading Index comes twice: as a change and as a level
	[Fact]
	public void Economic_releases_merge_only_when_everything_is_the_same()
	{
		CalendarEventDto Release(string detail) =>
			new()
			{
				Date = Day,
				Kind = CalendarEventKind.Economic,
				Company = "Leading Index",
				TypeName = "Japan",
				TimeText = "01:00",
				Detail = detail,
			};

		Assert.Equal(
			2,
			CalendarHelpers
				.Merge([Release("Act. 0.4%"), Release("Act. 118.0"), Release("Act. 118.0")])
				.Count
		);
	}

	[Fact]
	public void Economic_releases_come_first_in_a_day_by_time_all_day_first()
	{
		CalendarEventDto Release(string time, string name) =>
			new()
			{
				Date = Day,
				Kind = CalendarEventKind.Economic,
				Company = name,
				TimeText = time,
			};

		var day = Assert.Single(
			CalendarHelpers.GroupByDay([
				new()
				{
					Date = Day,
					Kind = CalendarEventKind.Earnings,
					Symbol = "KO",
					Time = CalendarEventTime.BeforeOpen,
				},
				Release("10:30", "Dallas Fed"),
				Release("08:30", "Chicago Fed"),
				Release("All day", "Labour Day"),
			])
		);

		Assert.Equal(
			["Labour Day", "Chicago Fed", "Dallas Fed", ""],
			day.Events.Select(x => x.Company)
		);
	}

	[Fact]
	public void Today_is_one_day()
	{
		Assert.Equal(
			(Wednesday, Wednesday),
			CalendarHelpers.GetDates(CalendarRange.Today, Wednesday)
		);
	}

	// 28 October 2026 is still summer time in New York (UTC-4)
	[Theory]
	[InlineData(
		CalendarEventKind.Economic,
		CalendarEventTime.Unknown,
		"08:30",
		"2026-10-28T12:30:00Z"
	)]
	[InlineData(
		CalendarEventKind.Economic,
		CalendarEventTime.Unknown,
		"1:00",
		"2026-10-28T05:00:00Z"
	)]
	[InlineData(
		CalendarEventKind.Earnings,
		CalendarEventTime.BeforeOpen,
		"",
		"2026-10-28T13:30:00Z"
	)]
	[InlineData(
		CalendarEventKind.Earnings,
		CalendarEventTime.DuringMarket,
		"",
		"2026-10-28T20:00:00Z"
	)]
	[InlineData(
		CalendarEventKind.Earnings,
		CalendarEventTime.AfterClose,
		"",
		"2026-10-29T00:00:00Z"
	)]
	// No time, all day or a text: once the day is over in New York
	[InlineData(CalendarEventKind.Earnings, CalendarEventTime.Unknown, "", "2026-10-29T04:00:00Z")]
	[InlineData(
		CalendarEventKind.Economic,
		CalendarEventTime.Unknown,
		"All day",
		"2026-10-29T04:00:00Z"
	)]
	[InlineData(
		CalendarEventKind.Other,
		CalendarEventTime.Unknown,
		"Tentative",
		"2026-10-29T04:00:00Z"
	)]
	public void An_event_turns_past_at_its_time_in_New_York(
		CalendarEventKind kind,
		CalendarEventTime time,
		string timeText,
		string passedAt
	)
	{
		var item = new CalendarEventDto
		{
			Date = Wednesday,
			Kind = kind,
			Time = time,
			TimeText = timeText,
		};
		var zone = TradeJournalHelpers.GetTradeDayTimeZone();
		var expected = DateTimeOffset.Parse(passedAt);

		Assert.Equal(expected, CalendarHelpers.GetPassedAt(item, zone));
		Assert.False(CalendarHelpers.IsPast(item, expected.AddTicks(-1), zone));
		Assert.True(CalendarHelpers.IsPast(item, expected, zone));
	}

	[Fact]
	public void The_next_change_is_the_next_event_to_pass_or_New_York_midnight()
	{
		var zone = TradeJournalHelpers.GetTradeDayTimeZone();
		var now = DateTimeOffset.Parse("2026-10-28T15:00:00Z");
		var midnight = DateTimeOffset.Parse("2026-10-29T04:00:00Z");
		DateTimeOffset[] passedAt =
		[
			DateTimeOffset.Parse("2026-10-28T12:30:00Z"), // already past
			DateTimeOffset.Parse("2026-10-28T20:00:00Z"),
			DateTimeOffset.Parse("2026-10-28T17:00:00Z"),
			DateTimeOffset.Parse("2026-10-29T13:30:00Z"), // tomorrow
		];

		Assert.Equal(
			DateTimeOffset.Parse("2026-10-28T17:00:00Z"),
			CalendarHelpers.GetNextChange(passedAt, now, zone)
		);
		Assert.Equal(midnight, CalendarHelpers.GetNextChange([passedAt[0]], now, zone));
		Assert.Equal(midnight, CalendarHelpers.GetNextChange([], now, zone));
	}
}
