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
/// Owns the IB Gateway child process (via IBC) and connects the API to it once logged in.
/// Registered as a singleton (see Program.cs).
/// </summary>
public class GatewayManager : IGatewayManager
{
	private readonly IIbcGatewayProcess _gatewayProcess;
	private readonly IIbInstallProbe _installProbe;
	private readonly IIbcDownloader _ibcDownloader;
	private readonly IConnectionManager _connectionManager;
	private readonly ISettingsRepository _settingsRepository;
	private readonly TimeProvider _timeProvider;

	public GatewayManager(
		IIbcGatewayProcess gatewayProcess,
		IIbInstallProbe installProbe,
		IIbcDownloader ibcDownloader,
		IConnectionManager connectionManager,
		ISettingsRepository settingsRepository,
		TimeProvider timeProvider
	)
	{
		_gatewayProcess = gatewayProcess;
		_installProbe = installProbe;
		_ibcDownloader = ibcDownloader;
		_connectionManager = connectionManager;
		_settingsRepository = settingsRepository;
		_timeProvider = timeProvider;

		// Exits during start-up are handled by StartAsync; this catches a crash after login
		_gatewayProcess.Exited += exitCode =>
		{
			if (Status.State != GatewayState.Running)
				return;
			_gatewayProcess.DeleteConfig();
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
			Username = gateway.Username,
			TradingMode = gateway.TradingMode,
			IbcPath = gateway.IbcPath,
			GatewayPath = gateway.GatewayPath,
			GatewayVersion = gateway.GatewayVersion,
			UseVirtualDisplay = gateway.UseVirtualDisplay,
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

		var launch = IbcHelpers.BuildLaunch(
			request,
			AppPathHelpers.GetIbcConfigFilePath(),
			OperatingSystem.IsWindows(),
			OperatingSystem.IsLinux()
		);
		var script = IbcHelpers.GetIbcScriptPath(request.IbcPath, OperatingSystem.IsWindows());
		if (!_gatewayProcess.ScriptExists(script))
			throw new LSCoreBadRequestException(
				GatewayValidationCodes.GVC_012.GetValidationMessage(script)
			);

		// A Gateway already listening (e.g. left running when the app was killed): use it
		if (await AttachRunningAsync(request.TradingMode, cancellationToken))
			return;
		var apiPort = IbcHelpers.GetApiPort(request.TradingMode);

		var settings = _settingsRepository.Get();
		settings.Gateway.Username = request.Username;
		settings.Gateway.TradingMode = request.TradingMode;
		settings.Gateway.IbcPath = request.IbcPath;
		settings.Gateway.GatewayPath = request.GatewayPath;
		settings.Gateway.GatewayVersion = request.GatewayVersion;
		settings.Gateway.UseVirtualDisplay = request.UseVirtualDisplay;
		_settingsRepository.Save(settings);

		Status = new GatewayStatusDto
		{
			State = GatewayState.Starting,
			TradingMode = request.TradingMode,
			ApiPort = apiPort,
		};

		string? error = null;
		string? lastLine = null;
		Action<string> onOutput = line =>
		{
			if (!string.IsNullOrWhiteSpace(line))
				lastLine = line.Trim();
			switch (IbcHelpers.ParseOutputLine(line))
			{
				case IbcOutputEvent.SecondFactorRequested
					when Status.State == GatewayState.Starting:
					Status = new GatewayStatusDto
					{
						State = GatewayState.AwaitingSecondFactor,
						TradingMode = request.TradingMode,
						ApiPort = apiPort,
					};
					break;
				case IbcOutputEvent.LoginFailed:
					error ??= GatewayValidationCodes.GVC_015.GetValidationMessage();
					break;
			}
		};
		Action<int> onExited = exitCode =>
			error ??= GatewayValidationCodes.GVC_016.GetValidationMessage(exitCode, lastLine);

		_gatewayProcess.OutputReceived += onOutput;
		_gatewayProcess.Exited += onExited;
		try
		{
			_gatewayProcess.Start(launch);
			var deadline = _timeProvider.GetUtcNow() + GatewayConstants.StartTimeout;
			while (error == null)
			{
				if (await _gatewayProcess.IsPortOpenAsync(apiPort, cancellationToken))
					break;
				if (_timeProvider.GetUtcNow() > deadline)
					error = GatewayValidationCodes.GVC_014.GetValidationMessage(
						GatewayConstants.StartTimeout.TotalMinutes
					);
				else
					await Task.Delay(
						GatewayConstants.StartPollInterval,
						_timeProvider,
						cancellationToken
					);
			}
		}
		catch (Exception ex)
		{
			error ??= ex.Message;
		}
		finally
		{
			_gatewayProcess.OutputReceived -= onOutput;
			_gatewayProcess.Exited -= onExited;
			_gatewayProcess.DeleteConfig();
		}

		// StopAsync was called mid-start; it owns the final state
		if (Status.State is GatewayState.Stopping or GatewayState.Stopped)
			return;

		if (error != null)
		{
			await _gatewayProcess.StopAsync();
			Status = new GatewayStatusDto
			{
				State = GatewayState.Failed,
				TradingMode = request.TradingMode,
				ApiPort = apiPort,
				Error = error,
			};
			return;
		}

		Status = new GatewayStatusDto
		{
			State = GatewayState.Running,
			TradingMode = request.TradingMode,
			ApiPort = apiPort,
		};

		if (_connectionManager.Status.State == ConnectionState.Connected)
			return;
		await _connectionManager.ConnectAsync(
			new ConnectRequest
			{
				Host = ConnectionConstants.DefaultHost,
				Port = apiPort,
				ClientId = _connectionManager.GetLastConnectRequest().ClientId,
			},
			cancellationToken
		);
	}

	public async Task<bool> AttachRunningAsync(
		TradingMode tradingMode,
		CancellationToken cancellationToken = default
	)
	{
		if (_gatewayProcess.IsRunning || Status.IsExternal)
			return false;
		var apiPort = IbcHelpers.GetApiPort(tradingMode);
		if (!await _gatewayProcess.IsPortOpenAsync(apiPort, cancellationToken))
			return false;

		Status = new GatewayStatusDto
		{
			State = GatewayState.Running,
			TradingMode = tradingMode,
			ApiPort = apiPort,
			IsExternal = true,
		};
		if (_connectionManager.Status.State != ConnectionState.Connected)
			await _connectionManager.ConnectAsync(
				new ConnectRequest
				{
					Host = ConnectionConstants.DefaultHost,
					Port = apiPort,
					ClientId = _connectionManager.GetLastConnectRequest().ClientId,
				},
				cancellationToken
			);
		return true;
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
		_gatewayProcess.DeleteConfig();
		Status = new GatewayStatusDto { State = GatewayState.Stopped };
	}

	public GatewayInstallDto LocateInstall()
	{
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var isWindows = OperatingSystem.IsWindows();
		var gateway = IbcInstallHelpers.FindGateway(
			_installProbe,
			IbcInstallHelpers.GetGatewayCandidates(home, isWindows)
		);
		return new GatewayInstallDto
		{
			IbcPath = IbcInstallHelpers.FindIbcPath(
				_installProbe,
				IbcInstallHelpers.GetIbcCandidates(
					home,
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					isWindows
				),
				IbcInstallHelpers.GetIbcSearchRoots(home, isWindows),
				isWindows
			),
			GatewayPath = gateway?.GatewayPath,
			GatewayVersions = gateway?.GatewayVersions ?? [],
		};
	}

	public async Task<string> InstallIbcAsync(CancellationToken cancellationToken = default)
	{
		if (_gatewayProcess.IsRunning)
			throw new LSCoreBadRequestException(
				GatewayValidationCodes.GVC_011.GetValidationMessage()
			);

		var release = await _ibcDownloader.GetLatestReleaseAsync(cancellationToken);
		var asset =
			IbcInstallHelpers.SelectReleaseAsset(
				release.Assets,
				OperatingSystem.IsWindows(),
				OperatingSystem.IsMacOS()
			)
			?? throw new LSCoreBadRequestException(
				GatewayValidationCodes.GVC_020.GetValidationMessage(
					release.Version,
					IbcInstallHelpers.GetReleaseAssetPrefix(
						OperatingSystem.IsWindows(),
						OperatingSystem.IsMacOS()
					)
				)
			);

		var target = AppPathHelpers.GetIbcInstallDirectory();
		await _ibcDownloader.InstallAsync(asset, target, cancellationToken);
		return IbcInstallHelpers.ResolveIbcPath(_installProbe, target, OperatingSystem.IsWindows())
			?? throw new LSCoreBadRequestException(
				GatewayValidationCodes.GVC_021.GetValidationMessage(
					IbcInstallHelpers.GetIbcScriptName(OperatingSystem.IsWindows())
				)
			);
	}

	public string ResolveIbcPath(string selected) =>
		IbcInstallHelpers.ResolveIbcPath(_installProbe, selected, OperatingSystem.IsWindows())
		?? throw new LSCoreBadRequestException(
			GatewayValidationCodes.GVC_018.GetValidationMessage(
				selected,
				IbcInstallHelpers.GetIbcScriptName(OperatingSystem.IsWindows())
			)
		);

	public GatewayInstallDto ResolveGatewayPath(string selected) =>
		IbcInstallHelpers.ResolveGatewayPath(_installProbe, selected)
		?? throw new LSCoreBadRequestException(
			GatewayValidationCodes.GVC_019.GetValidationMessage(selected)
		);
}
