using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Executions;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Executions;

public partial class ExecutionsViewModel : ConnectedViewModelBase, ILinkable
{
	private readonly IExecutionManager _executionManager;
	private readonly AccountStore _accountStore;
	private readonly DisplayTimeStore _displayTime;
	private readonly ZonedText _updatedText;
	private readonly ZonedText _staleText;
	private bool _restoringSelection;

	public ExecutionsViewModel(
		IExecutionManager executionManager,
		IConnectionManager connectionManager,
		AccountStore accountStore,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		_executionManager = executionManager;
		_accountStore = accountStore;
		_displayTime = displayTime;
		_updatedText = new ZonedText(x => UpdatedText = x);
		_staleText = new ZonedText(x => StaleText = x);
		Link = new ContractLink(
			linkGroupStore,
			() => GetLinkContract(SelectedExecution?.Execution)
		);
		displayTime.ZoneChanged += OnZoneChanged;
		accountStore.PropertyChanged += OnAccountChanged;
		// After the base class has applied the connection state
		Dispatcher.UIThread.Post(ShowOffline);
	}

	/// <summary>Clicking a fill sends its contract to the link group.</summary>
	public ContractLink Link { get; }

	public ObservableCollection<ExecutionRowViewModel> Executions { get; } = [];

	[ObservableProperty]
	public partial ExecutionRowViewModel? SelectedExecution { get; set; }

	[ObservableProperty]
	public partial string? UpdatedText { get; set; }

	/// <summary>Set while the list is the last cached copy, not live.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsStale))]
	public partial string? StaleText { get; set; }

	public bool IsStale => StaleText != null;

	partial void OnSelectedExecutionChanged(ExecutionRowViewModel? value)
	{
		// A reload putting the same fill back selected must not pull the group back to it
		if (!_restoringSelection && GetLinkContract(value?.Execution) is { } contract)
			Link.Publish(contract);
	}

	[RelayCommand]
	public void Refresh() => RestartSession();

	public override void Dispose()
	{
		_displayTime.ZoneChanged -= OnZoneChanged;
		Link.Dispose();
		base.Dispose();
	}

	protected override void OnConnected(CancellationToken session) =>
		_ = RunPeriodicAsync(LoadAsync, AccountConstants.ExecutionsRefreshInterval, session);

	protected override void OnDisconnected()
	{
		Executions.Clear();
		ShowOffline();
	}

	// Every row's time formatted again; the selected fill stays selected
	private void OnZoneChanged()
	{
		ShowExecutions(Executions.Select(x => x.Execution).ToList());
		_updatedText.Rebuild();
		_staleText.Rebuild();
	}

	// The list is rebuilt on every refresh; the same fill (by ExecId) stays selected
	private void ShowExecutions(IEnumerable<ExecutionDto> executions)
	{
		var selectedId = SelectedExecution?.Execution.ExecId;
		Executions.Clear();
		foreach (var execution in executions)
			Executions.Add(
				new ExecutionRowViewModel(
					execution,
					execution.Time is { } time ? _displayTime.Format(time) : FormatHelpers.Empty
				)
			);
		_restoringSelection = true;
		try
		{
			SelectedExecution = Executions.FirstOrDefault(x => x.Execution.ExecId == selectedId);
		}
		finally
		{
			_restoringSelection = false;
		}
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
		ShowExecutions(result.Value);
		if (result.IsStale)
			_staleText.Set(() =>
				CacheHelpers.FormatStale(
					_displayTime.ToDisplay(result.FetchedAt),
					_displayTime.Now,
					account ?? "",
					result.Error
				)
			);
		else
			_staleText.Clear();
		if (result.IsStale)
			_updatedText.SetFixed($"{result.Value.Count} fills");
		else if (IsConnected)
			_updatedText.Set(() =>
				$"{result.Value.Count} fills today · updated {_displayTime.Format(result.FetchedAt)}"
			);
		else
			_updatedText.Clear();
	}

	// Cached fills are for looking at only, like remembered positions; they drive no other widget
	private ContractDto? GetLinkContract(ExecutionDto? execution) =>
		execution is { Contract.ConId: > 0 } && !IsStale ? execution.Contract : null;
}
