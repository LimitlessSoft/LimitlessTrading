using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.ICalendarSources;

namespace LT.Domain.CalendarSources;

/// <summary>
/// Corporate events from IB's Wall Street Horizon data (needs the WSH subscription and a
/// connection). A load asks for the metadata (WSH wants it first; it also names the event types),
/// then one day at a time, so a month is at most 32 requests in a row, never in parallel. Days
/// are kept in memory for <see cref="CalendarConstants.EventsTtl"/>. Registered as a singleton.
/// </summary>
public class WshCalendarSource(IIbkrApiClient ibkrApiClient, TimeProvider timeProvider)
	: ICalendarSource
{
	// One load at a time: WSH requests never overlap, and a second load reuses the first's days
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly Dictionary<
		(DateOnly Day, string Filter),
		(IReadOnlyList<CalendarEventDto> Events, DateTimeOffset FetchedAt)
	> _days = [];

	public CalendarSource Id => CalendarSource.WallStreetHorizon;

	public string Name => CalendarConstants.WallStreetHorizonSourceName;

	public bool IsAvailable => ibkrApiClient.IsConnected;

	public string UnavailableMessage => CalendarValidationCodes.CLVC_006.GetValidationMessage();

	public async Task<CalendarSourceResultDto> GetEventsAsync(
		CalendarSourceQueryDto query,
		CancellationToken cancellationToken = default
	)
	{
		// WSH lists corporate events only: nothing to ask for economic releases
		if (!CalendarHelpers.HasCorporateEvents(query.Filter) || query.Days.Count == 0)
			return new CalendarSourceResultDto { Status = CalendarStatus.Loaded };

		await _gate.WaitAsync(cancellationToken);
		var now = timeProvider.GetUtcNow();
		try
		{
			var types = CalendarHelpers.ParseEventTypes(
				await ibkrApiClient.GetWshMetaDataAsync(cancellationToken)
			);
			var filter = CalendarHelpers.BuildEventFilter(
				CalendarHelpers.GetRequestTags(query.Filter, types)
			);
			foreach (var key in _days.Keys.ToList())
				if (now - _days[key].FetchedAt > CalendarConstants.EventsTtl)
					_days.Remove(key);

			var events = new List<CalendarEventDto>();
			var truncated = new HashSet<DateOnly>();
			foreach (var day in query.Days)
			{
				if (query.Refresh || !_days.TryGetValue((day, filter), out var cached))
				{
					var json = await ibkrApiClient.GetWshEventDataAsync(
						new WshEventQueryDto
						{
							Filter = filter,
							StartDate = day,
							EndDate = day,
							TotalLimit = CalendarConstants.MaxEventsPerRequest,
						},
						cancellationToken
					);
					if (CalendarHelpers.ParseEvents(json, types, day) is not { } parsed)
						return CalendarSourceResultDto.Failure(
							CalendarStatus.Unreadable,
							CalendarValidationCodes.CLVC_005.GetValidationMessage(
								CalendarHelpers.GetSnippet(json)
							)
						);
					cached = (parsed, now);
					_days[(day, filter)] = cached;
				}
				events.AddRange(cached.Events);
				if (cached.Events.Count >= CalendarConstants.MaxEventsPerRequest)
					truncated.Add(day);
			}
			return new CalendarSourceResultDto
			{
				Status = CalendarStatus.Loaded,
				Events = [.. events.Where(x => CalendarHelpers.Matches(x.Kind, query.Filter))],
				TruncatedDays = truncated,
			};
		}
		catch (IbkrRequestException ex) when (CalendarHelpers.IsNotSubscribed(ex.Code))
		{
			return CalendarSourceResultDto.Failure(
				CalendarStatus.NotSubscribed,
				CalendarValidationCodes.CLVC_003.GetValidationMessage(
					ex.Code,
					ex.Message,
					CalendarConstants.SubscriptionName
				)
			);
		}
		catch (IbkrRequestException ex)
		{
			return CalendarSourceResultDto.Failure(
				CalendarStatus.Failed,
				CalendarValidationCodes.CLVC_004.GetValidationMessage(ex.Code, ex.Message)
			);
		}
		finally
		{
			_gate.Release();
		}
	}
}
