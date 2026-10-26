using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Performance;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

/// <summary>
/// Singleton (Program.cs): holds every recorded fill in memory (the journal is small) and the
/// curve points of the day being recorded, which are written at most every
/// <see cref="TradeJournalConstants.PnlSaveInterval"/>.
/// </summary>
public class TradeJournalManager(
	IIbkrApiClient ibkrApiClient,
	IAccountManager accountManager,
	ITradeJournalRepository repository,
	TimeProvider timeProvider
) : ITradeJournalManager
{
	private readonly Lock _lock = new();
	private readonly TimeZoneInfo _zone = TradeJournalHelpers.GetTradeDayTimeZone();
	private readonly Lazy<Dictionary<DateOnly, List<FillEntity>>> _fills = new(() =>
		repository.GetFillDays().ToDictionary(x => x, x => repository.GetFills(x).ToList())
	);
	private DateOnly? _pnlDay;
	private List<PnlSampleEntity> _pnlSamples = [];
	private DateTimeOffset _pnlSavedAt;
	private bool _pnlDirty;

	public event Action? FillsChanged;
	public event Action? PnlChanged;

	public string? ExecutionsError { get; private set; }

	public DateOnly Today => TradeJournalHelpers.GetTradeDay(timeProvider.GetUtcNow(), _zone);

	public async Task RecordAsync(CancellationToken cancellationToken)
	{
		var accounts = ConnectionHelpers
			.ParseAccounts(ibkrApiClient.ManagedAccounts)
			.Take(TradeJournalConstants.MaxPnlAccounts);
		var pnlStreams = accounts
			.Select(account =>
				Task.Run(
					async () =>
					{
						try
						{
							await foreach (
								var pnl in accountManager.SubscribePnlAsync(
									account,
									cancellationToken
								)
							)
								RecordPnl(account, pnl);
						}
						catch (OperationCanceledException) { }
						// A failed reqPnL only stops this account's curve until the next connect;
						// the Account widget shows the same error
						catch (Exception) { }
					},
					CancellationToken.None
				)
			)
			.ToList();

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await RecordExecutionsAsync(cancellationToken);
				await Task.Delay(
					AccountConstants.ExecutionsRefreshInterval,
					timeProvider,
					cancellationToken
				);
			}
		}
		catch (OperationCanceledException) { }

		await Task.WhenAll(pnlStreams);
		Flush();
	}

	public async Task<int> RecordExecutionsAsync(CancellationToken cancellationToken = default)
	{
		if (!ibkrApiClient.IsConnected)
			return 0;

		IReadOnlyList<ExecutionDto> executions;
		try
		{
			executions = await ibkrApiClient.GetExecutionsAsync(cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			var changedError = ExecutionsError != ex.Message;
			ExecutionsError = ex.Message;
			if (changedError)
				FillsChanged?.Invoke();
			return 0;
		}

		var now = timeProvider.GetUtcNow();
		var changed = 0;
		lock (_lock)
		{
			var days = new HashSet<DateOnly>();
			foreach (var execution in executions)
			{
				var fill = execution.ToMapped<ExecutionDto, FillEntity>();
				fill.Time = execution.Time ?? now;
				fill.RecordedAt = now;
				var day = TradeJournalHelpers.GetTradeDay(fill.Time, _zone);
				if (!_fills.Value.TryGetValue(day, out var dayFills))
					_fills.Value[day] = dayFills = [];
				if (!TradeJournalHelpers.MergeFill(dayFills, fill))
					continue;
				changed++;
				days.Add(day);
			}
			foreach (var day in days)
				repository.SaveFills(day, _fills.Value[day]);
		}

		var hadError = ExecutionsError != null;
		ExecutionsError = null;
		if (changed > 0 || hadError)
			FillsChanged?.Invoke();
		return changed;
	}

	public void RecordPnl(string account, AccountPnlDto pnl)
	{
		if (pnl.DailyPnl is not { } value)
			return;

		var now = timeProvider.GetUtcNow();
		var day = TradeJournalHelpers.GetTradeDay(now, _zone);
		lock (_lock)
		{
			if (_pnlDay != day)
			{
				if (_pnlDirty && _pnlDay is { } previous)
					repository.SavePnlSamples(previous, _pnlSamples);
				_pnlDay = day;
				_pnlSamples = repository.GetPnlSamples(day).ToList();
				_pnlSavedAt = now;
			}

			PnlCurveHelpers.AddSample(
				_pnlSamples,
				account,
				now,
				value,
				TradeJournalConstants.PnlSampleInterval
			);
			_pnlDirty = true;
			if (now - _pnlSavedAt >= TradeJournalConstants.PnlSaveInterval)
			{
				repository.SavePnlSamples(day, _pnlSamples);
				_pnlSavedAt = now;
				_pnlDirty = false;
			}
		}
		PnlChanged?.Invoke();
	}

	public void Flush()
	{
		lock (_lock)
		{
			if (!_pnlDirty || _pnlDay is not { } day)
				return;
			repository.SavePnlSamples(day, _pnlSamples);
			_pnlSavedAt = timeProvider.GetUtcNow();
			_pnlDirty = false;
		}
	}

	public IReadOnlyList<DateOnly> GetDays()
	{
		lock (_lock)
		{
			var days = new SortedSet<DateOnly>(_fills.Value.Keys) { Today };
			days.UnionWith(repository.GetPnlDays());
			if (_pnlDay is { } recording)
				days.Add(recording);
			return days.ToList();
		}
	}

	public IReadOnlyList<PnlSampleDto> GetPnlCurve(DateOnly day, string account)
	{
		lock (_lock)
		{
			var samples = day == _pnlDay ? _pnlSamples : repository.GetPnlSamples(day);
			return samples
				.Where(x => x.Account == account)
				.OrderBy(x => x.Time)
				.ToList()
				.ToMappedList<PnlSampleEntity, PnlSampleDto>();
		}
	}

	public DayPerformanceDto GetDayPerformance(DateOnly day, string? account)
	{
		List<ExecutionDto> fills;
		int fillCount;
		lock (_lock)
		{
			var recorded = _fills
				.Value.Where(x => x.Key <= day)
				.SelectMany(x => x.Value)
				.Where(x => account == null || x.Account == account)
				.ToList();
			fills = recorded.ToMappedList<FillEntity, ExecutionDto>();
			fillCount = _fills
				.Value.GetValueOrDefault(day, [])
				.Count(x => account == null || x.Account == account);
		}

		var trades = TradeMatchingHelpers.Match(fills);
		var closed = trades
			.Where(x =>
				x.CloseTime is { } close && TradeJournalHelpers.GetTradeDay(close, _zone) == day
			)
			.ToList();
		return new DayPerformanceDto
		{
			Day = day,
			Trades = closed,
			Stats = TradeStatsHelpers.Compute(closed),
			OpenTrades = trades.Count(x => !x.IsClosed),
			FillCount = fillCount,
		};
	}
}
