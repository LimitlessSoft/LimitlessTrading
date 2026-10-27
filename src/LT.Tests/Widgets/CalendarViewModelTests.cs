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

	private static CalendarEventDto Earnings(string symbol) =>
		new()
		{
			Date = Thursday,
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
		Open();
		TestUiThread.Invoke(() => Calendar.Filter = CalendarEventFilter.Economic);

		await Loaded();

		Assert.True(Calendar.IsEconomic);
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

	public void Dispose()
	{
		if (_calendar != null)
			TestUiThread.Invoke(_calendar.Dispose);
		GC.SuppressFinalize(this);
	}

	// Opens the widget; its first load (offline) has finished
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
			new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
			_connection,
			_links,
			new DisplayTimeStore(_settings, _cache.Time),
			_navigator
		);
	}
}
