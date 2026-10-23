using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Connection;

namespace LT.App.Widgets.ConnectionBar;

public partial class ConnectionBarViewModel : ViewModelBase
{
	private readonly IConnectionManager _connectionManager;

	public ConnectionBarViewModel(IConnectionManager connectionManager)
	{
		_connectionManager = connectionManager;

		var last = connectionManager.GetLastConnectRequest();
		Host = last.Host;
		Port = last.Port;
		ClientId = last.ClientId;

		Apply(connectionManager.Status);
		connectionManager.StatusChanged += status => Dispatcher.UIThread.Post(() => Apply(status));
	}

	public int PortMin => ConnectionConstants.PortMin;
	public int PortMax => ConnectionConstants.PortMax;
	public int ClientIdMin => ConnectionConstants.ClientIdMin;
	public int ClientIdMax => ConnectionConstants.ClientIdMax;

	[ObservableProperty]
	public partial string Host { get; set; } = string.Empty;

	[ObservableProperty]
	public partial decimal? Port { get; set; }

	[ObservableProperty]
	public partial decimal? ClientId { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand))]
	public partial ConnectionState State { get; set; }

	[ObservableProperty]
	public partial string StateText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	public bool IsConnected => State == ConnectionState.Connected;
	public bool IsConnecting => State == ConnectionState.Connecting;
	public bool IsFailed => State == ConnectionState.Failed;
	public bool CanEditEndpoint => State is ConnectionState.Disconnected or ConnectionState.Failed;

	partial void OnStateChanged(ConnectionState value)
	{
		OnPropertyChanged(nameof(IsConnected));
		OnPropertyChanged(nameof(IsConnecting));
		OnPropertyChanged(nameof(IsFailed));
		OnPropertyChanged(nameof(CanEditEndpoint));
	}

	[RelayCommand(CanExecute = nameof(CanEditEndpoint))]
	private async Task ConnectAsync()
	{
		ErrorMessage = null;
		try
		{
			await _connectionManager.ConnectAsync(
				new ConnectRequest
				{
					Host = Host.Trim(),
					Port = (int)(Port ?? 0),
					ClientId = (int)(ClientId ?? -1),
				}
			);
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	[RelayCommand(CanExecute = nameof(IsConnected))]
	private Task DisconnectAsync() => _connectionManager.DisconnectAsync();

	private void Apply(ConnectionStatusDto status)
	{
		State = status.State;
		StateText = status.State switch
		{
			ConnectionState.Connecting => $"Connecting to {status.Endpoint}…",
			ConnectionState.Connected => $"Connected — {status.Endpoint}",
			ConnectionState.Failed => $"Connection failed — {status.Endpoint}",
			_ => "Disconnected",
		};
		ErrorMessage = status.Error;
	}
}
