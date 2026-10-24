using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Executions;

public partial class ExecutionsViewModel : ConnectedViewModelBase
{
	private readonly IExecutionManager _executionManager;
	private readonly AccountStore _accountStore;

	public ExecutionsViewModel(
		IExecutionManager executionManager,
		IConnectionManager connectionManager,
		AccountStore accountStore
	)
		: base(connectionManager)
	{
		_executionManager = executionManager;
		_accountStore = accountStore;
		accountStore.PropertyChanged += OnAccountChanged;
	}

	public ObservableCollection<ExecutionDto> Executions { get; } = [];

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	[RelayCommand]
	public void Refresh() => RestartSession();

	protected override void OnConnected(CancellationToken session) =>
		_ = RunPeriodicAsync(LoadAsync, AccountConstants.ExecutionsRefreshInterval, session);

	protected override void OnDisconnected() => Executions.Clear();

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AccountStore.SelectedAccount))
			RestartSession();
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		var executions = await _executionManager.GetExecutionsAsync(
			_accountStore.SelectedAccount,
			cancellationToken
		);
		Executions.Clear();
		foreach (var execution in executions)
			Executions.Add(execution);
		UpdatedText = $"{executions.Count} fills today · updated {DateTime.Now:HH:mm:ss}";
	}
}
