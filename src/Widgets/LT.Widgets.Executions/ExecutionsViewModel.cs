using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Helpers;
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
		// After the base class has applied the connection state
		Dispatcher.UIThread.Post(ShowOffline);
	}

	public ObservableCollection<ExecutionDto> Executions { get; } = [];

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	/// <summary>Set while the list is the last cached copy, not live.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsStale))]
	public partial string? StaleText { get; set; }

	public bool IsStale => StaleText != null;

	[RelayCommand]
	public void Refresh() => RestartSession();

	protected override void OnConnected(CancellationToken session) =>
		_ = RunPeriodicAsync(LoadAsync, AccountConstants.ExecutionsRefreshInterval, session);

	protected override void OnDisconnected()
	{
		Executions.Clear();
		ShowOffline();
	}

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		RestartSession();
		ShowOffline();
	}

	// Disconnected, the manager answers from the cache: the account's last-known fills, if any
	private async void ShowOffline()
	{
		if (IsConnected)
			return;
		try
		{
			await LoadAsync(CancellationToken.None);
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		var account = IsConnected ? _accountStore.SelectedAccount : _accountStore.SnapshotAccount;
		var result = await _executionManager.GetExecutionsAsync(account, cancellationToken);
		Executions.Clear();
		foreach (var execution in result.Value)
			Executions.Add(execution);
		var fetchedAt = result.FetchedAt.ToLocalTime();
		StaleText = result.IsStale
			? CacheHelpers.FormatStale(fetchedAt, DateTimeOffset.Now, account ?? "", result.Error)
			: null;
		UpdatedText =
			result.IsStale ? $"{result.Value.Count} fills"
			: IsConnected ? $"{result.Value.Count} fills today · updated {fetchedAt:HH:mm:ss}"
			: null;
	}
}
