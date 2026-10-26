using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Controls;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Watchlists;

namespace LT.App.Widgets.Watchlist;

public partial class WatchlistViewModel : ConnectedViewModelBase, ILinkable, ITickerDragSource
{
	private readonly IWatchlistManager _watchlistManager;
	private readonly IContractManager _contractManager;
	private readonly IMarketDataManager _marketDataManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly ISharesOutstandingManager _sharesOutstandingManager;
	private CancellationToken _session = new(true);

	// The rows in the watchlist's saved order: the base of every sort, and of no sort
	private readonly List<WatchlistRowViewModel> _savedOrder = [];

	// A quote (or share count) changed the sorted column; re-sorted on the next resort tick
	private bool _resortPending;

	// Rows are being moved: the grid drops and restores the selection, which isn't a new pick
	private bool _reordering;

	public WatchlistViewModel(
		IWatchlistManager watchlistManager,
		IContractManager contractManager,
		IMarketDataManager marketDataManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		ISettingsManager settingsManager,
		ISharesOutstandingManager sharesOutstandingManager,
		LinkGroupStore linkGroupStore
	)
		: base(connectionManager)
	{
		_watchlistManager = watchlistManager;
		_contractManager = contractManager;
		_marketDataManager = marketDataManager;
		_messageLogManager = messageLogManager;
		_sharesOutstandingManager = sharesOutstandingManager;
		Link = new ContractLink(linkGroupStore, () => SelectedRow?.Contract);

		LoadWatchlists();
		LoadRows();

		settingsManager.AppSettingsChanged += _ => Dispatcher.UIThread.Post(RestartSession);

		new DispatcherTimer(
			MarketDataConstants.UiFlushInterval,
			DispatcherPriority.Background,
			(_, _) =>
			{
				var changed = false;
				foreach (var row in Rows)
					changed |= row.Flush();
				if (changed && WatchlistSortHelpers.DependsOnQuotes(SortColumn))
					_resortPending = true;
			}
		).Start();

		new DispatcherTimer(
			MarketDataConstants.WatchlistResortInterval,
			DispatcherPriority.Background,
			(_, _) =>
			{
				if (_resortPending)
					ApplyOrder();
			}
		).Start();
	}

	/// <summary>The column the unpinned rows are sorted by (header click); pinned rows stay on top.</summary>
	[ObservableProperty]
	public partial WatchlistSortColumn SortColumn { get; private set; }

	[ObservableProperty]
	public partial ListSortDirection SortDirection { get; private set; }

	/// <summary>A sortable column's header was clicked: sort by it, or flip its direction.</summary>
	public void SortBy(WatchlistSortColumn column)
	{
		(SortColumn, SortDirection) = WatchlistSortHelpers.NextSort(
			SortColumn,
			SortDirection,
			column
		);
		ApplyOrder();
	}

	/// <summary>The selected row can be pinned (context menu).</summary>
	public bool CanPin => SelectedRow is { IsPinned: false };

	/// <summary>The selected row can be unpinned (context menu).</summary>
	public bool CanUnpin => SelectedRow is { IsPinned: true };

	[RelayCommand]
	private void Pin(WatchlistRowViewModel? row)
	{
		row ??= SelectedRow;
		if (row != null && Run(() => _watchlistManager.Pin(row.Contract)))
			LoadPins();
	}

	[RelayCommand]
	private void Unpin(WatchlistRowViewModel? row)
	{
		row ??= SelectedRow;
		if (row != null && Run(() => _watchlistManager.Unpin(row.Contract)))
			LoadPins();
	}

	// Pin places from the saved watchlist, then the rows in display order
	private void LoadPins()
	{
		var pinned = _watchlistManager.GetPinned();
		foreach (var row in _savedOrder)
		{
			var index = pinned
				.Select((contract, i) => (contract, i))
				.FirstOrDefault(x => ContractHelpers.IsSameListing(x.contract, row.Contract));
			row.PinIndex = index.contract != null ? index.i : null;
		}
		OnPropertyChanged(nameof(CanPin));
		OnPropertyChanged(nameof(CanUnpin));
		ApplyOrder();
	}

