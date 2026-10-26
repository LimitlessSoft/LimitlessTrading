using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Flex;

namespace LT.Domain.Managers;

/// <summary>
/// Singleton (Program.cs), run by the FlexImportScheduler hosted service. One import at a time
/// (<c>_run</c>); each statement goes through <see cref="IFlexApiClient"/>, which paces requests
/// within IB's per-token limits. Activity statements always carry an explicit fd / td range:
/// <list type="number">
/// <item>First run (or another Activity query): the newest year, so recent history shows first.</item>
/// <item>Back-fill: the year before the oldest covered day, again and again, until a year comes
/// back empty or "not available" (1003), reaches an account's dateOpened, or
/// <see cref="FlexConstants.MaxBackfillChunks"/>. Progress is saved after every year.</item>
/// <item>Daily: from a week before the last covered day through yesterday, once per New York
/// day (retried an hour after a failure).</item>
/// <item>Trade Confirmation (the query's own period, "Today"): every
/// <see cref="FlexConstants.TradeConfirmInterval"/> and on <see cref="ImportNowAsync"/>.</item>
/// </list>
/// NAV, cash flows and transfers go to <see cref="IAccountPerformanceManager.ImportFlexHistory"/>, trades to
/// <see cref="ITradeJournalManager.ImportFlexTrades"/>; both merges are idempotent, so
/// overlapping ranges are harmless.
/// </summary>
public class FlexImportManager : IFlexImportManager
{
	private readonly IFlexApiClient _flexApiClient;
	private readonly ISettingsManager _settingsManager;
	private readonly IFlexImportStateRepository _repository;
	private readonly IAccountPerformanceManager _accountPerformanceManager;
	private readonly ITradeJournalManager _tradeJournalManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly TimeProvider _timeProvider;
	private readonly TimeZoneInfo _zone = FlexHelpers.GetStatementTimeZone();

	private readonly SemaphoreSlim _run = new(1, 1);
	private readonly Lock _lock = new();
	private FlexImportStateEntity? _state;
	private string? _activity;
	private TaskCompletionSource _wake = NewWake();

	public FlexImportManager(
		IFlexApiClient flexApiClient,
		ISettingsManager settingsManager,
		IFlexImportStateRepository repository,
		IAccountPerformanceManager accountPerformanceManager,
		ITradeJournalManager tradeJournalManager,
		IMessageLogManager messageLogManager,
		TimeProvider timeProvider
	)
	{
		_flexApiClient = flexApiClient;
		_settingsManager = settingsManager;
		_repository = repository;
		_accountPerformanceManager = accountPerformanceManager;
		_tradeJournalManager = tradeJournalManager;
		_messageLogManager = messageLogManager;
		_timeProvider = timeProvider;
		settingsManager.FlexSettingsChanged += OnSettingsChanged;
	}

	public event Action? StatusChanged;

	public FlexImportStatusDto GetStatus()
	{
		var settings = _settingsManager.GetFlexSettings();
		lock (_lock)
		{
			var state = GetState();
			return new FlexImportStatusDto
			{
				IsConfigured = settings.IsConfigured,
				HasTradeConfirmQuery = settings.TradeConfirmQueryId.Length > 0,
				IsRunning = _activity != null,
				Activity = _activity,
				LastActivityImport = state.LastActivitySuccess,
				LastTradeConfirmImport = state.LastTradeConfirmSuccess,
				CoveredFrom = state.CoveredFrom,
				CoveredTo = state.CoveredTo,
				BackfillComplete = state.BackfillComplete,
				LastError = state.LastError,
				LastErrorAt = state.LastErrorAt,
				ErrorNeedsUser = state.LastError != null && state.LastErrorNeedsUser,
				HasCashTransactionsSection = state.HadCashTransactionsSection,
				HasTransfersSection = state.HadTransfersSection,
			};
		}
	}

	public async Task RunAsync(CancellationToken stoppingToken)
	{
		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				TaskCompletionSource wake;
				lock (_lock)
					wake = _wake;

				await RunDueAsync(force: false, stoppingToken);

				var settings = _settingsManager.GetFlexSettings();
				TimeSpan delay;
				lock (_lock)
					delay = settings.IsConfigured
						? FlexImportHelpers.GetDelayUntilDue(
							GetState(),
							settings.TradeConfirmQueryId.Length > 0,
							_timeProvider.GetUtcNow(),
							_zone
						)
						: Timeout.InfiniteTimeSpan;
				// Never spin: something due "now" again waits a second
				if (delay != Timeout.InfiniteTimeSpan && delay < TimeSpan.FromSeconds(1))
					delay = TimeSpan.FromSeconds(1);

				using var sleep = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
				await Task.WhenAny(Task.Delay(delay, _timeProvider, sleep.Token), wake.Task);
				await sleep.CancelAsync();
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
	}

