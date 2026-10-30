using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Calendar;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.Settings;
using LT.Domain.CalendarSources;
using LT.Domain.EarningsResultSources;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The Calendar widget never shows an unexplained empty list: no source on, no WSH subscription, a
/// failed source and a range without events each say so. Nasdaq loads without IB; events show
/// under their day and a click links the stock.
/// </summary>
public class CalendarViewModelTests : IDisposable
{
	private static readonly DateOnly Thursday = new(2026, 10, 29);

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeNasdaqApiClient _nasdaq = new();
	private readonly FakeYahooApiClient _yahoo = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly LinkGroupStore _links = new();
	private readonly SettingsNavigator _navigator = new();
	private SettingsManager _settings = null!;
	private CalendarViewModel? _calendar;

	// Created by Open() (never lazily: inside TestUiThread.Invoke that would deadlock)
	private CalendarViewModel Calendar =>
		_calendar ?? throw new InvalidOperationException("Call Open() first.");

	/// <summary>Opens the widget after the test set its sources and answers.</summary>
	private void Open() => _calendar ??= TestUiThread.Invoke(CreateCalendar);

	public CalendarViewModelTests() => LSCoreContainer.EnsureInitialized();

	private void UseSources(bool wsh, bool nasdaq)
	{
		_cache.Settings.Settings.General.CalendarWallStreetHorizon = wsh;
		_cache.Settings.Settings.General.CalendarNasdaq = nasdaq;
	}

	private static CalendarEventDto Earnings(string symbol, DateOnly? day = null) =>
		new()
		{
			Date = day ?? Thursday,
			Kind = CalendarEventKind.Earnings,
			TypeName = "Earnings",
			Symbol = symbol,
			Company = symbol + " Inc.",
			Time = CalendarEventTime.AfterClose,
			Source = CalendarSource.Nasdaq,
		};

	[Fact]
	public async Task Offline_it_loads_Nasdaq_without_IB()
	{
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Thursday ? [Earnings("AAPL")] : [];

		await Loaded();

		Assert.False(Calendar.IsConnected);
		Assert.Equal(2, Calendar.Items.Count);
		Assert.EndsWith(CalendarConstants.NasdaqSourceName, Calendar.StatusText);
		Assert.False(Calendar.HasSourceWarning);
		Assert.Equal(0, _ibkr.WshMetaDataCalls);
	}

	[Fact]
	public async Task With_only_WSH_on_and_no_subscription_it_shows_what_to_subscribe_to()
	{
		UseSources(wsh: true, nasdaq: false);
		_ibkr.WshException = new IbkrRequestException(10276, "News feed is not allowed.");

		await Connect();

		Assert.True(Calendar.HasNotice);
		Assert.Equal("Wall Street Horizon subscription needed", Calendar.NoticeTitle);
		Assert.Contains(CalendarConstants.SubscriptionName, Calendar.NoticeText);
		Assert.Empty(Calendar.Items);
		Assert.False(Calendar.HasEmptyText);
	}

	[Fact]
	public async Task A_failed_source_is_named_in_the_status_and_the_others_events_still_show()
	{
		UseSources(wsh: true, nasdaq: true);
		_ibkr.WshException = new IbkrRequestException(10276, "News feed is not allowed.");
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Thursday ? [Earnings("AAPL")] : [];

		await Connect();

		Assert.False(Calendar.HasNotice);
		Assert.Equal(2, Calendar.Items.Count);
		Assert.StartsWith(
			CalendarConstants.WallStreetHorizonSourceName,
			Calendar.SourceWarningText
		);
		Assert.Contains("10276", Calendar.SourceWarningText);
	}

	[Fact]
	public async Task A_range_without_events_says_so()
	{
		await Loaded();

		Assert.Empty(Calendar.Items);
		Assert.StartsWith("No events between", Calendar.EmptyText);
		Assert.False(Calendar.HasNotice);
	}