	// Moves rows where the pins and the sort want them; keeps the selection without republishing it
	private void ApplyOrder()
	{
		_resortPending = false;
		var order = WatchlistSortHelpers.Order(
			_savedOrder,
			row => row.PinIndex,
			SortColumn,
			SortDirection,
			(row, column) => row.GetSortKey(column)
		);
		var selected = SelectedRow;
		_reordering = true;
		try
		{
			if (CollectionHelpers.ApplyOrder(Rows, order) > 0)
				SelectedRow = selected;
		}
		finally
		{
			_reordering = false;
		}
	}

	public ObservableCollection<WatchlistRowViewModel> Rows { get; } = [];

	/// <summary>The named watchlists, as rows of the header picker.</summary>
	public ObservableCollection<WatchlistItemViewModel> Watchlists { get; } = [];

	/// <summary>The shown watchlist (header button).</summary>
	[ObservableProperty]
	public partial WatchlistItemViewModel? ActiveWatchlist { get; set; }

	/// <summary>The last watchlist can't be deleted: rows hide their ✕ then.</summary>
	public bool CanDelete => Watchlists.Count > 1;

	[RelayCommand]
	private void SelectWatchlist(WatchlistItemViewModel item)
	{
		if (item.IsActive)
			return;
		Run(() => _watchlistManager.SetActive(item.Id));
		LoadWatchlists();
		LoadRows();
	}

	/// <summary>Creates "Watchlist N", switches to it and opens its name for editing.</summary>
	[RelayCommand]
	private void NewWatchlist()
	{
		if (
			!Run(() =>
				_watchlistManager.SaveName(
					new SaveWatchlistNameRequest { Name = _watchlistManager.GetNewName() }
				)
			)
		)
			return;
		LoadWatchlists();
		LoadRows();
		if (ActiveWatchlist != null)
			BeginRename(ActiveWatchlist);
	}

	[RelayCommand]
	private void BeginRename(WatchlistItemViewModel item)
	{
		foreach (var row in Watchlists)
			row.IsConfirmingDelete = row.IsRenaming = false;
		item.RenameText = item.Name;
		item.IsRenaming = true;
	}

	/// <summary>Saves the typed name; on an error (blank, taken) the name box stays open.</summary>
	[RelayCommand]
	private void CommitRename(WatchlistItemViewModel item)
	{
		if (!item.IsRenaming)
			return;
		// Close the box first: hiding it fires its LostFocus, which must not commit again while
		// the list below is being rebuilt
		item.IsRenaming = false;
		if (
			Run(() =>
				_watchlistManager.SaveName(
					new SaveWatchlistNameRequest { Id = item.Id, Name = item.RenameText }
				)
			)
		)
			LoadWatchlists();
		else
			item.IsRenaming = true;
	}

	[RelayCommand]
	private void CancelRename(WatchlistItemViewModel item)
	{
		item.IsRenaming = false;
		ErrorMessage = null;
	}

	[RelayCommand]
	private void AskDelete(WatchlistItemViewModel item)
	{
		foreach (var row in Watchlists)
			row.IsConfirmingDelete = row.IsRenaming = false;
		item.IsConfirmingDelete = true;
	}

	[RelayCommand]
	private void ConfirmDelete(WatchlistItemViewModel item)
	{
		Run(() => _watchlistManager.Delete(item.Id));
		LoadWatchlists();
		LoadRows();
	}

	[RelayCommand]
	private void CancelDelete(WatchlistItemViewModel item) => item.IsConfirmingDelete = false;

	// Watchlist changes report problems (taken name, last list) in the widget's error line
	private bool Run(Action change)
	{
		try
		{
			change();
			ErrorMessage = null;
			return true;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return false;
		}
	}

	private void LoadWatchlists()
	{
		var activeId = _watchlistManager.GetActive().Id;
		Watchlists.Clear();
		foreach (var watchlist in _watchlistManager.GetWatchlists())
			Watchlists.Add(new WatchlistItemViewModel(this, watchlist, watchlist.Id == activeId));
		ActiveWatchlist = Watchlists.FirstOrDefault(x => x.IsActive);
		OnPropertyChanged(nameof(CanDelete));
	}

	// The active watchlist's rows, streaming when connected
	private void LoadRows()
	{
		foreach (var row in Rows)
			Stop(row);
		Rows.Clear();
		_savedOrder.Clear();
		foreach (var contract in _watchlistManager.GetItems())
		{
			var row = new WatchlistRowViewModel(contract);
			_savedOrder.Add(row);
			Rows.Add(row);
		}
		LoadPins();
		SelectedRow = Rows.FirstOrDefault();
		if (IsConnected)
			foreach (var row in Rows)
				Start(row);
	}

