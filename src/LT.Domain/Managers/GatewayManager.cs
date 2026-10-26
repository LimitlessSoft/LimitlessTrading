using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IProcesses;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.Connection;
using LT.Contracts.Requests.Gateway;

namespace LT.Domain.Managers;

/// <summary>
/// Starts IB Gateway, lets the user log in in its own window and connects the API once its port
/// opens. Registered as a singleton (see Program.cs).
/// </summary>
public class GatewayManager : IGatewayManager
{
	private readonly IGatewayProcess _gatewayProcess;
	private readonly IIbInstallProbe _installProbe;
	private readonly IConnectionManager _connectionManager;
	private readonly ISettingsRepository _settingsRepository;
	private readonly TimeProvider _timeProvider;

	public GatewayManager(
		IGatewayProcess gatewayProcess,
		IIbInstallProbe installProbe,
		IConnectionManager connectionManager,
		ISettingsRepository settingsRepository,
		TimeProvider timeProvider
	)
	{
		_gatewayProcess = gatewayProcess;
		_installProbe = installProbe;
		_connectionManager = connectionManager;
		_settingsRepository = settingsRepository;
		_timeProvider = timeProvider;

		// Exits during start-up are handled by StartAsync; this catches the Gateway closing after
		// login (window closed, crash)
		_gatewayProcess.Exited += exitCode =>
		{
			if (Status.State != GatewayState.Running || Status.IsExternal)
				return;
			Status = new GatewayStatusDto
			{
				State = GatewayState.Failed,
				Error = GatewayValidationCodes.GVC_017.GetValidationMessage(exitCode),
			};
			// Nothing left to reconnect to
			_ = _connectionManager.ShutdownAsync();
		};
	}

	public event Action<GatewayStatusDto>? StatusChanged;

	public GatewayStatusDto Status
	{
		get;
		private set
		{
			field = value;
			StatusChanged?.Invoke(value);
		}
	} = new() { State = GatewayState.Stopped };

	public StartGatewayRequest GetLastStartRequest()
	{
		var gateway = _settingsRepository.Get().Gateway;
		return new StartGatewayRequest
		{
			GatewayPath = gateway.GatewayPath,
			GatewayVersion = gateway.GatewayVersion,
		};
	}

