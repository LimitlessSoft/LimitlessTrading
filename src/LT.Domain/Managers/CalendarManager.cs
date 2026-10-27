using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.ICalendarSources;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Calendar;

namespace LT.Domain.Managers;

/// <summary>
/// Merges the calendar sources switched on in Settings → Calendar (registered order: IB's Wall
/// Street Horizon, then Nasdaq). Each source paces and caches its own requests; they are asked in
/// parallel, since they talk to different services. A source that fails or isn't available is
/// reported next to the others' events; only when none answered does the result carry a status
/// instead of days. Registered as a singleton.
/// </summary>
public class CalendarManager : ICalendarManager
{
	private readonly IReadOnlyList<ICalendarSource> _sources;
	private readonly ISettingsManager _settingsManager;
	private readonly TimeProvider _timeProvider;
	private IReadOnlyList<CalendarSource> _enabled;

	public CalendarManager(
		IEnumerable<ICalendarSource> sources,
		ISettingsManager settingsManager,
		TimeProvider timeProvider
	)
	{
		_sources = [.. sources];
		_settingsManager = settingsManager;
		_timeProvider = timeProvider;
		_enabled = CalendarHelpers.GetEnabledSources(settingsManager.GetAppSettings());
		settingsManager.AppSettingsChanged += OnAppSettingsChanged;
	}

	public event Action? SourcesChanged;

	public async Task<CalendarResultDto> GetEventsAsync(
		GetCalendarEventsRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		var now = _timeProvider.GetUtcNow();
		var (start, end) = CalendarHelpers.GetDates(request.Range, CalendarHelpers.GetToday(now));
		var settings = _settingsManager.GetAppSettings();
		var enabled = _sources.Where(x => CalendarHelpers.IsEnabled(x.Id, settings)).ToList();
		if (enabled.Count == 0)
			return new CalendarResultDto
			{
				Status = CalendarStatus.NoSources,
				Start = start,
				End = end,
				Message = CalendarValidationCodes.CLVC_007.GetValidationMessage(),
				FetchedAt = now,
			};

		var query = new CalendarSourceQueryDto
		{
			Days = CalendarHelpers.GetDays(start, end),
			Filter = request.Filter,
			Refresh = request.Refresh,
		};
		var results = await Task.WhenAll(
			enabled.Select(x => AskAsync(x, query, cancellationToken))
		);

		var loaded = results.Where(x => x.Result.Status == CalendarStatus.Loaded).ToList();
		var errors = results
			.Where(x => x.Result.Status != CalendarStatus.Loaded)
			.Select(x => new CalendarSourceErrorDto
			{
				Source = x.Source.Id,
				Status = x.Result.Status,
				Message = x.Result.Message ?? string.Empty,
			})
			.ToList();
		if (loaded.Count == 0)
			return new CalendarResultDto
			{
				// One source: its own status (no WSH subscription gets its notice)
				Status = errors.Count == 1 ? errors[0].Status : CalendarStatus.Failed,
				Start = start,
				End = end,
				Message =
					errors.Count == 1
						? errors[0].Message
						: string.Join(
							Environment.NewLine,
							errors.Select(x =>
								$"{CalendarHelpers.GetSourceName(x.Source)}: {x.Message}"
							)
						),
				FetchedAt = now,
				SourceErrors = errors,
			};

		return new CalendarResultDto
		{
			Status = CalendarStatus.Loaded,
			Start = start,
			End = end,
			Days = CalendarHelpers.GroupByDay(
				loaded.SelectMany(x => x.Result.Events),
				loaded.SelectMany(x => x.Result.TruncatedDays).ToHashSet()
			),
			FetchedAt = now,
			Sources = [.. loaded.Select(x => x.Source.Id)],
			SourceErrors = errors,
		};
	}

	// A source's answer; not available or an unexpected error is that source's failure
	private static async Task<(ICalendarSource Source, CalendarSourceResultDto Result)> AskAsync(
		ICalendarSource source,
		CalendarSourceQueryDto query,
		CancellationToken cancellationToken
	)
	{
		if (!source.IsAvailable)
			return (
				source,
				CalendarSourceResultDto.Failure(CalendarStatus.Failed, source.UnavailableMessage)
			);
		try
		{
			return (source, await source.GetEventsAsync(query, cancellationToken));
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return (source, CalendarSourceResultDto.Failure(CalendarStatus.Failed, ex.Message));
		}
	}

	private void OnAppSettingsChanged(AppSettingsDto settings)
	{
		var enabled = CalendarHelpers.GetEnabledSources(settings);
		if (enabled.SequenceEqual(_enabled))
			return;
		_enabled = enabled;
		SourcesChanged?.Invoke();
	}
}
