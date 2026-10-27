using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Exceptions;
using LT.Contracts.Requests.Calendar;
using LT.Contracts.Requests.Settings;
using LT.Domain.CalendarSources;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// The calendar over its sources: IB's Wall Street Horizon (the WSH cases run with only it on) and
/// Nasdaq, merged, deduplicated, each one's failure reported without hiding the other's events.
/// </summary>
public class CalendarManagerTests
{
	private static readonly DateOnly Monday = new(2026, 10, 26);
	private static readonly DateOnly Thursday = new(2026, 10, 29);

	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeNasdaqApiClient _nasdaq = new();
	private readonly InMemorySettingsRepository _settingsRepository = new();
	private readonly SettingsManager _settings;

	// Wednesday 28 October 2026, 16:00 in New York
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 28, 20, 0, 0, TimeSpan.Zero)
	);
	private readonly CalendarManager _manager;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public CalendarManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_settings = new SettingsManager(_settingsRepository, _ibkr);
		UseSources(wsh: true, nasdaq: false);
		_manager = new CalendarManager(
			[new WshCalendarSource(_ibkr, _time), new NasdaqCalendarSource(_nasdaq, _time)],
			_settings,
			_time
		);
		_ibkr.WshMetaData =
			"""{"event_types":[{"tag":"wshe_ed","name":"Earnings Date"},{"tag":"wshe_div","name":"Dividend"}]}""";
	}

	private void UseSources(bool wsh, bool nasdaq)
	{
		_settingsRepository.Settings.General.CalendarWallStreetHorizon = wsh;
		_settingsRepository.Settings.General.CalendarNasdaq = nasdaq;
	}

	private static CalendarEventDto NasdaqEarnings(DateOnly date, string symbol) =>
		new()
		{
			Date = date,
			Kind = CalendarEventKind.Earnings,
			TypeName = "Earnings",
			Symbol = symbol,
			Company = symbol + " Inc.",
			Time = CalendarEventTime.AfterClose,
			Detail = "EPS est. $1.00",
			Source = CalendarSource.Nasdaq,
		};

	private static GetCalendarEventsRequest Request(
		CalendarRange range = CalendarRange.ThisWeek,
		CalendarEventFilter filter = CalendarEventFilter.All,
		bool refresh = false
	) =>
		new()
		{
			Range = range,
			Filter = filter,
			Refresh = refresh,
		};

	[Fact]
	public async Task Without_the_WSH_subscription_it_says_what_to_subscribe_to()
	{
		// IB's answer to every WSH request on the live account without the subscription
		_ibkr.WshException = new IbkrRequestException(10276, "News feed is not allowed.");

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.NotSubscribed, result.Status);
		Assert.Empty(result.Days);
		Assert.Contains(CalendarConstants.SubscriptionName, result.Message);
		Assert.Contains("10276", result.Message);
		Assert.Equal(1, _ibkr.WshMetaDataCalls);
		Assert.Empty(_ibkr.WshEventRequests);
	}

	[Fact]
	public async Task A_refusal_of_the_event_data_alone_is_also_not_subscribed()
	{
		_ibkr.WshEventsBy = _ => throw new IbkrRequestException(10276, "News feed is not allowed.");

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.NotSubscribed, result.Status);
		Assert.Single(_ibkr.WshEventRequests);
	}

	[Fact]
	public async Task Other_refusals_fail_with_IBs_code()
	{
		_ibkr.WshException = new IbkrRequestException(321, "Error validating request");

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Failed, result.Status);
		Assert.Contains("321", result.Message);
	}

	[Fact]
	public async Task Not_connected_fails_without_asking_IB()
	{
		_ibkr.IsConnected = false;

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Failed, result.Status);
		Assert.Contains("Not connected", result.Message);
		Assert.Equal(0, _ibkr.WshMetaDataCalls);
	}

	[Fact]
	public async Task Economic_releases_are_not_asked_of_WSH()
	{
		var result = await _manager.GetEventsAsync(
			Request(filter: CalendarEventFilter.Economic),
			Ct
		);

		Assert.Equal(CalendarStatus.Loaded, result.Status);
		Assert.Equal(0, _ibkr.WshMetaDataCalls);
	}

	[Fact]
	public async Task A_week_is_one_market_wide_request_per_day_after_the_metadata()
	{
		_ibkr.WshEventsBy = q =>
			q.StartDate == new DateOnly(2026, 10, 29)
				? """[{"event_type":"wshe_ed","ticker":"AAPL","company":"Apple Inc","earnings_time":"AMC"}]"""
				: "[]";

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Loaded, result.Status);
		Assert.Equal((Monday, Monday.AddDays(6)), (result.Start, result.End));
		Assert.Equal(7, _ibkr.WshEventRequests.Count);
		Assert.All(
			_ibkr.WshEventRequests,
			q =>
			{
				Assert.Equal(q.StartDate, q.EndDate);
				Assert.Equal(CalendarConstants.MaxEventsPerRequest, q.TotalLimit);
				Assert.Equal(
					"{\"country\":\"All\",\"limit\":100,\"wshe_ed\":\"true\",\"wshe_div\":\"true\"}",
					q.Filter
				);
			}
		);
		Assert.Equal(
			Enumerable.Range(0, 7).Select(Monday.AddDays),
			_ibkr.WshEventRequests.Select(x => x.StartDate)
		);
		var day = Assert.Single(result.Days);
		Assert.Equal(new DateOnly(2026, 10, 29), day.Date);
		Assert.Equal("AAPL", Assert.Single(day.Events).Symbol);
	}

	[Fact]
	public async Task Earnings_ask_for_earnings_only_and_drop_anything_else_IB_sends()
	{
		_ibkr.WshEventsBy = _ =>
			"""[{"event_type":"wshe_ed","ticker":"AAPL"},{"event_type":"wshe_div","ticker":"KO"}]""";

		var result = await _manager.GetEventsAsync(
			Request(filter: CalendarEventFilter.Earnings),
			Ct
		);

		Assert.All(_ibkr.WshEventRequests, q => Assert.DoesNotContain("wshe_div", q.Filter));
		Assert.All(result.Days, d => Assert.Equal("AAPL", Assert.Single(d.Events).Symbol));
	}

	[Fact]
	public async Task Days_come_from_memory_until_refreshed()
	{
		await _manager.GetEventsAsync(Request(), Ct);
		await _manager.GetEventsAsync(Request(), Ct);
		Assert.Equal(7, _ibkr.WshEventRequests.Count);

		await _manager.GetEventsAsync(Request(refresh: true), Ct);
		Assert.Equal(14, _ibkr.WshEventRequests.Count);

		_time.Advance(CalendarConstants.EventsTtl + TimeSpan.FromMinutes(1));
		await _manager.GetEventsAsync(Request(), Ct);
		Assert.Equal(21, _ibkr.WshEventRequests.Count);
	}

	[Fact]
	public async Task A_day_at_the_request_limit_is_marked_as_possibly_incomplete()
	{
		var full =
			"["
			+ string.Join(
				',',
				Enumerable
					.Range(0, CalendarConstants.MaxEventsPerRequest)
					.Select(i => $$"""{"event_type":"wshe_ed","ticker":"T{{i}}"}""")
			)
			+ "]";
		_ibkr.WshEventsBy = q => q.StartDate == Monday ? full : "[]";

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.True(Assert.Single(result.Days).IsTruncated);
	}

	[Fact]
	public async Task An_answer_it_cannot_read_is_reported_not_shown_as_empty()
	{
		_ibkr.WshEventsBy = _ => """{"error":"unexpected"}""";

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Unreadable, result.Status);
		Assert.Contains("unexpected", result.Message);
		Assert.Single(_ibkr.WshEventRequests);
	}

	[Fact]
	public async Task A_month_never_exceeds_the_day_cap()
	{
		await _manager.GetEventsAsync(Request(CalendarRange.ThisMonth), Ct);

		Assert.Equal(31, _ibkr.WshEventRequests.Count);
		Assert.True(_ibkr.WshEventRequests.Count <= CalendarConstants.MaxDaysPerLoad);
	}

	[Fact]
	public async Task By_default_only_Nasdaq_is_asked_and_IB_is_not_needed()
	{
		_settingsRepository.Settings.General = new();
		_ibkr.IsConnected = false;
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Thursday
				? [NasdaqEarnings(day, "AAPL")]
				: [];

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Loaded, result.Status);
		Assert.Equal([CalendarSource.Nasdaq], result.Sources);
		Assert.Empty(result.SourceErrors);
		Assert.Equal("AAPL", Assert.Single(Assert.Single(result.Days).Events).Symbol);
		Assert.Equal(0, _ibkr.WshMetaDataCalls);
	}

	[Fact]
	public async Task An_event_both_sources_report_is_shown_once_with_both_sources_facts()
	{
		UseSources(wsh: true, nasdaq: true);
		_ibkr.WshEventsBy = q =>
			q.StartDate == Thursday
				? """[{"event_type":"wshe_ed","ticker":"AAPL","conid":"265598"},{"event_type":"wshe_ed","ticker":"MSFT"}]"""
				: "[]";
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Thursday
				? [NasdaqEarnings(day, "AAPL"), NasdaqEarnings(day, "META")]
				: [];

		var result = await _manager.GetEventsAsync(Request(), Ct);

		var events = Assert.Single(result.Days).Events;
		Assert.Equal(["AAPL", "META", "MSFT"], events.Select(x => x.Symbol).Order());
		var aapl = events.Single(x => x.Symbol == "AAPL");
		// WSH's (registered first) with Nasdaq's time, company and estimate filled in
		Assert.Equal(
			(
				CalendarSource.WallStreetHorizon,
				265598,
				CalendarEventTime.AfterClose,
				"AAPL Inc.",
				"EPS est. $1.00"
			),
			(aapl.Source, aapl.ConId, aapl.Time, aapl.Company, aapl.Detail)
		);
		Assert.Equal([CalendarSource.WallStreetHorizon, CalendarSource.Nasdaq], result.Sources);
	}

	[Fact]
	public async Task Without_the_WSH_subscription_Nasdaqs_events_still_show_and_WSH_says_why()
	{
		UseSources(wsh: true, nasdaq: true);
		_ibkr.WshException = new IbkrRequestException(10276, "News feed is not allowed.");
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Earnings && day == Thursday
				? [NasdaqEarnings(day, "AAPL")]
				: [];

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Loaded, result.Status);
		Assert.Single(result.Days);
		var error = Assert.Single(result.SourceErrors);
		Assert.Equal(
			(CalendarSource.WallStreetHorizon, CalendarStatus.NotSubscribed),
			(error.Source, error.Status)
		);
		Assert.Contains("10276", error.Message);
	}

	[Fact]
	public async Task When_Nasdaq_fails_WSHs_events_still_show_and_Nasdaq_says_why()
	{
		UseSources(wsh: true, nasdaq: true);
		_ibkr.WshEventsBy = q =>
			q.StartDate == Thursday ? """[{"event_type":"wshe_ed","ticker":"AAPL"}]""" : "[]";
		_nasdaq.Answer = (_, _) =>
			throw new NasdaqServiceException("Nasdaq's calendar could not be loaded (HTTP 403).");

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Loaded, result.Status);
		Assert.Single(result.Days);
		var error = Assert.Single(result.SourceErrors);
		Assert.Equal(CalendarSource.Nasdaq, error.Source);
		Assert.Contains("403", error.Message);
	}

	[Fact]
	public async Task When_every_source_fails_each_says_why()
	{
		UseSources(wsh: true, nasdaq: true);
		_ibkr.IsConnected = false;
		_nasdaq.Answer = (_, _) =>
			throw new NasdaqServiceException("Nasdaq's calendar could not be loaded (HTTP 503).");

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Failed, result.Status);
		Assert.Empty(result.Days);
		Assert.Contains(
			$"{CalendarConstants.WallStreetHorizonSourceName}: Not connected",
			result.Message
		);
		Assert.Contains($"{CalendarConstants.NasdaqSourceName}: Nasdaq's calendar", result.Message);
	}

	[Fact]
	public async Task A_source_throwing_unexpectedly_is_that_sources_failure()
	{
		_settingsRepository.Settings.General = new() { CalendarWallStreetHorizon = true };
		var wsh = new FakeCalendarSource(CalendarSource.WallStreetHorizon)
		{
			Error = new InvalidOperationException("boom"),
		};
		var nasdaq = new FakeCalendarSource(CalendarSource.Nasdaq);
		var manager = new CalendarManager([wsh, nasdaq], _settings, _time);

		var result = await manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.Loaded, result.Status);
		Assert.Equal("boom", Assert.Single(result.SourceErrors).Message);
	}

	[Fact]
	public async Task With_every_source_off_it_says_where_to_turn_one_on()
	{
		UseSources(wsh: false, nasdaq: false);

		var result = await _manager.GetEventsAsync(Request(), Ct);

		Assert.Equal(CalendarStatus.NoSources, result.Status);
		Assert.Contains("Settings → Calendar", result.Message);
		Assert.Equal(0, _ibkr.WshMetaDataCalls);
		Assert.Empty(_nasdaq.Requests);
	}

	[Fact]
	public void Switching_a_source_in_Settings_is_announced_other_settings_are_not()
	{
		var raised = 0;
		_manager.SourcesChanged += () => raised++;
		var request = new SaveAppSettingsRequest
		{
			MarketDataType = MarketDataType.Live,
			HeartbeatSeconds = ConnectionConstants.DefaultHeartbeatSeconds,
			CalendarWallStreetHorizon = true,
			CalendarNasdaq = false,
		};

		_settings.SaveAppSettings(request);
		Assert.Equal(0, raised);

		request.CalendarNasdaq = true;
		_settings.SaveAppSettings(request);
		Assert.Equal(1, raised);
	}
}