	public Task ImportNowAsync(CancellationToken cancellationToken = default) =>
		RunDueAsync(force: true, cancellationToken);

	private async Task RunDueAsync(bool force, CancellationToken cancellationToken)
	{
		await _run.WaitAsync(cancellationToken);
		try
		{
			var settings = _settingsManager.GetFlexSettings();
			if (!settings.IsConfigured)
				return;

			lock (_lock)
			{
				var state = GetState();
				// Another Activity query covers other dates: start over (merges are idempotent)
				if (state.ActivityQueryId != settings.ActivityQueryId)
				{
					_state = new FlexImportStateEntity
					{
						ActivityQueryId = settings.ActivityQueryId,
						LastTradeConfirmAttempt = state.LastTradeConfirmAttempt,
						LastTradeConfirmSuccess = state.LastTradeConfirmSuccess,
					};
					SaveState();
				}
			}

			await RunActivityAsync(settings, force, cancellationToken);

			bool tradeConfirmDue;
			lock (_lock)
				tradeConfirmDue =
					settings.TradeConfirmQueryId.Length > 0
					&& (
						force
						|| FlexImportHelpers.IsTradeConfirmDue(
							GetState(),
							_timeProvider.GetUtcNow()
						)
					);
			if (tradeConfirmDue)
				await RunTradeConfirmAsync(settings, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		// Disk or merge failures must not end the scheduler; they show like Flex errors
		catch (Exception ex) when (ex is not FlexServiceException)
		{
			Update(state => RecordError(state, ex.Message, false));
		}
		finally
		{
			SetActivity(null);
			_run.Release();
		}
	}

	private async Task RunActivityAsync(
		FlexSettingsDto settings,
		bool force,
		CancellationToken cancellationToken
	)
	{
		var now = _timeProvider.GetUtcNow();
		var today = TradeJournalHelpers.GetTradeDay(now, _zone);
		bool due;
		DateOnly? coveredTo;
		lock (_lock)
		{
			var state = GetState();
			coveredTo = state.CoveredTo;
			due =
				FlexImportHelpers.IsActivityDue(state, now, _zone)
				|| (
					force
					&& (
						!FlexImportHelpers.SucceededToday(state, now, _zone)
						|| FlexImportHelpers.ShouldRecheckCashSections(state, now)
					)
				);
		}

		if (due)
		{
			Update(state => state.LastActivityAttempt = now);
			var ranges = coveredTo is { } to
				? FlexImportHelpers.GetForwardRanges(to, today)
				: [FlexImportHelpers.GetInitialRange(today)];
			try
			{
				foreach (var (from, until) in ranges)
				{
					SetActivity(
						FlexImportHelpers.FormatRange("Importing account history", from, until)
					);
					var statement = await FetchAsync(
						settings,
						settings.ActivityQueryId,
						from,
						until,
						cancellationToken
					);
					ImportActivity(statement, from, until);
					Update(state => FlexImportHelpers.ExtendCoverage(state, from, until));
				}
				Update(state =>
				{
					state.LastActivitySuccess = _timeProvider.GetUtcNow();
					state.LastError = null;
					state.LastErrorAt = null;
				});
			}
			catch (FlexServiceException ex)
			{
				Update(state => RecordError(state, ex.Message, ex.NeedsUserAction));
				return;
			}
		}

		await BackfillAsync(settings, force, cancellationToken);
	}

	private async Task BackfillAsync(
		FlexSettingsDto settings,
		bool force,
		CancellationToken cancellationToken
	)
	{
		for (var chunk = 0; chunk < FlexConstants.MaxBackfillChunks; chunk++)
		{
			DateOnly coveredFrom;
			lock (_lock)
			{
				var state = GetState();
				if (
					state.CoveredFrom is not { } from
					|| state.BackfillComplete
					|| !(force || FlexImportHelpers.IsBackfillDue(state, _timeProvider.GetUtcNow()))
				)
					return;
				coveredFrom = from;
			}

			var (rangeFrom, rangeTo) = FlexImportHelpers.GetBackfillRange(coveredFrom);
			SetActivity(
				FlexImportHelpers.FormatRange("Back-filling account history", rangeFrom, rangeTo)
			);
			try
			{
				var statement = await FetchAsync(
					settings,
					settings.ActivityQueryId,
					rangeFrom,
					rangeTo,
					cancellationToken
				);
				ImportActivity(statement, rangeFrom, rangeTo);
				// The account's start is in this year: nothing older to fetch
				var reachedStart =
					statement.AccountsOpened.Count > 0
					&& statement.AccountsOpened.Values.All(x => x >= rangeFrom);
				Update(state =>
				{
					FlexImportHelpers.ExtendCoverage(state, rangeFrom, rangeTo);
					state.BackfillComplete = statement.IsEmpty || reachedStart;
				});
			}
			catch (FlexServiceException ex) when (ex.ErrorCode == 1003)
			{
				Update(state => state.BackfillComplete = true);
				return;
			}
			catch (FlexServiceException ex)
			{
				Update(state =>
				{
					state.LastActivityAttempt = _timeProvider.GetUtcNow();
					RecordError(state, ex.Message, ex.NeedsUserAction);
				});
				return;
			}
		}
		// The chunk limit: whatever is older stays out
		Update(state => state.BackfillComplete = true);
	}

	private async Task RunTradeConfirmAsync(
		FlexSettingsDto settings,
		CancellationToken cancellationToken
	)
	{
		Update(state => state.LastTradeConfirmAttempt = _timeProvider.GetUtcNow());
		SetActivity("Importing today's trades");
		try
		{
			var statement = await FetchAsync(
				settings,
				settings.TradeConfirmQueryId,
				null,
				null,
				cancellationToken
			);
			if (!statement.HasTradesSection)
				throw new FlexServiceException(
					FlexValidationCodes.FVC_019.GetValidationMessage(),
					needsUserAction: true
				);
			_tradeJournalManager.ImportFlexTrades(statement.Trades, statement.CancelledTradeIds);
			Update(state =>
			{
				state.LastTradeConfirmSuccess = _timeProvider.GetUtcNow();
				state.LastError = null;
				state.LastErrorAt = null;
			});
		}
		catch (FlexServiceException ex)
		{
			Update(state => RecordError(state, ex.Message, ex.NeedsUserAction));
		}
	}

	private Task<FlexStatementDto> FetchAsync(
		FlexSettingsDto settings,
		string queryId,
		DateOnly? from,
		DateOnly? to,
		CancellationToken cancellationToken
	) =>
		_flexApiClient.GetStatementAsync(
			new FlexStatementRequest
			{
				Token = settings.Token,
				QueryId = queryId,
				From = from,
				To = to,
			},
			cancellationToken
		);

	private void ImportActivity(FlexStatementDto statement, DateOnly from, DateOnly to)
	{
		// Without the NAV section only trades come in; say what to add to the query
		if (!statement.HasNavSection)
			throw new FlexServiceException(
				FlexValidationCodes.FVC_018.GetValidationMessage(),
				needsUserAction: true
			);
		_accountPerformanceManager.ImportFlexHistory(
			statement.Nav,
			statement.CashFlows,
			statement.Transfers
		);
		_tradeJournalManager.ImportFlexTrades(statement.Trades, statement.CancelledTradeIds);
		// A section new to the history: fetch every year again for its movements
		Update(state =>
			FlexImportHelpers.NoteCashSections(
				state,
				statement.HasCashTransactionsSection,
				statement.HasTransfersSection,
				from,
				to
			)
		);
	}

	private void OnSettingsChanged(FlexSettingsDto settings)
	{
		// A new token or query may fix the last error; the next run says
		Update(state =>
		{
			state.LastError = null;
			state.LastErrorAt = null;
			state.LastActivityAttempt = null;
			state.LastTradeConfirmAttempt = null;
		});
		TaskCompletionSource wake;
		lock (_lock)
		{
			wake = _wake;
			_wake = NewWake();
		}
		wake.TrySetResult();
	}

	private void RecordError(FlexImportStateEntity state, string message, bool needsUser)
	{
		state.LastErrorNeedsUser = needsUser;
		if (state.LastError != message)
			_messageLogManager.Add(
				MessageSeverity.Warning,
				MessageLogConstants.SourceFlex,
				$"Flex import: {message}"
			);
		state.LastError = message;
		state.LastErrorAt = _timeProvider.GetUtcNow();
	}

	private void Update(Action<FlexImportStateEntity> apply)
	{
		lock (_lock)
		{
			apply(GetState());
			SaveState();
		}
		StatusChanged?.Invoke();
	}

	private void SetActivity(string? activity)
	{
		lock (_lock)
		{
			if (_activity == activity)
				return;
			_activity = activity;
		}
		StatusChanged?.Invoke();
	}

	// Callers hold _lock
	private FlexImportStateEntity GetState() => _state ??= _repository.Get();

	// Callers hold _lock
	private void SaveState()
	{
		try
		{
			_repository.Save(GetState());
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// The import still worked; it only repeats after a restart
		}
	}

	private static TaskCompletionSource NewWake() =>
		new(TaskCreationOptions.RunContinuationsAsynchronously);
}
