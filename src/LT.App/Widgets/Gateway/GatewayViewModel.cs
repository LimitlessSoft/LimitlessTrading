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

/// <summary>
/// Backs GatewayWindow: starts IB Gateway (the user logs in in Gateway's own window) and stops it.
/// </summary>
public partial class GatewayViewModel : ViewModelBase
{
	private readonly IGatewayManager _gatewayManager;

	public GatewayViewModel(IGatewayManager gatewayManager)
	{
		_gatewayManager = gatewayManager;

		var last = gatewayManager.GetLastStartRequest();
		GatewayPath = last.GatewayPath;
		GatewayVersion = last.GatewayVersion;

		Apply(gatewayManager.Status);
		gatewayManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
		_ = CheckInstallAsync();
	}

	[ObservableProperty]
	public partial string GatewayPath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string GatewayVersion { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool IsGatewayFound { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LocateCommand))]
	public partial bool IsLocating { get; set; }

	[ObservableProperty]
	public partial string GatewayStatusText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? InstallError { get; set; }

	public bool CanLocate => !IsLocating && CanEdit;

	/// <summary>On open: keep the saved folder if it still holds an install, else search.</summary>
	private async Task CheckInstallAsync()
	{
		var gateway = GatewayPath;
		var versions = await Task.Run(() =>
			TryResolve(() => _gatewayManager.ResolveGatewayPath(gateway))?.GatewayVersions
		);
		if (versions != null)
		{
			SetGatewayFound(gateway, versions);
			return;
		}
		await LocateAsync();
	}

	[RelayCommand(CanExecute = nameof(CanLocate))]
	private async Task LocateAsync()
	{
		IsLocating = true;
		InstallError = null;
		GatewayStatusText = "Searching…";
		try
		{
			var found = await Task.Run(_gatewayManager.LocateInstall);
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
		State is GatewayState.Starting or GatewayState.AwaitingLogin or GatewayState.Stopping;
	public bool IsFailed => State == GatewayState.Failed;
	public bool CanEdit => State is GatewayState.Stopped or GatewayState.Failed;
	public bool CanStop =>
		State is GatewayState.Running or GatewayState.AwaitingLogin or GatewayState.Starting;

	/// <summary>Stopping before the login finished closes Gateway's window: a cancel.</summary>
	public string StopText => State == GatewayState.Running ? "Log out" : "Cancel";

	partial void OnStateChanged(GatewayState value)
	{
		OnPropertyChanged(nameof(IsRunning));
		OnPropertyChanged(nameof(IsBusy));
		OnPropertyChanged(nameof(IsFailed));
		OnPropertyChanged(nameof(CanEdit));
		OnPropertyChanged(nameof(CanStop));
		OnPropertyChanged(nameof(StopText));
		OnPropertyChanged(nameof(CanLocate));
		LocateCommand.NotifyCanExecuteChanged();
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
					GatewayPath = GatewayPath.Trim(),
					GatewayVersion = GatewayVersion.Trim(),
				}
			);
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
			GatewayState.Starting => "Starting IB Gateway…",
			GatewayState.AwaitingLogin =>
				"Log in in the IB Gateway window (live or paper) — this app connects once you're in",
			GatewayState.Running when status.IsExternal =>
				$"Using a Gateway already running on port {status.ApiPort} — not started by this app",
			GatewayState.Running =>
				$"Gateway running — {status.TradingMode}, port {status.ApiPort}",
			GatewayState.Stopping => "Stopping Gateway…",
			GatewayState.Failed => "Gateway failed",
			_ when status.Error != null => "Gateway closed",
			_ => "Gateway stopped",
		};
		ErrorMessage = status.Error;
	}
}
