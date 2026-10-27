using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Domain.CalendarSources;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.CalendarSources;

public class NasdaqCalendarSourceTests
{
	private static readonly DateOnly Monday = new(2026, 10, 26);
	private static readonly IReadOnlyList<DateOnly> Week = CalendarHelpers.GetDays(
		Monday,
		Monday.AddDays(6)
	);

	private readonly FakeNasdaqApiClient _nasdaq = new();
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 28, 20, 0, 0, TimeSpan.Zero)
	);
	private readonly NasdaqCalendarSource _source;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public NasdaqCalendarSourceTests()
	{
		LSCoreContainer.EnsureInitialized();
		_source = new NasdaqCalendarSource(_nasdaq, _time);
	}

	private static CalendarSourceQueryDto Query(
		CalendarEventFilter filter = CalendarEventFilter.All,
		bool refresh = false
	) =>
		new()
		{
			Days = Week,
			Filter = filter,
			Refresh = refresh,
		};

	private static CalendarEventDto Event(
		DateOnly date,
		CalendarEventKind kind,
		string symbol = "AAPL"
	) =>
		new()
		{
			Date = date,
			Kind = kind,
			Symbol = symbol,
			Source = CalendarSource.Nasdaq,
		};

	[Fact]
	public async Task A_week_is_splits_once_then_each_day_with_weekends_only_for_economic_releases()
	{
		await _source.GetEventsAsync(Query(), Ct);

		Assert.Equal((NasdaqCalendar.Splits, Monday), _nasdaq.Requests[0]);
		Assert.Equal(18, _nasdaq.Requests.Count); // 1 splits + 5 × (earnings, dividends) + 7 economic
		Assert.DoesNotContain(
			_nasdaq.Requests,
			x =>
				x.Calendar is NasdaqCalendar.Earnings or NasdaqCalendar.Dividends
				&& x.Day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
		);
		Assert.Equal(7, _nasdaq.Requests.Count(x => x.Calendar == NasdaqCalendar.Economic));
	}

	[Fact]
	public async Task A_month_stays_within_a_bounded_number_of_requests()
	{
		var month = CalendarHelpers.GetDays(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

		await _source.GetEventsAsync(new CalendarSourceQueryDto { Days = month }, Ct);

		// 1 splits + 22 weekdays × 2 + 31 economic
		Assert.Equal(76, _nasdaq.Requests.Count);
	}

	[Theory]
	[InlineData(CalendarEventFilter.Earnings, new[] { NasdaqCalendar.Earnings })]
	[InlineData(CalendarEventFilter.Economic, new[] { NasdaqCalendar.Economic })]
	[InlineData(
		CalendarEventFilter.Other,
		new[] { NasdaqCalendar.Splits, NasdaqCalendar.Dividends }
	)]
	public async Task Only_the_calendars_the_filter_shows_are_asked_for(
		CalendarEventFilter filter,
		NasdaqCalendar[] calendars
	)
	{
		await _source.GetEventsAsync(Query(filter), Ct);

		Assert.Equal(
			calendars.Order(),
			_nasdaq.Requests.Select(x => x.Calendar).Distinct().Order()
		);
	}

	[Fact]
	public async Task Splits_outside_the_range_are_left_out()
	{
		_nasdaq.Answer = (calendar, _) =>
			calendar == NasdaqCalendar.Splits
				?
				[
					Event(Monday.AddDays(3), CalendarEventKind.Split, "SOXX"),
					Event(Monday.AddDays(14), CalendarEventKind.Split, "MMT"),
				]
				: [];

		var result = await _source.GetEventsAsync(Query(), Ct);

		Assert.Equal("SOXX", Assert.Single(result.Events).Symbol);
	}

	[Fact]
	public async Task Answers_come_from_memory_until_refreshed_or_stale()
	{
		await _source.GetEventsAsync(Query(), Ct);
		await _source.GetEventsAsync(Query(CalendarEventFilter.Earnings), Ct);
		Assert.Equal(18, _nasdaq.Requests.Count);

		await _source.GetEventsAsync(Query(refresh: true), Ct);
		Assert.Equal(36, _nasdaq.Requests.Count);

		_time.Advance(CalendarConstants.EventsTtl + TimeSpan.FromMinutes(1));
		await _source.GetEventsAsync(Query(), Ct);
		Assert.Equal(54, _nasdaq.Requests.Count);
	}

	[Fact]
	public async Task A_failed_request_ends_the_load_with_its_message()
	{
		var message = CalendarValidationCodes.CLVC_008.GetValidationMessage("HTTP 403 Forbidden");
		_nasdaq.Answer = (calendar, _) =>
			calendar == NasdaqCalendar.Dividends ? throw new NasdaqServiceException(message) : [];

		var result = await _source.GetEventsAsync(Query(), Ct);

		Assert.Equal(CalendarStatus.Failed, result.Status);
		Assert.Contains("403", result.Message);
		Assert.Empty(result.Events);
		// Stopped at the first failure: splits, Monday's earnings, Monday's dividends
		Assert.Equal(3, _nasdaq.Requests.Count);
	}

	[Fact]
	public async Task Events_are_filtered_by_kind()
	{
		_nasdaq.Answer = (calendar, day) =>
			calendar == NasdaqCalendar.Economic && day == Monday
				? [Event(day, CalendarEventKind.Economic, string.Empty)]
				: [];

		var earnings = await _source.GetEventsAsync(Query(CalendarEventFilter.Earnings), Ct);
		var all = await _source.GetEventsAsync(Query(), Ct);

		Assert.Empty(earnings.Events);
		Assert.Equal(CalendarEventKind.Economic, Assert.Single(all.Events).Kind);
	}
}
