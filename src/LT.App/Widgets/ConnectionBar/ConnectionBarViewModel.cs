using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.ConnectionBar;

/// <summary>
/// Status-bar item for the IB Gateway session + API connection; clicking it opens the log-in
/// dialog. There is no manual endpoint — the app only connects to the Gateway it started.
/// </summary>
public partial class ConnectionBarViewModel : ViewModelBase
{
	private readonly IConnectionManager _connectionManager;
	private readonly IGatewayManager _gatewayManager;

	public ConnectionBarViewModel(
		IConnectionManager connectionManager,
		IGatewayManager gatewayManager
	)
	{
		_connectionManager = connectionManager;
		_gatewayManager = gatewayManager;

		Apply();
		connectionManager.StatusChanged += _ => Dispatcher.UIThread.Post(Apply);
		gatewayManager.StatusChanged += _ => Dispatcher.UIThread.Post(Apply);
	}

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LogOutCommand))]
	public partial bool IsConnected { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LogOutCommand))]
	public partial bool IsConnecting { get; set; }

	[ObservableProperty]
	public partial bool IsFailed { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LogOutCommand))]
	public partial bool CanLogIn { get; set; }

	[ObservableProperty]
	public partial string StateText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	public bool CanLogOut => !CanLogIn;

	[RelayCommand(CanExecute = nameof(CanLogOut))]
	private Task LogOutAsync() => _gatewayManager.StopAsync();

	/// <summary>
	/// On start: uses a Gateway still running from an earlier session (live or paper port)
	/// instead of asking to log in. True when one was found.
	/// </summary>
	public Task<bool> AttachRunningGatewayAsync() => _gatewayManager.AttachRunningAsync();

	private void Apply()
	{
		var gateway = _gatewayManager.Status;
		var connection = _connectionManager.Status;

		IsConnected =
			gateway.State == GatewayState.Running && connection.State == ConnectionState.Connected;
		IsConnecting =
			gateway.State
				is GatewayState.Starting
					or GatewayState.AwaitingLogin
					or GatewayState.Stopping
			|| (
				gateway.State == GatewayState.Running
				&& connection.State is ConnectionState.Connecting or ConnectionState.Reconnecting
			);
		IsFailed =
			gateway.State == GatewayState.Failed
			|| (
				gateway.State == GatewayState.Running && connection.State == ConnectionState.Failed
			);
		CanLogIn = gateway.State is GatewayState.Stopped or GatewayState.Failed;
		OnPropertyChanged(nameof(CanLogOut));

		StateText = gateway.State switch
		{
			GatewayState.Starting => "Starting IB Gateway…",
			GatewayState.AwaitingLogin => "Log in in the IB Gateway window…",
			GatewayState.Stopping => "Logging out…",
			GatewayState.Failed => "Login failed — click to retry",
			GatewayState.Running => FormatConnection(connection),
			_ => "Not logged in — click to log in",
		};
		ErrorMessage = gateway.State == GatewayState.Running ? connection.Error : gateway.Error;
	}

	private static string FormatConnection(ConnectionStatusDto status) =>
		status.State switch
		{
			ConnectionState.Connecting => $"Connecting to {status.Endpoint}…",
			ConnectionState.Connected => $"Connected — {status.Endpoint}",
			ConnectionState.Failed => $"Connection failed — {status.Endpoint}",
			ConnectionState.Reconnecting =>
				$"Reconnecting to {status.Endpoint} (attempt {status.ReconnectAttempt + 1}, next at {status.NextAttemptAt?.ToLocalTime():HH:mm:ss})",
			_ => "Gateway running — not connected",
		};
}
