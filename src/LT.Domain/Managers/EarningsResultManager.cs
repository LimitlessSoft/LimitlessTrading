using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IEarningsResultSources;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Calendar;

namespace LT.Domain.Managers;

/// <summary>
/// Asks every earnings result source in parallel (registered fastest first: SEC EDGAR, Yahoo
/// Finance, Nasdaq) and keeps the first result per symbol and day
/// (<see cref="EarningsResultHelpers.Merge"/>). A source that fails is left out and logged to
/// Messages, once until it fails differently or answers again, so a poll every 30 s doesn't
/// fill the log. Registered as a singleton.
/// </summary>
public class EarningsResultManager : IEarningsResultManager
{
	private readonly IReadOnlyList<IEarningsResultSource> _sources;
	private readonly IMessageLogManager _messageLogManager;
	private readonly Dictionary<IEarningsResultSource, string> _lastFailures = [];
	private readonly Lock _lock = new();

	public EarningsResultManager(
		IEnumerable<IEarningsResultSource> sources,
		IMessageLogManager messageLogManager
	)
	{
		_sources = [.. sources];
		_messageLogManager = messageLogManager;
	}

	public async Task<IReadOnlyList<EarningsResultDto>> GetResultsAsync(
		GetEarningsResultsRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		var wanted = request.Symbols.Select(EarningsResultHelpers.GetSymbolKey).ToHashSet();
		if (wanted.Count == 0)
			return [];
		var query = new EarningsResultQueryDto
		{
			Date = request.Date,
			Symbols = [.. wanted],
			Refresh = request.Refresh,
		};
		var answers = await Task.WhenAll(
			_sources.Select(x => AskAsync(x, query, cancellationToken))
		);
		return
		[
			.. EarningsResultHelpers
				.Merge(answers)
				.Where(x => wanted.Contains(EarningsResultHelpers.GetSymbolKey(x.Symbol))),
		];
	}

	public TimeSpan GetPollInterval(DateTimeOffset now) =>
		_sources.Count == 0
			? EarningsResultConstants.SecInterval
			: _sources.Min(x => x.GetRefreshInterval(now));

	private async Task<IReadOnlyList<EarningsResultDto>> AskAsync(
		IEarningsResultSource source,
		EarningsResultQueryDto query,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var results = await source.GetResultsAsync(query, cancellationToken);
			lock (_lock)
				_lastFailures.Remove(source);
			return results;
		}
		// A request timing out is a failure too (HttpClient throws it as a cancellation)
		catch (Exception ex)
			when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested
			)
		{
			bool changed;
			lock (_lock)
			{
				changed = !_lastFailures.TryGetValue(source, out var last) || last != ex.Message;
				_lastFailures[source] = ex.Message;
			}
			if (changed)
				_messageLogManager.Add(
					MessageSeverity.Warning,
					MessageLogConstants.SourceCalendar,
					CalendarValidationCodes.CLVC_015.GetValidationMessage(source.Name, ex.Message)
				);
			return [];
		}
	}
}