	[Fact]
	public async Task Economic_releases_show_with_their_figures()
	{
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Economic && day == Thursday
				?
				[
					new()
					{
						Date = day,
						Kind = CalendarEventKind.Economic,
						Company = "GDP",
						TypeName = "United States",
						TimeText = "08:30",
						Detail = "Act. 2.8% · Cons. 2.6%",
						Source = CalendarSource.Nasdaq,
					},
				]
				: [];
		// Switched in the same UI job that creates it: another test's RunJobs on the shared UI
		// thread can't start the first load with every calendar in between
		_calendar = TestUiThread.Invoke(() =>
		{
			var calendar = CreateCalendar();
			calendar.Filter = CalendarEventFilter.Economic;
			return calendar;
		});

		await Loaded();

		Assert.True(Calendar.IsEconomic);
		Assert.NotEmpty(_nasdaq.Requests);
		Assert.All(_nasdaq.Requests, x => Assert.Equal(NasdaqCalendar.Economic, x.Calendar));
		var row = Assert.IsType<CalendarEventRowViewModel>(Calendar.Items[1]);
		Assert.Equal("08:30", row.TimeText);
		Assert.True(row.HasDetail);
		Assert.Equal("GDP · United States · Act. 2.8% · Cons. 2.6% · from Nasdaq", row.ToolTipText);
	}

