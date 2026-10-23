using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
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
/// Owns the single IB connection of the app. Registered as a singleton (see Program.cs).
/// </summary>
public class ConnectionManager(IIbkrApiClient ibkrApiClient, ISettingsRepository settingsRepository)
	: IConnectionManager
{
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
			var serverTime = await ibkrApiClient.GetServerTimeAsync(cancellationToken);
			Status = new ConnectionStatusDto
			{
				State = ConnectionState.Connected,
				Endpoint = endpoint,
				Accounts = ConnectionHelpers.ParseAccounts(ibkrApiClient.ManagedAccounts),
				ServerTime = serverTime,
			};
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
		}
	}

	public async Task DisconnectAsync()
	{
		await ibkrApiClient.DisconnectAsync();
		Status = new ConnectionStatusDto { State = ConnectionState.Disconnected };
	}
}
