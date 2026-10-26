using LT.Contracts.Dtos.Connection;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using Microsoft.Extensions.Hosting;

namespace LT.App.Services;

/// <summary>
/// Feeds every executions answer of the trade journal to IFillAlertManager, and starts a new
/// session there whenever the connection comes up or goes down, so IB's replay of the day's
/// fills on connect stays silent. When one of the app's own orders reports a fill, the journal asks
/// for executions right away (coalesced) instead of at its next poll.
/// </summary>
public class FillAlertService(
	IConnectionManager connectionManager,
	ITradeJournalManager tradeJournalManager,
	IOrderManager orderManager,
	IFillAlertManager fillAlertManager
) : BackgroundService
{
	private bool? _connected;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		connectionManager.StatusChanged += Apply;
		tradeJournalManager.ExecutionsReceived += OnExecutions;
		orderManager.OrderFilled += OnOrderFilled;
		Apply(connectionManager.Status);
		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}
		catch (OperationCanceledException) { }
		finally
		{
			connectionManager.StatusChanged -= Apply;
			tradeJournalManager.ExecutionsReceived -= OnExecutions;
			orderManager.OrderFilled -= OnOrderFilled;
		}
	}

	private void Apply(ConnectionStatusDto status)
	{
		var connected = status.State == ConnectionState.Connected;
		if (_connected == connected)
			return;
		_connected = connected;
		fillAlertManager.BeginSession();
	}

	private void OnOrderFilled(OrderStatusDto status) =>
		tradeJournalManager.RequestExecutionsRefresh();

	private void OnExecutions(IReadOnlyList<ExecutionDto> executions) =>
		fillAlertManager.OnExecutions(executions);
}
