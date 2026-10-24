using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Gateway;

namespace LT.App.Widgets.Gateway;

/// <summary>Backs GatewayWindow: starts / stops IB Gateway through IBC with the typed credentials.</summary>
public partial class GatewayViewModel : ViewModelBase
{
	private readonly IGatewayManager _gatewayManager;

	public GatewayViewModel(IGatewayManager gatewayManager)
	{
		_gatewayManager = gatewayManager;

		var last = gatewayManager.GetLastStartRequest();
		Username = last.Username;
		TradingMode = last.TradingMode;
		IbcPath = last.IbcPath;
		GatewayPath = last.GatewayPath;
		GatewayVersion = last.GatewayVersion;
		UseVirtualDisplay = last.UseVirtualDisplay;

		Apply(gatewayManager.Status);
		gatewayManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
		_ = CheckInstallAsync();
	}

	public IReadOnlyList<TradingMode> TradingModes { get; } = Enum.GetValues<TradingMode>();

	public bool IsLinux => OperatingSystem.IsLinux();

	[ObservableProperty]
	public partial string Username { get; set; } = string.Empty;

	/// <summary>Kept only until the start attempt finishes; never persisted.</summary>
	[ObservableProperty]
	public partial string Password { get; set; } = string.Empty;

	[ObservableProperty]
	public partial TradingMode TradingMode { get; set; }

