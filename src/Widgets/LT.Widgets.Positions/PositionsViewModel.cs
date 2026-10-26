using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Positions;

/// <summary>Live positions (reqPositions stream) with per-position P&amp;L (reqPnLSingle).</summary>
public partial class PositionsViewModel : ConnectedViewModelBase, ILinkable
{
	private readonly IPositionManager _positionManager;
	private readonly AccountStore _accountStore;
	private readonly OrderTicketNavigator _orderTicketNavigator;
	private readonly Dictionary<(string Account, int ContractId), PositionRowViewModel> _all = [];

	public PositionsViewModel(
		IPositionManager positionManager,
		IConnectionManager connectionManager,
		AccountStore accountStore,
		LinkGroupStore linkGroupStore,
		OrderTicketNavigator orderTicketNavigator
	)
		: base(connectionManager)
	{
		_positionManager = positionManager;
		_accountStore = accountStore;
		_orderTicketNavigator = orderTicketNavigator;
		Link = new ContractLink(linkGroupStore, () => SelectedPosition?.Contract);
		accountStore.PropertyChanged += OnAccountChanged;
		// After the base class has applied the connection state
		Dispatcher.UIThread.Post(() =>
		{
			if (!IsConnected)
				ShowSnapshot(null);
		});
	}

	/// <summary>Clicking a position sends its contract to the link group.</summary>
	public ContractLink Link { get; }

	public ObservableCollection<PositionRowViewModel> Positions { get; } = [];

	[ObservableProperty]
	public partial PositionRowViewModel? SelectedPosition { get; set; }

	[ObservableProperty]
	public partial string TotalsText { get; set; } = string.Empty;

	/// <summary>Set while the rows are the account's last-known positions, not live (no P&amp;L).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsStale))]
	[NotifyPropertyChangedFor(nameof(ShowConnectHint))]
	[NotifyPropertyChangedFor(nameof(CanClose))]
	public partial string? StaleText { get; set; }

	public bool IsStale => StaleText != null;

	public bool ShowConnectHint => !IsConnected && !IsStale;

	/// <summary>Only live positions can be closed; remembered ones may be out of date.</summary>
	public bool CanClose => IsConnected && !IsStale;

	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.PropertyName == nameof(IsConnected))
		{
			OnPropertyChanged(nameof(ShowConnectHint));
			OnPropertyChanged(nameof(CanClose));
		}
	}

	partial void OnSelectedPositionChanged(PositionRowViewModel? value)
	{
		// Remembered rows are for looking at only; they drive no other widget
		if (value != null && !IsStale)
			Link.Publish(value.Contract);
	}

	[RelayCommand]
	public void Refresh() => RestartSession();

	/// <summary>
	/// Opens an order ticket pre-filled to flatten <paramref name="row"/>; it is only sent from
	/// the ticket, after review, on "Send order".
	/// </summary>
	[RelayCommand]
	private void ClosePosition(PositionRowViewModel row)
	{
		if (!CanClose || OrderHelpers.GetClosingDraft(row.Position) is not { } draft)
			return;
		_orderTicketNavigator.Open(draft);
	}

	public override void Dispose()
	{
		Link.Dispose();
		base.Dispose();
	}

	protected override void OnConnected(CancellationToken session)
	{
		Clear();
		_ = StreamPositionsAsync(session);
	}

	protected override void OnDisconnected()
	{
		Clear();
		ShowSnapshot(null);
	}

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AccountStore.SelectedAccount))
			return;
		if (IsStale || !IsConnected)
			ShowSnapshot(null);
		else
			RebuildVisible();
	}

	// Display only, never for decisions: a remembered position may have changed since
	private void ShowSnapshot(string? error)
	{
		Positions.Clear();
		StaleText = null;
		UpdateTotals();
		if (_accountStore.SnapshotAccount is not { } account)
			return;
		if (_positionManager.GetPositionsSnapshot(account) is not { } snapshot)
			return;
		foreach (var position in snapshot.Value.OrderBy(x => x.Symbol))
			Positions.Add(new PositionRowViewModel(position));
		StaleText = CacheHelpers.FormatStale(
			snapshot.FetchedAt.ToLocalTime(),
			DateTimeOffset.Now,
			account,
			error
		);
		TotalsText = Positions.Count == 0 ? string.Empty : $"{Positions.Count} positions";
	}

	private async Task StreamPositionsAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (
				var position in _positionManager.SubscribePositionsAsync(cancellationToken)
			)
			{
				if (IsStale)
				{
					Positions.Clear();
					StaleText = null;
				}
				var key = (position.Account, position.ContractId);
				if (position.Quantity == 0)
				{
					if (_all.Remove(key, out var closed))
					{
						closed.PnlSubscription?.Cancel();
						Positions.Remove(closed);
					}
				}
				else if (_all.TryGetValue(key, out var row))
					row.Update(position);
				else
				{
					row = new PositionRowViewModel(position);
					_all[key] = row;
					if (IsVisible(row))
						Positions.Add(row);
					row.PnlSubscription = CancellationTokenSource.CreateLinkedTokenSource(
						cancellationToken
					);
					_ = StreamPnlAsync(row, row.PnlSubscription.Token);
				}
				UpdateTotals();
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			if (_all.Count == 0)
				ShowSnapshot(ErrorMessage);
		}
	}

	private async Task StreamPnlAsync(PositionRowViewModel row, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (
				var pnl in _positionManager.SubscribePnlAsync(
					row.Account,
					row.ContractId,
					cancellationToken
				)
			)
			{
				row.ApplyPnl(pnl);
				UpdateTotals();
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = $"{row.Symbol}: {ErrorMessageHelpers.ToUserMessage(ex)}";
		}
	}

	private bool IsVisible(PositionRowViewModel row) =>
		_accountStore.SelectedAccount == null || row.Account == _accountStore.SelectedAccount;

	private void RebuildVisible()
	{
		Positions.Clear();
		foreach (var row in _all.Values.Where(IsVisible).OrderBy(x => x.Symbol))
			Positions.Add(row);
		UpdateTotals();
	}

	private void UpdateTotals()
	{
		var unrealized = Positions.Sum(x => x.UnrealizedValue ?? 0);
		var daily = Positions.Sum(x => x.DailyValue ?? 0);
		TotalsText =
			Positions.Count == 0
				? string.Empty
				: $"{Positions.Count} positions · unrealized {FormatHelpers.SignedMoney(unrealized)} · daily {FormatHelpers.SignedMoney(daily)}";
	}

	private void Clear()
	{
		StaleText = null;
		foreach (var row in _all.Values)
			row.PnlSubscription?.Cancel();
		_all.Clear();
		Positions.Clear();
		UpdateTotals();
	}
}