	public async Task StartAsync(
		StartGatewayRequest request,
		CancellationToken cancellationToken = default
	)
	{
		request.Validate();
		if (_gatewayProcess.IsRunning || Status.IsExternal)
			throw new LSCoreBadRequestException(
				GatewayValidationCodes.GVC_011.GetValidationMessage()
			);

		var launcher =
			GatewayInstallHelpers.FindLauncher(
				_installProbe,
				request.GatewayPath,
				request.GatewayVersion,
				OperatingSystem.IsWindows()
			)
			?? throw new LSCoreBadRequestException(
				GatewayValidationCodes.GVC_022.GetValidationMessage(
					request.GatewayPath,
					request.GatewayVersion
				)
			);

		// A Gateway already listening (e.g. left running when the app was killed): use it
		if (await AttachRunningAsync(cancellationToken))
			return;

		var settings = _settingsRepository.Get();
		settings.Gateway.GatewayPath = request.GatewayPath;
		settings.Gateway.GatewayVersion = request.GatewayVersion;
		_settingsRepository.Save(settings);
		var ports = GatewayHelpers.GetApiPorts(settings.Gateway.TradingMode);

		Status = new GatewayStatusDto { State = GatewayState.Starting };

		int? exitCode = null;
		Action<int> onExited = code => exitCode ??= code;
		(TradingMode Mode, int Port)? opened = null;
		string? error = null;
		var cancelled = false;

		_gatewayProcess.Exited += onExited;
		try
		{
			_gatewayProcess.Start(launcher);
			Status = new GatewayStatusDto { State = GatewayState.AwaitingLogin };

			// The user types credentials, picks live / paper and approves 2FA in Gateway's
			// window; the API port of the chosen mode opens once that is done
			var deadline = _timeProvider.GetUtcNow() + GatewayConstants.StartTimeout;
			while (exitCode == null && Status.State == GatewayState.AwaitingLogin)
			{
				foreach (var port in ports)
					if (await _gatewayProcess.IsPortOpenAsync(port.Port, cancellationToken))
					{
						opened = port;
						break;
					}
				if (opened != null)
					break;
				if (_timeProvider.GetUtcNow() > deadline)
				{
					error = GatewayValidationCodes.GVC_014.GetValidationMessage(
						GatewayConstants.StartTimeout.TotalMinutes
					);
					break;
				}
				await Task.Delay(
					GatewayConstants.StartPollInterval,
					_timeProvider,
					cancellationToken
				);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			cancelled = true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
		}
		finally
		{
			_gatewayProcess.Exited -= onExited;
		}

		// StopAsync was called mid-start; it owns the final state
		if (Status.State is GatewayState.Stopping or GatewayState.Stopped)
			return;

		if (opened is not { } api)
		{
			// Closed by the user before logging in, cancelled, or timed out / failed to start
			await _gatewayProcess.StopAsync();
			Status =
				error != null
					? new GatewayStatusDto { State = GatewayState.Failed, Error = error }
					: new GatewayStatusDto
					{
						State = GatewayState.Stopped,
						Error =
							cancelled || exitCode is null or 0
								? null
								: GatewayValidationCodes.GVC_023.GetValidationMessage(exitCode),
					};
			return;
		}

		settings = _settingsRepository.Get();
		settings.Gateway.TradingMode = api.Mode;
		_settingsRepository.Save(settings);

		Status = new GatewayStatusDto
		{
			State = GatewayState.Running,
			TradingMode = api.Mode,
			ApiPort = api.Port,
		};
		if (_connectionManager.Status.State != ConnectionState.Connected)
			await _connectionManager.ConnectAsync(
				new ConnectRequest
				{
					Host = ConnectionConstants.DefaultHost,
					Port = api.Port,
					ClientId = _connectionManager.GetLastConnectRequest().ClientId,
				},
				cancellationToken
			);
	}

	public async Task<bool> AttachRunningAsync(CancellationToken cancellationToken = default)
	{
		if (_gatewayProcess.IsRunning || Status.IsExternal)
			return false;
		var preferred = _settingsRepository.Get().Gateway.TradingMode;
		foreach (var (mode, port) in GatewayHelpers.GetApiPorts(preferred))
		{
			if (!await _gatewayProcess.IsPortOpenAsync(port, cancellationToken))
				continue;

			Status = new GatewayStatusDto
			{
				State = GatewayState.Running,
				TradingMode = mode,
				ApiPort = port,
				IsExternal = true,
			};
			if (_connectionManager.Status.State != ConnectionState.Connected)
				await _connectionManager.ConnectAsync(
					new ConnectRequest
					{
						Host = ConnectionConstants.DefaultHost,
						Port = port,
						ClientId = _connectionManager.GetLastConnectRequest().ClientId,
					},
					cancellationToken
				);
			return true;
		}
		return false;
	}

	public async Task StopAsync()
	{
		if (Status.IsExternal)
		{
			// Not ours to stop: only let go of it
			if (_connectionManager.Status.State != ConnectionState.Disconnected)
				await _connectionManager.DisconnectAsync();
			Status = new GatewayStatusDto { State = GatewayState.Stopped };
			return;
		}

		if (!_gatewayProcess.IsRunning)
		{
			Status = new GatewayStatusDto { State = GatewayState.Stopped };
			return;
		}

		Status = new GatewayStatusDto
		{
			State = GatewayState.Stopping,
			TradingMode = Status.TradingMode,
			ApiPort = Status.ApiPort,
		};
		if (_connectionManager.Status.State != ConnectionState.Disconnected)
			await _connectionManager.DisconnectAsync();
		await _gatewayProcess.StopAsync();
		Status = new GatewayStatusDto { State = GatewayState.Stopped };
	}

	public GatewayInstallDto LocateInstall() =>
		GatewayInstallHelpers.FindGateway(
			_installProbe,
			GatewayInstallHelpers.GetGatewayCandidates(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				OperatingSystem.IsWindows()
			)
		) ?? new GatewayInstallDto();

	public GatewayInstallDto ResolveGatewayPath(string selected) =>
		GatewayInstallHelpers.ResolveGatewayPath(_installProbe, selected)
		?? throw new LSCoreBadRequestException(
			GatewayValidationCodes.GVC_019.GetValidationMessage(selected)
		);
}