	[ObservableProperty]
	public partial string IbcPath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string GatewayPath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string GatewayVersion { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool UseVirtualDisplay { get; set; }

	[ObservableProperty]
	public partial bool IsIbcFound { get; set; }

	[ObservableProperty]
	public partial bool IsGatewayFound { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LocateCommand), nameof(InstallIbcCommand))]
	public partial bool IsLocating { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LocateCommand), nameof(InstallIbcCommand))]
	public partial bool IsInstallingIbc { get; set; }

	[ObservableProperty]
	public partial string IbcStatusText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string GatewayStatusText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? InstallError { get; set; }

	public bool IsInstallReady => IsIbcFound && IsGatewayFound;

	public bool CanLocate => !IsLocating && !IsInstallingIbc && CanEdit;

	partial void OnIsIbcFoundChanged(bool value) => OnPropertyChanged(nameof(IsInstallReady));

	partial void OnIsGatewayFoundChanged(bool value) => OnPropertyChanged(nameof(IsInstallReady));

	/// <summary>On open: keep the saved folders if they still hold an install, else search.</summary>
	private async Task CheckInstallAsync()
	{
		var ibc = IbcPath;
		var gateway = GatewayPath;
		var (ibcOk, gatewayVersions) = await Task.Run(() =>
			(
				TryResolve(() => _gatewayManager.ResolveIbcPath(ibc)) != null,
				TryResolve(() => _gatewayManager.ResolveGatewayPath(gateway))?.GatewayVersions
			)
		);
		if (ibcOk && gatewayVersions != null)
		{
			SetIbcFound(ibc);
			SetGatewayFound(gateway, gatewayVersions);
			return;
		}
		await LocateAsync();
	}

	[RelayCommand(CanExecute = nameof(CanLocate))]
	private async Task LocateAsync()
	{
		IsLocating = true;
		InstallError = null;
		IbcStatusText = GatewayStatusText = "Searching…";
		try
		{
			var found = await Task.Run(_gatewayManager.LocateInstall);
			if (found.IbcPath is { } ibc)
				SetIbcFound(ibc);
			else
				SetIbcMissing();
			if (found.GatewayPath is { } gateway)
				SetGatewayFound(gateway, found.GatewayVersions);
			else
				SetGatewayMissing();
		}
		catch (Exception ex)
		{
			InstallError = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			IsLocating = false;
		}
	}

	/// <summary>Downloads IBC from GitHub into the app-data folder and uses it.</summary>
	[RelayCommand(CanExecute = nameof(CanLocate))]
	private async Task InstallIbcAsync()
	{
		IsInstallingIbc = true;
		InstallError = null;
		IbcStatusText = "Downloading IBC from GitHub…";
		try
		{
			SetIbcFound(await _gatewayManager.InstallIbcAsync());
		}
		catch (Exception ex)
		{
			SetIbcMissing();
			InstallError = $"Installing IBC failed: {ErrorMessageHelpers.ToUserMessage(ex)}";
		}
		finally
		{
			IsInstallingIbc = false;
		}
	}

	/// <summary>A folder picked in the file dialog (IBC folder, its scripts folder, …).</summary>
	public void UseIbcFolder(string path)
	{
		InstallError = null;
		try
		{
			SetIbcFound(_gatewayManager.ResolveIbcPath(path));
		}
		catch (Exception ex)
		{
			InstallError = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	/// <summary>A folder picked in the file dialog (Jts, ibgateway, ibgateway/1037, …).</summary>
	public void UseGatewayFolder(string path)
	{
		InstallError = null;
		try
		{
			var found = _gatewayManager.ResolveGatewayPath(path);
			SetGatewayFound(found.GatewayPath!, found.GatewayVersions);
		}
		catch (Exception ex)
		{
			InstallError = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	private void SetIbcFound(string path)
	{
		IbcPath = path;
		IsIbcFound = true;
		IbcStatusText = $"IBC found in {path}";
	}

	private void SetIbcMissing()
	{
		IsIbcFound = false;
		IbcStatusText = "IBC not found — Install IBC, or Browse… to its folder";
	}

	private void SetGatewayFound(string path, IReadOnlyList<string> versions)
	{
		GatewayPath = path;
		IsGatewayFound = true;
		if (!versions.Contains(GatewayVersion))
			GatewayVersion = versions[0];
		GatewayStatusText = $"Gateway found in {path} (versions {string.Join(", ", versions)})";
	}

	private void SetGatewayMissing()
	{
		IsGatewayFound = false;
		GatewayStatusText =
			"IB Gateway (offline installer) not found — install it, or Browse… to its folder";
	}

	private static T? TryResolve<T>(Func<T> resolve)
		where T : class
	{
		try
		{
			return resolve();
		}
		catch (Exception)
		{
			return null;
		}
	}

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
	public partial GatewayState State { get; set; }

	[ObservableProperty]
	public partial string StateText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	public bool IsRunning => State == GatewayState.Running;
	public bool IsBusy =>
		State
			is GatewayState.Starting
				or GatewayState.AwaitingSecondFactor
				or GatewayState.Stopping;
	public bool IsFailed => State == GatewayState.Failed;
	public bool CanEdit => State is GatewayState.Stopped or GatewayState.Failed;
	public bool CanStop =>
		State is GatewayState.Running or GatewayState.AwaitingSecondFactor or GatewayState.Starting;

	partial void OnStateChanged(GatewayState value)
	{
		OnPropertyChanged(nameof(IsRunning));
		OnPropertyChanged(nameof(IsBusy));
		OnPropertyChanged(nameof(IsFailed));
		OnPropertyChanged(nameof(CanEdit));
		OnPropertyChanged(nameof(CanStop));
		OnPropertyChanged(nameof(CanLocate));
		LocateCommand.NotifyCanExecuteChanged();
		InstallIbcCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = nameof(CanEdit))]
	private async Task StartAsync()
	{
		ErrorMessage = null;
		try
		{
			await _gatewayManager.StartAsync(
				new StartGatewayRequest
				{
					Username = Username.Trim(),
					Password = Password,
					TradingMode = TradingMode,
					IbcPath = IbcPath.Trim(),
					GatewayPath = GatewayPath.Trim(),
					GatewayVersion = GatewayVersion.Trim(),
					UseVirtualDisplay = UseVirtualDisplay,
				}
			);
			Password = string.Empty;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	[RelayCommand(CanExecute = nameof(CanStop))]
	private Task StopAsync() => _gatewayManager.StopAsync();

	private void Apply(GatewayStatusDto status)
	{
		State = status.State;
		StateText = status.State switch
		{
			GatewayState.Starting => $"Starting Gateway ({status.TradingMode})…",
			GatewayState.AwaitingSecondFactor => "Approve the login in IB Key on your phone…",
			GatewayState.Running =>
				$"Gateway running — {status.TradingMode}, port {status.ApiPort}",
			GatewayState.Stopping => "Stopping Gateway…",
			GatewayState.Failed => "Gateway failed",
			_ => "Gateway stopped",
		};
		ErrorMessage = status.Error;
		if (status.State is GatewayState.Failed)
			Password = string.Empty;
	}
}
