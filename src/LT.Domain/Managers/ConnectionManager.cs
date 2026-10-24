using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Connection;

namespace LT.Domain.Managers;

/// <summary>
/// Owns the single IB connection of the app, including heartbeat and auto-reconnect.
/// Registered as a singleton (see Program.cs).
/// </summary>
public class ConnectionManager(
	IIbkrApiClient ibkrApiClient,
	ISettingsRepository settingsRepository,
	IMessageLogManager messageLogManager,
	TimeProvider timeProvider
) : IConnectionManager
{
	// Serializes user connects/disconnects with the watchdog
	private readonly SemaphoreSlim _gate = new(1, 1);

	public event Action<ConnectionStatusDto>? StatusChanged;

	public ConnectionStatusDto Status
	{
		get;
		private set
		{
			field = value;
			StatusChanged?.Invoke(value);
		}
	} = new() { State = ConnectionState.Disconnected };

	public ConnectRequest GetLastConnectRequest()
	{
		var connection = settingsRepository.Get().Connection;
		return new ConnectRequest
		{
			Host = connection.Host,
			Port = connection.Port,
			ClientId = connection.ClientId,
		};
	}

	public async Task ConnectAsync(
		ConnectRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (ibkrApiClient.IsConnected)
				throw new LSCoreBadRequestException(
					ConnectionValidationCodes.CVC_005.GetValidationMessage()
				);

			var settings = settingsRepository.Get();
			settings.Connection.Host = request.Host;
			settings.Connection.Port = request.Port;
			settings.Connection.ClientId = request.ClientId;
			settingsRepository.Save(settings);

			var endpoint = ConnectionHelpers.FormatEndpoint(
				request.Host,
				request.Port,
				request.ClientId
			);
			Status = new ConnectionStatusDto
			{
				State = ConnectionState.Connecting,
				Endpoint = endpoint,
			};

			try
			{
				await ibkrApiClient.ConnectAsync(request, cancellationToken);
				ibkrApiClient.SetMarketDataType(settings.General.MarketDataType);
				var serverTime = await ibkrApiClient.GetServerTimeAsync(cancellationToken);
				Status = new ConnectionStatusDto
				{
					State = ConnectionState.Connected,
					Endpoint = endpoint,
					Accounts = ConnectionHelpers.ParseAccounts(ibkrApiClient.ManagedAccounts),
					ServerTime = serverTime,
				};
				messageLogManager.Add(
					MessageSeverity.Info,
					MessageLogConstants.SourceConnection,
					$"Connected to {endpoint}."
				);
			}
			catch (Exception ex)
			{
				await ibkrApiClient.DisconnectAsync();
				Status = new ConnectionStatusDto
				{
					State = ConnectionState.Failed,
					Endpoint = endpoint,
					Error = ex.Message,
				};
				messageLogManager.Add(
					MessageSeverity.Error,
					MessageLogConstants.SourceConnection,
					$"Connection to {endpoint} failed: {ex.Message}"
				);
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task DisconnectAsync()
	{
		await _gate.WaitAsync();
		try
		{
			var wasActive = Status.State is not ConnectionState.Disconnected;
			await ibkrApiClient.DisconnectAsync();
			Status = new ConnectionStatusDto { State = ConnectionState.Disconnected };
			if (wasActive)
				messageLogManager.Add(
					MessageSeverity.Info,
					MessageLogConstants.SourceConnection,
					"Disconnected."
				);
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task ShutdownAsync()
	{
		await _gate.WaitAsync();
		try
		{
			await ibkrApiClient.DisconnectAsync();
			Status = new ConnectionStatusDto { State = ConnectionState.Disconnected };
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task CheckHealthAsync(CancellationToken cancellationToken = default)
	{
		if (!await _gate.WaitAsync(0, cancellationToken))
			return;

		try
		{
			var settings = settingsRepository.Get();
			var request = new ConnectRequest
			{
				Host = settings.Connection.Host,
				Port = settings.Connection.Port,
				ClientId = settings.Connection.ClientId,
			};
			var endpoint = ConnectionHelpers.FormatEndpoint(
				request.Host,
				request.Port,
				request.ClientId
			);

			if (Status.State == ConnectionState.Connected)
			{
				if (ibkrApiClient.IsConnected)
				{
					using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
						cancellationToken
					);
					timeout.CancelAfter(ConnectionConstants.HeartbeatTimeout);
					try
					{
						await ibkrApiClient.GetServerTimeAsync(timeout.Token);
						return;
					}
					catch (Exception) when (!cancellationToken.IsCancellationRequested)
					{
						// heartbeat failed - treated as a lost connection below
					}
				}

				await ibkrApiClient.DisconnectAsync();
				messageLogManager.Add(
					MessageSeverity.Warning,
					MessageLogConstants.SourceConnection,
					$"Connection to {endpoint} lost."
				);

				Status = settings.General.AutoReconnect
					? new ConnectionStatusDto
					{
						State = ConnectionState.Reconnecting,
						Endpoint = endpoint,
						Error = "Connection lost.",
						NextAttemptAt =
							timeProvider.GetUtcNow() + ConnectionHelpers.GetReconnectDelay(0),
					}
					: new ConnectionStatusDto
					{
						State = ConnectionState.Failed,
						Endpoint = endpoint,
						Error = "Connection lost.",
					};
				return;
			}

			if (
				Status.State != ConnectionState.Reconnecting
				|| timeProvider.GetUtcNow() < Status.NextAttemptAt
			)
				return;

			try
			{
				await ibkrApiClient.ConnectAsync(request, cancellationToken);
				ibkrApiClient.SetMarketDataType(settings.General.MarketDataType);
				var serverTime = await ibkrApiClient.GetServerTimeAsync(cancellationToken);
				Status = new ConnectionStatusDto
				{
					State = ConnectionState.Connected,
					Endpoint = endpoint,
					Accounts = ConnectionHelpers.ParseAccounts(ibkrApiClient.ManagedAccounts),
					ServerTime = serverTime,
				};
				messageLogManager.Add(
					MessageSeverity.Info,
					MessageLogConstants.SourceConnection,
					$"Reconnected to {endpoint}."
				);
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				await ibkrApiClient.DisconnectAsync();
				var attempt = Status.ReconnectAttempt + 1;
				Status = new ConnectionStatusDto
				{
					State = ConnectionState.Reconnecting,
					Endpoint = endpoint,
					Error = ex.Message,
					ReconnectAttempt = attempt,
					NextAttemptAt =
						timeProvider.GetUtcNow() + ConnectionHelpers.GetReconnectDelay(attempt),
				};
			}
		}
		finally
		{
			_gate.Release();
		}
	}
}
