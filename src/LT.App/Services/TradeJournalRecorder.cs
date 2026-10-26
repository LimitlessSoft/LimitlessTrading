using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Runs ITradeJournalManager.RecordAsync for as long as the connection is up, whether or not the
/// Day trade performance widget is open, so fills and the P&amp;L curve are recorded all day.
/// </summary>
public class TradeJournalRecorder(
	IConnectionManager connectionManager,
	ITradeJournalManager tradeJournalManager
) : BackgroundService
{
	private readonly Lock _lock = new();
	private CancellationTokenSource? _session;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		connectionManager.StatusChanged += Apply;
		Apply(connectionManager.Status);
		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException) { }
		finally
		{
			connectionManager.StatusChanged -= Apply;
			lock (_lock)
				StopSession();
			tradeJournalManager.Flush();
		}
	}

	private void Apply(ConnectionStatusDto status)
	{
		var connected = status.State == ConnectionState.Connected;
		lock (_lock)
		{
			if (connected == (_session != null))
				return;
			StopSession();
			if (!connected)
				return;
			_session = new CancellationTokenSource();
			var token = _session.Token;
			_ = Task.Run(() => tradeJournalManager.RecordAsync(token), CancellationToken.None);
		}
	}

	private void StopSession()
	{
		_session?.Cancel();
		_session?.Dispose();
		_session = null;
	}
}
