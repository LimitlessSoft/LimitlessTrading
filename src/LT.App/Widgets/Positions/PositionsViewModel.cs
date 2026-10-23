using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Dtos.Connection;
using LT.Contracts.Dtos.Positions;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Positions;

public partial class PositionsViewModel : ViewModelBase
{
	private readonly IPositionManager _positionManager;

	public PositionsViewModel(
		IConnectionManager connectionManager,
		IPositionManager positionManager
	)
	{
		_positionManager = positionManager;
		IsConnected = connectionManager.Status.State == ConnectionState.Connected;
		connectionManager.StatusChanged += status =>
			Dispatcher.UIThread.Post(() => OnConnectionStatusChanged(status));
	}

	public ObservableCollection<PositionDto> Positions { get; } = [];

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
	public partial bool IsConnected { get; set; }

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	[RelayCommand(CanExecute = nameof(IsConnected))]
	private async Task RefreshAsync()
	{
		ErrorMessage = null;
		try
		{
			var positions = await _positionManager.GetPositionsAsync();
			Positions.Clear();
			foreach (var position in positions)
				Positions.Add(position);
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	private void OnConnectionStatusChanged(ConnectionStatusDto status)
	{
		IsConnected = status.State == ConnectionState.Connected;
		if (IsConnected)
			RefreshCommand.Execute(null);
		else
			Positions.Clear();
	}
}