	[Fact]
	public async Task Turning_every_source_off_in_Settings_says_where_to_turn_one_on()
	{
		await Loaded();

		TestUiThread.Invoke(() =>
			_settings.SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = MarketDataType.Live,
					HeartbeatSeconds = ConnectionConstants.DefaultHeartbeatSeconds,
					CalendarNasdaq = false,
				}
			)
		);
		await WaitFor(() => Calendar.HasNotice);

		Assert.Equal("No calendar source is on", Calendar.NoticeTitle);
		Assert.Contains("Settings → Calendar", Calendar.NoticeText);
	}

	[Fact]
	public async Task Sources_opens_Settings_on_the_calendar_page()
	{
		string? opened = null;
		_navigator.OpenRequested += x => opened = x;
		await Loaded();

		TestUiThread.Invoke(() => Calendar.OpenSourcesCommand.Execute(null));

		Assert.Equal("Calendar", opened);
	}

	[Fact]
	public async Task Events_show_under_their_day_and_a_click_links_the_stock()
	{
		UseSources(wsh: true, nasdaq: false);
		var aapl = new ContractDto
		{
			ConId = 265598,
			Symbol = "AAPL",
			SecurityType = "STK",
			Exchange = "SMART",
			Currency = "USD",
		};
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = aapl }];
		_ibkr.WshEventsBy = q =>
			q.StartDate.Day == 29
				? """[{"event_type":"wshe_ed","ticker":"AAPL","company":"Apple Inc","earnings_time":"AMC"}]"""
				: "[]";

		await Connect();

		var header = Assert.IsType<CalendarDayHeaderViewModel>(Calendar.Items[0]);
		var row = Assert.IsType<CalendarEventRowViewModel>(Calendar.Items[1]);
		Assert.StartsWith("Thu 29 Oct", header.Title);
		Assert.Equal("After close", row.TimeText);
		Assert.Null(Calendar.EmptyText);

		TestUiThread.Invoke(() => Calendar.SelectedItem = row);
		await WaitFor(() => _links.Get(LinkGroup.Red) != null);
		Assert.Equal(aapl.ConId, _links.Get(LinkGroup.Red)!.ConId);
	}

	[Fact]
	public void Earnings_rows_show_their_figures_in_columns_with_a_beat_or_a_miss()
	{
		CalendarEventRowViewModel Row(CalendarEventDto item) =>
			new(item, string.Empty, DateTimeOffset.MinValue, isPast: true);
		var beat = Row(
			new CalendarEventDto
			{
				Kind = CalendarEventKind.Earnings,
				TypeName = "Earnings",
				Symbol = "MSFT",
				EpsEstimate = 4.71m,
				EpsActual = 4.89m,
				EpsSurprisePercent = 3.82m,
			}
		);
		var miss = Row(
			new CalendarEventDto
			{
				Kind = CalendarEventKind.Earnings,
				Symbol = "GOOG",
				EpsEstimate = 2.94m,
				EpsActual = 2.12m,
				EpsSurprisePercent = -27.89m,
			}
		);
		var upcoming = Row(
			new CalendarEventDto
			{
				Kind = CalendarEventKind.Earnings,
				Symbol = "V",
				EpsEstimate = 3.43m,
			}
		);
		var noSurprise = Row(
			new CalendarEventDto
			{
				Kind = CalendarEventKind.Earnings,
				Symbol = "BBVA",
				EpsActual = 0.49m,
			}
		);
		var wshOnly = Row(
			new CalendarEventDto
			{
				Kind = CalendarEventKind.Earnings,
				Symbol = "AAPL",
				Detail = "Q4 earnings",
			}
		);
		var dividend = Row(
			new CalendarEventDto
			{
				Kind = CalendarEventKind.Dividend,
				TypeName = "Ex-dividend",
				Symbol = "ASML",
				Detail = "2.1077 · pays 4 Nov",
			}
		);

		Assert.Equal(
			(true, false, "$4.71", "$4.89", "+3.8%", true, false),
			(
				beat.ShowsFigures,
				beat.ShowsTypeName,
				beat.EstimateText,
				beat.ActualText,
				beat.SurpriseText,
				beat.IsBeat,
				beat.IsMiss
			)
		);
		Assert.Equal(("-27.9%", false, true), (miss.SurpriseText, miss.IsBeat, miss.IsMiss));
		// Not reported yet: the estimate only, no dashes where the result will be
		Assert.Equal(
			("$3.43", "", "", false, false),
			(
				upcoming.EstimateText,
				upcoming.ActualText,
				upcoming.SurpriseText,
				upcoming.IsBeat,
				upcoming.IsMiss
			)
		);
		Assert.Equal(
			("\u2014", "$0.49", "\u2014", false, false),
			(
				noSurprise.EstimateText,
				noSurprise.ActualText,
				noSurprise.SurpriseText,
				noSurprise.IsBeat,
				noSurprise.IsMiss
			)
		);
		Assert.Equal((false, true), (wshOnly.ShowsFigures, wshOnly.ShowsDetail));
		Assert.Equal(
			(false, true, true),
			(dividend.ShowsFigures, dividend.ShowsDetail, dividend.ShowsTypeName)
		);
	}

	private static readonly DateOnly Wednesday = new(2026, 10, 28);

	private static CalendarEventDto Release(DateOnly day, string name, string time) =>
		new()
		{
			Date = day,
			Kind = CalendarEventKind.Economic,
			Company = name,
			TypeName = "United States",
			TimeText = time,
			Source = CalendarSource.Nasdaq,
		};

	// Wednesday 16:00 New York: the 08:30 release is out, AAPL reports after the close, MSFT
	// tomorrow
	private void AnswerWednesdayAndThursday() =>
		_nasdaq.Answer = (calendar, day) =>
			(calendar, day) switch
			{
				(NasdaqCalendar.Economic, var d) when d == Wednesday =>
				[
					Release(Wednesday, "GDP", "08:30"),
				],
				(NasdaqCalendar.Earnings, var d) when d == Wednesday =>
				[
					Earnings("AAPL", Wednesday),
				],
				(NasdaqCalendar.Earnings, var d) when d == Thursday => [Earnings("MSFT")],
				_ => [],
			};

	private string[] Lines() => TestUiThread.Invoke(LinesOnUiThread);

	// The list as text, read on the UI thread (inside WaitFor's condition, which runs there)
	private string[] LinesOnUiThread() =>
		[
			.. Calendar.Items.Select(x =>
				x switch
				{
					CalendarDayHeaderViewModel h => (h.IsToday ? "today " : "") + h.Date.Day,
					CalendarNowMarkerViewModel => "now",
					CalendarEventRowViewModel r => (r.IsPast ? "past " : "")
						+ (r.Event.Symbol.Length > 0 ? r.Event.Symbol : r.Event.Company),
					_ => "?",
				}
			),
		];

	[Fact]
	public async Task Past_events_are_marked_and_the_now_line_sits_before_the_next_one()
	{
		AnswerWednesdayAndThursday();
		CalendarItemViewModel? scrolledTo = null;
		_calendar = TestUiThread.Invoke(() =>
		{
			var calendar = CreateCalendar();
			calendar.ScrollRequested += x => scrolledTo = x;
			return calendar;
		});

		await Loaded();

		Assert.Equal(["today 28", "past GDP", "now", "AAPL", "29", "MSFT"], Lines());
		Assert.IsType<CalendarNowMarkerViewModel>(scrolledTo);
	}

	[Fact]
	public async Task As_time_passes_events_turn_past_and_a_new_day_moves_today()
	{
		AnswerWednesdayAndThursday();
		await Loaded();

		// 20:00 New York: AAPL's after-close report is in; tomorrow's events are still ahead
		_cache.Time.Advance(TimeSpan.FromHours(4));
		await WaitFor(() =>
			LinesOnUiThread()
				.SequenceEqual(["today 28", "past GDP", "past AAPL", "now", "29", "MSFT"])
		);

		// Midnight in New York: the range loads again and Thursday is today
		_cache.Time.Advance(TimeSpan.FromHours(4));
		await WaitFor(() =>
			LinesOnUiThread()
				.SequenceEqual(["28", "past GDP", "past AAPL", "today 29", "now", "MSFT"])
		);
	}

	[Fact]
	public async Task A_release_shows_its_country_as_a_flag()
	{
		AnswerWednesdayAndThursday();
		await Loaded();

		var release = TestUiThread.Invoke(() =>
			Calendar.Items.OfType<CalendarEventRowViewModel>().First(x => x.Event.Company == "GDP")
		);
		var earnings = TestUiThread.Invoke(() =>
			Calendar.Items.OfType<CalendarEventRowViewModel>().First(x => x.Event.Symbol == "AAPL")
		);
		Assert.Equal("US", release.FlagCode);
		Assert.False(release.ShowsTypeName);
		Assert.Null(earnings.FlagCode);
		// Earnings name no type: their figure columns tell them apart
		Assert.False(earnings.ShowsTypeName);
		Assert.True(earnings.ShowsFigures);
	}

	[Fact]
	public async Task The_earnings_captions_sit_over_a_days_first_earnings_row()
	{
		AnswerWednesdayAndThursday();
		await Loaded();

		var (headers, rows) = TestUiThread.Invoke(() =>
			(
				Calendar.Items.OfType<CalendarDayHeaderViewModel>().ToList(),
				Calendar.Items.OfType<CalendarEventRowViewModel>().ToList()
			)
		);

		// Wednesday opens with a release (its figures labelled their own way): the captions go
		// over AAPL; Thursday opens with earnings: in its header
		Assert.Equal([false, true], headers.Select(x => x.ShowsCaptions));
		Assert.Equal(
			[("GDP", false), ("AAPL", true), ("MSFT", false)],
			rows.Select(x =>
				(x.Event.Symbol.Length > 0 ? x.Event.Symbol : x.Event.Company, x.ShowsCaptions)
			)
		);
	}

	[Fact]
	public async Task Today_shows_only_today()
	{
		AnswerWednesdayAndThursday();
		_calendar = TestUiThread.Invoke(() =>
		{
			var calendar = CreateCalendar();
			calendar.Range = CalendarRange.Today;
			return calendar;
		});

		await Loaded();

		Assert.True(Calendar.IsToday);
		Assert.All(_nasdaq.Requests, x => Assert.Equal(Wednesday, x.Day));
		Assert.Equal(["today 28", "past GDP", "now", "AAPL"], Lines());
	}

	[Fact]
	public async Task Next_week_has_no_now_line()
	{
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == new DateOnly(2026, 11, 3)
				? [Earnings("AAPL", day)]
				: [];
		_calendar = TestUiThread.Invoke(() =>
		{
			var calendar = CreateCalendar();
			calendar.Range = CalendarRange.NextWeek;
			return calendar;
		});

		await Loaded();

		Assert.Equal(["3", "AAPL"], Lines());
	}

	public void Dispose()
	{
		if (_calendar != null)
			TestUiThread.Invoke(_calendar.Dispose);
		GC.SuppressFinalize(this);
	}

	// Opens the widget; its first load (offline) has finished
	// Wednesday 28 October, 16:00 New York: AAPL reports after the close; Nasdaq lists no result
	// for today, Yahoo has it once it's out
	[Fact]
	public async Task Todays_earnings_rows_get_the_reported_eps_from_yahoo()
	{
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Wednesday
				? [Earnings("AAPL", Wednesday), Earnings("MSFT", Wednesday)]
				: [];
		_yahoo.Answer = (day, _) =>
			day == Wednesday
				? FakeYahooApiClient.Page(day, 1, 2, ("AAPL", 1.85m, 4.52m), ("msft", null, null))
				: new();

		await Loaded();
		await WaitFor(() => Row("AAPL").ActualText.Length > 0);

		var apple = TestUiThread.Invoke(() => Row("AAPL"));
		Assert.Equal("$1.85", apple.ActualText);
		Assert.Equal("+4.5%", apple.SurpriseText);
		Assert.True(apple.IsBeat);
		Assert.Contains("EPS from Yahoo Finance", apple.ToolTipText);
		Assert.Equal(string.Empty, TestUiThread.Invoke(() => Row("MSFT").ActualText));
	}

	[Fact]
	public async Task While_today_has_rows_without_a_result_it_looks_again_on_its_own()
	{
		var reported = false;
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Wednesday
				? [Earnings("AAPL", Wednesday)]
				: [];
		_yahoo.Answer = (day, _) =>
			day == Wednesday && reported
				? FakeYahooApiClient.Page(day, 1, 1, ("AAPL", 1.85m, 4.52m))
				: FakeYahooApiClient.Page(day, 1, 1, ("AAPL", null, null));

		await Loaded();
		await WaitFor(() => _yahoo.Requests.Count > 0);
		Assert.Equal(string.Empty, TestUiThread.Invoke(() => Row("AAPL").ActualText));
		var itemsBefore = TestUiThread.Invoke(() => Calendar.Items.ToList());

		// Out a few minutes later: the next look fills it in, in place (no reload)
		reported = true;
		_cache.Time.Advance(EarningsResultConstants.YahooInterval);
		await WaitFor(() => Row("AAPL").ActualText == "$1.85");
		Assert.Equal(itemsBefore, TestUiThread.Invoke(() => Calendar.Items.ToList()));

		// Nothing left to look for: no more requests
		var requests = _yahoo.Requests.Count;
		_cache.Time.Advance(EarningsResultConstants.YahooInterval * 3);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		await Task.Delay(50, TestContext.Current.CancellationToken);
		Assert.Equal(requests, _yahoo.Requests.Count);
	}

	// Wednesday 16:00 New York: AAPL's figure is already in Nasdaq's list, MSFT's at Yahoo; TSLA
	// reports while the widget is open
	private void AnswerEarningsWhileOpen(Func<bool> reported, Func<decimal?> nasdaqTsla)
	{
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Wednesday
				?
				[
					WithActual(Earnings("AAPL", Wednesday), 1.85m),
					Earnings("MSFT", Wednesday),
					WithActual(Earnings("TSLA", Wednesday), nasdaqTsla()),
				]
				: [];
		_yahoo.Answer = (day, _) =>
			day == Wednesday
				? FakeYahooApiClient.Page(
					day,
					1,
					2,
					("MSFT", 3.10m, 2.5m),
					("TSLA", reported() ? 0.52m : null, reported() ? 4m : null)
				)
				: new();
	}

	private static CalendarEventDto WithActual(CalendarEventDto item, decimal? eps)
	{
		item.EpsActual = eps;
		return item;
	}

	private bool Fresh(string symbol) => TestUiThread.Invoke(() => Row(symbol).IsActualFresh);

	[Fact]
	public async Task Figures_already_out_when_the_widget_opens_are_not_highlighted()
	{
		AnswerEarningsWhileOpen(() => true, () => null);

		await Loaded();
		await WaitFor(() => Row("TSLA").ActualText == "$0.52" && Row("MSFT").ActualText.Length > 0);

		Assert.False(Fresh("AAPL"));
		Assert.False(Fresh("MSFT"));
		Assert.False(Fresh("TSLA"));
	}

	[Fact]
	public async Task A_figure_coming_in_while_open_is_highlighted_for_five_minutes()
	{
		var reported = false;
		AnswerEarningsWhileOpen(() => reported, () => null);
		await Loaded();
		await WaitFor(() => _yahoo.Requests.Count > 0 && !Calendar.IsLoading);
		Assert.False(Fresh("TSLA"));

		// The next poll finds it
		reported = true;
		_cache.Time.Advance(EarningsResultConstants.YahooInterval);
		await WaitFor(() => Row("TSLA").ActualText == "$0.52");
		Assert.True(Fresh("TSLA"));
		Assert.False(Fresh("AAPL"));
		Assert.False(Fresh("MSFT"));

		// Still on just before the end, off after it
		_cache.Time.Advance(
			EarningsResultConstants.ActualHighlightDuration - TimeSpan.FromSeconds(1)
		);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		Assert.True(Fresh("TSLA"));
		_cache.Time.Advance(TimeSpan.FromSeconds(1));
		await WaitFor(() => !Row("TSLA").IsActualFresh);
	}

	[Fact]
	public async Task A_figure_changing_again_is_highlighted_again()
	{
		var reported = false;
		decimal? nasdaqTsla = null;
		AnswerEarningsWhileOpen(() => reported, () => nasdaqTsla);
		await Loaded();
		await WaitFor(() => _yahoo.Requests.Count > 0 && !Calendar.IsLoading);

		reported = true;
		_cache.Time.Advance(EarningsResultConstants.YahooInterval);
		await WaitFor(() => Row("TSLA").ActualText == "$0.52");
		_cache.Time.Advance(EarningsResultConstants.ActualHighlightDuration);
		await WaitFor(() => !Row("TSLA").IsActualFresh);

		// Nasdaq's own figure differs: a Refresh shows it, highlighted again
		nasdaqTsla = 0.55m;
		TestUiThread.Invoke(Calendar.Refresh);
		await WaitFor(() => !Calendar.IsLoading && Row("TSLA").ActualText == "$0.55");
		Assert.True(Fresh("TSLA"));
		Assert.False(Fresh("AAPL"));

		// The same figure on the next Refresh is no news
		_cache.Time.Advance(EarningsResultConstants.ActualHighlightDuration);
		TestUiThread.Invoke(Calendar.Refresh);
		await WaitFor(() => !Calendar.IsLoading && !Row("TSLA").IsActualFresh);
		Assert.Equal("$0.55", TestUiThread.Invoke(() => Row("TSLA").ActualText));
	}

	[Fact]
	public async Task When_yahoo_fails_the_nasdaq_rows_still_show()
	{
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Wednesday
				? [Earnings("AAPL", Wednesday)]
				: [];
		_yahoo.Answer = (_, _) => throw new YahooServiceException("Yahoo is down");

		await Loaded();
		await WaitFor(() => _yahoo.Requests.Count > 0);

		Assert.Single(
			TestUiThread.Invoke(() => Calendar.Items.OfType<CalendarEventRowViewModel>().ToList())
		);
		Assert.Null(TestUiThread.Invoke(() => Calendar.ErrorMessage));
		Assert.False(TestUiThread.Invoke(() => Calendar.HasNotice));
		Assert.Equal(string.Empty, TestUiThread.Invoke(() => Row("AAPL").ActualText));
	}

	private CalendarEventRowViewModel Row(string symbol) =>
		Calendar.Items.OfType<CalendarEventRowViewModel>().Single(x => x.Event.Symbol == symbol);

	private Task Loaded()
	{
		Open();
		return WaitFor(() =>
			!Calendar.IsLoading && (Calendar.StatusText != null || Calendar.HasNotice)
		);
	}

	private async Task Connect()
	{
		Open();
		_connection.SetStatus(ConnectionState.Connected, "U1");
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		await WaitFor(() =>
			Calendar.IsConnected
			&& _ibkr.WshMetaDataCalls > 0
			&& !Calendar.IsLoading
			&& (Calendar.StatusText != null || Calendar.HasNotice)
		);
	}

	private static async Task WaitFor(Func<bool> condition)
	{
		for (var i = 0; i < 200 && !TestUiThread.Invoke(condition); i++)
		{
			TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
			await Task.Delay(10, TestContext.Current.CancellationToken);
		}
		Assert.True(TestUiThread.Invoke(condition));
	}

	// Wednesday 28 October 2026 in New York, so this week runs 26 October – 1 November
	private CalendarViewModel CreateCalendar()
	{
		_cache.Time.SetUtcNow(new DateTimeOffset(2026, 10, 28, 20, 0, 0, TimeSpan.Zero));
		_settings = new SettingsManager(_cache.Settings, _ibkr);
		return new CalendarViewModel(
			new CalendarManager(
				[
					new WshCalendarSource(_ibkr, _cache.Time),
					new NasdaqCalendarSource(_nasdaq, _cache.Time),
				],
				_settings,
				_cache.Time
			),
			new EarningsResultManager(
				[new YahooEarningsResultSource(_yahoo, _cache.Time)],
				new MessageLogManager()
			),
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
			_connection,
			_links,
			new DisplayTimeStore(_settings, _cache.Time),
			_navigator,
			_cache.Time
		);
	}
}