	/// <summary>Clicking a row sends its contract to the link group.</summary>
	public ContractLink Link { get; }

	[ObservableProperty]
	public partial WatchlistRowViewModel? SelectedRow { get; set; }

	/// <summary>A watchlist row dragged onto a chart.</summary>
	public ContractDto? GetDragContract(object? item) =>
		item is WatchlistRowViewModel { Contract.ConId: > 0 } row ? row.Contract : null;

	partial void OnSelectedRowChanged(WatchlistRowViewModel? value)
	{
		OnPropertyChanged(nameof(CanPin));
		OnPropertyChanged(nameof(CanUnpin));
		if (value != null && !_reordering)
			Link.Publish(value.Contract);
	}

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		foreach (var row in Rows)
			Start(row);
	}

	protected override void OnDisconnected()
	{
		foreach (var row in Rows)
			Stop(row);
	}

	public override void Dispose()
	{
		Link.Dispose();
		base.Dispose();
	}

	/// <summary>Search box suggestions: IB's listings of the typed symbol.</summary>
	public Task<IEnumerable<object>> GetSuggestionsAsync(
		string? text,
		CancellationToken cancellationToken
	) =>
		string.IsNullOrWhiteSpace(text) || !IsConnected
			? Task.FromResult(Enumerable.Empty<object>())
			: ContractSuggestionItem.SearchAsync(_contractManager, text, cancellationToken);

	/// <summary>Search box: a typed ticker, resolved to its first listing (US first).</summary>
	public async Task AddTickerAsync(string ticker)
	{
		ErrorMessage = null;
		try
		{
			AddContract(await _contractManager.ResolveTickerAsync(ticker, _session));
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	/// <summary>Search box: a picked listing.</summary>
	public void AddContract(ContractDto contract)
	{
		try
		{
			_watchlistManager.Add(contract);
			var row = new WatchlistRowViewModel(contract);
			_savedOrder.Add(row);
			Rows.Add(row);
			ApplyOrder();
			SelectedRow = row;
			if (IsConnected)
				Start(row);
			ErrorMessage = null;
			LoadWatchlists();
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	[RelayCommand]
	private void Remove(WatchlistRowViewModel? row)
	{
		row ??= SelectedRow;
		if (row == null)
			return;

		Stop(row);
		_watchlistManager.Remove(row.Contract);
		_savedOrder.Remove(row);
		Rows.Remove(row);
		LoadWatchlists();
		if (row.IsPinned)
			LoadPins();
	}

	private void Start(WatchlistRowViewModel row)
	{
		Stop(row);
		var subscription = CancellationTokenSource.CreateLinkedTokenSource(_session);
		row.Subscription = subscription;
		var token = subscription.Token;
		_ = Task.Run(
			async () =>
			{
				try
				{
					await foreach (
						var quote in _marketDataManager
							.SubscribeQuotesAsync(row.Contract, token)
							.ConfigureAwait(false)
					)
						row.Pending.Set(quote);
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					var message = ErrorMessageHelpers.ToUserMessage(ex);
					Dispatcher.UIThread.Post(() => row.Error = message);
					_messageLogManager.Add(
						MessageSeverity.Warning,
						MessageLogConstants.SourceMarketData,
						$"{row.Symbol}: {message}"
					);
				}
			},
			token
		);
		LoadSharesOutstanding(row, token);
	}

	// Market cap's share count from the SEC (cached on disk); US stocks only, others stay "—"
	private void LoadSharesOutstanding(WatchlistRowViewModel row, CancellationToken token)
	{
		if (!MarketCapHelpers.IsEligible(row.Contract))
			return;
		_ = Task.Run(
			async () =>
			{
				try
				{
					var shares = await _sharesOutstandingManager
						.GetSharesOutstandingAsync(row.Contract, token)
						.ConfigureAwait(false);
					Dispatcher.UIThread.Post(() =>
					{
						row.SetSharesOutstanding(shares);
						if (SortColumn == WatchlistSortColumn.MarketCap)
							_resortPending = true;
					});
				}
				catch (OperationCanceledException) { }
			},
			token
		);
	}

	private static void Stop(WatchlistRowViewModel row)
	{
		row.Subscription?.Cancel();
		row.Subscription?.Dispose();
		row.Subscription = null;
	}
}
