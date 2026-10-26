using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Positions;

/// <summary>Live positions (reqPositions stream) with per-position P&amp;L (reqPnLSingle).</summary>
public partial class PositionsViewModel : ConnectedViewModelBase, ILinkable
{
	private readonly IPositionManager _positionManager;
	private readonly AccountStore _accountStore;
	private readonly Dictionary<(string Account, int ContractId), PositionRowViewModel> _all = [];

	public PositionsViewModel(
		IPositionManager positionManager,
		IConnectionManager connectionManager,
		AccountStore accountStore,
		LinkGroupStore linkGroupStore
	)
		: base(connectionManager)
	{
		_positionManager = positionManager;
		_accountStore = accountStore;
		Link = new ContractLink(linkGroupStore, () => SelectedPosition?.Contract);
		accountStore.PropertyChanged += OnAccountChanged;
	}

	/// <summary>Clicking a position sends its contract to the link group.</summary>
	public ContractLink Link { get; }

	public ObservableCollection<PositionRowViewModel> Positions { get; } = [];

	[ObservableProperty]
	public partial PositionRowViewModel? SelectedPosition { get; set; }

	[ObservableProperty]
	public partial string TotalsText { get; set; } = string.Empty;

	partial void OnSelectedPositionChanged(PositionRowViewModel? value)
	{
		if (value != null)
			Link.Publish(value.Contract);
	}

	[RelayCommand]
	public void Refresh() => RestartSession();

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

	protected override void OnDisconnected() => Clear();

	private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AccountStore.SelectedAccount))
			RebuildVisible();
	}

	private async Task StreamPositionsAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (
				var position in _positionManager.SubscribePositionsAsync(cancellationToken)
			)
			{
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
		foreach (var row in _all.Values)
			row.PnlSubscription?.Cancel();
		_all.Clear();
		Positions.Clear();
		UpdateTotals();
	}
}
