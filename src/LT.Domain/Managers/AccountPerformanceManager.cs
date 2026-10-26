using LSCore.Mapper.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Account;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;

namespace LT.Domain.Managers;

/// <summary>
/// Records account performance while connected: listens to the shared account summary and
/// account P&amp;L streams of every managed account (the same IB subscriptions the Account widget
/// uses) and keeps one snapshot per account and trading day, the last values of the day winning.
/// Registered as a singleton (see Program.cs); the host runs <see cref="RecordAsync"/>.
/// </summary>
public class AccountPerformanceManager : IAccountPerformanceManager
{
	private readonly IConnectionManager _connectionManager;
	private readonly IAccountManager _accountManager;
	private readonly IAccountHistoryRepository _repository;
	private readonly IMessageLogManager _messageLogManager;
	private readonly TimeProvider _timeProvider;
	private readonly TimeZoneInfo _tradingDayZone =
		AccountPerformanceHelpers.GetTradingDayTimeZone();

	private readonly Lock _lock = new();
	private readonly Dictionary<string, AccountHistoryEntity> _histories = [];
	private readonly Dictionary<string, AccountSummaryDto> _latestSummaries = [];
	private readonly HashSet<string> _dirty = [];
	private CancellationTokenSource? _session;
	private IReadOnlyList<string> _sessionAccounts = [];

	public AccountPerformanceManager(
		IConnectionManager connectionManager,
		IAccountManager accountManager,
		IAccountHistoryRepository repository,
		IMessageLogManager messageLogManager,
		TimeProvider timeProvider
	)
	{
		_connectionManager = connectionManager;
		_accountManager = accountManager;
		_repository = repository;
		_messageLogManager = messageLogManager;
		_timeProvider = timeProvider;
	}

	public event Action<string>? HistoryChanged;

	public async Task RecordAsync(CancellationToken stoppingToken)
	{
		_connectionManager.StatusChanged += OnStatusChanged;
		try
		{
			OnStatusChanged(_connectionManager.Status);
			while (true)
			{
				await Task.Delay(
					AccountPerformanceConstants.SaveInterval,
					_timeProvider,
					stoppingToken
				);
				Save();
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
		finally
		{
			_connectionManager.StatusChanged -= OnStatusChanged;
			StopSession();
			Save();
		}
	}

	public IReadOnlyList<AccountDaySnapshotDto> GetHistory(string account)
	{
		lock (_lock)
			return GetHistoryEntity(account)
				.Days.ToMappedList<AccountDaySnapshotEntity, AccountDaySnapshotDto>();
	}

	public DateOnly GetToday() =>
		AccountPerformanceHelpers.GetTradingDate(_timeProvider.GetUtcNow(), _tradingDayZone);

	private void OnStatusChanged(ConnectionStatusDto status)
	{
		if (status.State != ConnectionState.Connected)
		{
			StopSession();
			Save();
			return;
		}

		CancellationToken token;
		IReadOnlyList<string> accounts;
		lock (_lock)
		{
			if (_session != null && _sessionAccounts.SequenceEqual(status.Accounts))
				return;
			_session?.Cancel();
			_session = new CancellationTokenSource();
			_sessionAccounts = status.Accounts.ToList();
			token = _session.Token;
			accounts = _sessionAccounts;
		}

		foreach (var account in accounts)
		{
			_ = RecordSummaryAsync(account, token);
			_ = RecordPnlAsync(account, token);
		}
	}

	private void StopSession()
	{
		lock (_lock)
		{
			_session?.Cancel();
			_session = null;
			_sessionAccounts = [];
			_latestSummaries.Clear();
		}
	}

	private async Task RecordSummaryAsync(string account, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (
				var summary in _accountManager.SubscribeSummaryAsync(account, cancellationToken)
			)
				Update(
					account,
					day =>
					{
						_latestSummaries[account] = summary;
						AccountPerformanceHelpers.ApplySummary(day, summary);
					}
				);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
		// A dropped connection ends the stream too; that is not worth a message
		catch (Exception ex) when (_connectionManager.Status.State == ConnectionState.Connected)
		{
			LogError(account, ex);
		}
		catch (Exception) { }
	}

	private async Task RecordPnlAsync(string account, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var pnl in _accountManager.SubscribePnlAsync(account, cancellationToken))
				Update(account, day => AccountPerformanceHelpers.ApplyPnl(day, pnl));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
		// A dropped connection ends the stream too; that is not worth a message
		catch (Exception ex) when (_connectionManager.Status.State == ConnectionState.Connected)
		{
			LogError(account, ex);
		}
		catch (Exception) { }
	}

	private void Update(string account, Action<AccountDaySnapshotEntity> apply)
	{
		var now = _timeProvider.GetUtcNow();
		var date = AccountPerformanceHelpers.GetTradingDate(now, _tradingDayZone);
		lock (_lock)
		{
			var day = AccountPerformanceHelpers.GetOrAddDay(
				GetHistoryEntity(account).Days,
				date,
				now,
				_latestSummaries.GetValueOrDefault(account)
			);
			apply(day);
			day.LastUpdate = now;
			_dirty.Add(account);
		}
		HistoryChanged?.Invoke(account);
	}

	private void Save()
	{
		lock (_lock)
		{
			foreach (var account in _dirty)
			{
				try
				{
					_repository.Save(_histories[account]);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					LogError(account, ex);
				}
			}
			_dirty.Clear();
		}
	}

	// Callers hold _lock
	private AccountHistoryEntity GetHistoryEntity(string account)
	{
		if (_histories.TryGetValue(account, out var history))
			return history;

		history = _repository.Get(account);
		history.Account = account;
		history.Days.Sort((a, b) => a.Date.CompareTo(b.Date));
		_histories[account] = history;
		return history;
	}

	private void LogError(string account, Exception ex) =>
		_messageLogManager.Add(
			MessageSeverity.Warning,
			MessageLogConstants.SourceAccount,
			$"Performance recording ({account}): {ex.Message}"
		);
}
