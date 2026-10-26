using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Runs <see cref="IPriceAlertManager.WatchAsync"/> while the app is connected, so price alerts
/// fire whether or not a chart shows their contract; every (re)connect starts it again.
/// </summary>
public class PriceAlertService(
	IConnectionManager connectionManager,
	IPriceAlertManager priceAlertManager
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
				Stop();
		}
	}

	private void Apply(ConnectionStatusDto status)
	{
		lock (_lock)
		{
			var connected = status.State == ConnectionState.Connected;
			if (connected == (_session != null))
				return;
			if (!connected)
			{
				Stop();
				return;
			}
			_session = new CancellationTokenSource();
			var token = _session.Token;
			_ = Task.Run(() => priceAlertManager.WatchAsync(token), CancellationToken.None);
		}
	}

	// Under _lock. Cancelled, not disposed: the watcher may still be starting on its token
	private void Stop()
	{
		_session?.Cancel();
		_session = null;
	}
}
