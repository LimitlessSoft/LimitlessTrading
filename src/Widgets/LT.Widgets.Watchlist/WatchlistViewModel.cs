using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSCore.Validation.Contracts;
using LT.App.Controls;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Dtos.Watchlists;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Watchlists;

namespace LT.App.Widgets.Watchlist;

public partial class WatchlistViewModel
	: ConnectedViewModelBase,
		ILinkable,
		ITickerDragSource,
		IWidgetState
{
	private readonly IWatchlistManager _watchlistManager;
	private readonly ISettingsManager _settingsManager;
	private readonly DispatcherTimer _flushTimer;
	private readonly DispatcherTimer _resortTimer;
	private readonly IContractManager _contractManager;
	private readonly IMarketDataManager _marketDataManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly ISharesOutstandingManager _sharesOutstandingManager;
	private CancellationToken _session = new(true);

	// The watchlist this widget shows; every watchlist widget has its own
	private string _watchlistId;

	// This widget is changing the watchlists itself: it updates its rows on its own, so it skips
	// the manager's Changed it causes
	private bool _changing;

	// The rows in the watchlist's saved order: the base of every sort, and of no sort
	private readonly List<WatchlistRowViewModel> _savedOrder = [];

	// A quote (or share count) changed the sorted column; re-sorted on the next resort tick
	private bool _resortPending;

	// Rows are being moved: the grid drops and restores the selection, which isn't a new pick
	private bool _reordering;

	// Columns taken off the grid (saved per widget); the rows can still be sorted by them
	private HashSet<WatchlistSortColumn> _hiddenColumns = [];
	private HashSet<string> _hiddenCustomColumns = [];

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
		_settingsManager = settingsManager;
		Link = new ContractLink(linkGroupStore, () => SelectedRow?.Contract);

		// A new widget opens on the watchlist picked last
		_watchlistId = watchlistManager.GetActive().Id;
		LoadWatchlists();
		LoadRows();

		settingsManager.AppSettingsChanged += OnAppSettingsChanged;
		watchlistManager.Changed += OnWatchlistsChanged;

		_flushTimer = new DispatcherTimer(
			MarketDataConstants.UiFlushInterval,
			DispatcherPriority.Background,
			(_, _) => FlushQuotes()
		);
		_flushTimer.Start();

		_resortTimer = new DispatcherTimer(
			MarketDataConstants.WatchlistResortInterval,
			DispatcherPriority.Background,
			(_, _) => ResortIfStale()
		);
		_resortTimer.Start();
	}

	/// <summary>
	/// The UI flush (timer): applies the quotes that came since the last one; when sorted by a
	/// quote column, shown or hidden, the order goes stale for <see cref="ResortIfStale"/>.
	/// </summary>
	public void FlushQuotes()
	{
		var changed = false;
		foreach (var row in Rows)
			changed |= row.Flush();
		if (changed && WatchlistSortHelpers.DependsOnQuotes(SortColumn))
			_resortPending = true;
	}

	/// <summary>The resort tick (timer): re-sorts when quotes or share counts moved the sorted column.</summary>
	public void ResortIfStale()
	{
		if (_resortPending)
			ApplyOrder();
	}

	/// <summary>
	/// A saved widget shows its own watchlist again (a deleted one: the active one), with its
	/// hidden columns and its sort.
	/// </summary>
	public void LoadState(WidgetPlacementDto placement)
	{
		_hiddenColumns = [.. placement.WatchlistHiddenColumns ?? []];
		_hiddenCustomColumns = [.. placement.WatchlistHiddenCustomColumns ?? []];
		(SortColumn, SortCustomColumnId, SortDirection) = WatchlistSortHelpers.ResolveSort(
			placement.WatchlistSortBy,
			placement.WatchlistSortCustomColumnId,
			placement.WatchlistSortDescending
		);
		var id = WatchlistHelpers.ResolveShownId(
			placement.WatchlistId,
			_watchlistManager.GetActive().Id,
			_watchlistManager.GetWatchlists()
		);
		if (id != _watchlistId)
		{
			_watchlistId = id;
			LoadWatchlists();
			LoadRows();
		}
		else
			// Checks the sorted custom column still exists
			LoadColumns();
		ApplyHiddenColumns();
		ApplyOrder();
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.WatchlistId = _watchlistId;
		placement.WatchlistHiddenColumns =
			_hiddenColumns.Count > 0
				? [.. WatchlistColumnHelpers.BuiltInColumns.Where(_hiddenColumns.Contains)]
				: null;
		placement.WatchlistHiddenCustomColumns =
			_hiddenCustomColumns.Count > 0 ? [.. _hiddenCustomColumns.Order()] : null;
		placement.WatchlistSortBy = SortColumn != WatchlistSortColumn.None ? SortColumn : null;
		placement.WatchlistSortCustomColumnId = SortCustomColumnId;
		placement.WatchlistSortDescending =
			SortColumn != WatchlistSortColumn.None && SortDirection == ListSortDirection.Descending
				? true
				: null;
	}

	private void OnAppSettingsChanged(AppSettingsDto settings) =>
		Dispatcher.UIThread.Post(RestartSession);

	// Another watchlist widget changed the watchlists: follow a rename, a deleted list (back to
	// the active one) and items or pins added, removed or moved on the list shown here
	private void OnWatchlistsChanged()
	{
		if (_changing)
			return;
		var id = WatchlistHelpers.ResolveShownId(
			_watchlistId,
			_watchlistManager.GetActive().Id,
			_watchlistManager.GetWatchlists()
		);
		var reload =
			id != _watchlistId
			|| !WatchlistHelpers.IsSameItems(
				_watchlistManager.GetItems(id),
				_savedOrder.Select(x => x.Contract)
			);
		_watchlistId = id;
		LoadWatchlists();
		if (reload)
			LoadRows();
		else
		{
			LoadColumns();
			LoadPins();
		}
	}

	/// <summary>The column the unpinned rows are sorted by (header click); pinned rows stay on top.</summary>
	[ObservableProperty]
	public partial WatchlistSortColumn SortColumn { get; private set; }

	[ObservableProperty]
	public partial ListSortDirection SortDirection { get; private set; }

	/// <summary>The custom column sorted by, when <see cref="SortColumn"/> is <see cref="WatchlistSortColumn.Custom"/>.</summary>
	[ObservableProperty]
	public partial string? SortCustomColumnId { get; private set; }

	/// <summary>A sortable column's header was clicked: sort by it, or flip its direction.</summary>
	public void SortBy(WatchlistSortColumn column) => Sort(column, null);

	/// <summary>
	/// A column's sort arrow in the header popup, hidden column or not: sort by it, or flip the
	/// direction, as a header click does.
	/// </summary>
	[RelayCommand]
	private void SortByColumn(WatchlistColumnOptionViewModel option) =>
		Sort(option.Column, option.CustomColumnId);

	/// <summary>Sorts by a column in a given direction (the header menu), hidden or not.</summary>
	public void SortBy(
		WatchlistSortColumn column,
		string? customColumnId,
		ListSortDirection direction
	)
	{
		if (column == WatchlistSortColumn.None)
		{
			ClearSort();
			return;
		}
		(SortColumn, SortCustomColumnId, SortDirection) = (
			column,
			column == WatchlistSortColumn.Custom ? customColumnId : null,
			direction
		);
		ApplyOrder();
	}

	/// <summary>Back to the watchlist's saved order (pinned rows stay on top).</summary>
	[RelayCommand]
	private void ClearSort()
	{
		(SortColumn, SortCustomColumnId, SortDirection) = (
			WatchlistSortColumn.None,
			null,
			ListSortDirection.Ascending
		);
		ApplyOrder();
	}

	/// <summary>Sorted by a column (the header popup's "Clear sort").</summary>
	public bool HasSort => SortColumn != WatchlistSortColumn.None;

	/// <summary>Sorted by a column that is hidden: the grid has no arrow for it, so a line says so.</summary>
	public bool IsSortHidden =>
		SortColumn != WatchlistSortColumn.None && !IsColumnShown(SortColumn, SortCustomColumnId);

	/// <summary>The line above the grid while sorted by a hidden column ("Sorted by Vol ↓").</summary>
	public string? HiddenSortText =>
		IsSortHidden
			? $"Sorted by {GetColumnName(SortColumn, SortCustomColumnId)} (hidden) "
				+ (SortDirection == ListSortDirection.Descending ? "↓" : "↑")
			: null;

	partial void OnSortColumnChanged(WatchlistSortColumn value) => UpdateColumnOptions();

	partial void OnSortDirectionChanged(ListSortDirection value) => UpdateColumnOptions();

	partial void OnSortCustomColumnIdChanged(string? value) => UpdateColumnOptions();

	/// <summary>The grid's names of the built-in columns (headers, the header popup's list).</summary>
	public static string GetBuiltInName(WatchlistSortColumn column) =>
		column switch
		{
			WatchlistSortColumn.Symbol => "Symbol",
			WatchlistSortColumn.Last => "Last",
			WatchlistSortColumn.ChangePercent => "Chg %",
			WatchlistSortColumn.Bid => "Bid / Ask",
			WatchlistSortColumn.Volume => "Vol",
			WatchlistSortColumn.MarketCap => "Mkt Cap",
			_ => column.ToString(),
		};

	private string GetColumnName(WatchlistSortColumn column, string? customColumnId) =>
		column == WatchlistSortColumn.Custom
			? CustomColumns.FirstOrDefault(x => x.Id == customColumnId)?.Name ?? string.Empty
			: GetBuiltInName(column);

	/// <summary>
	/// Every column, built-in then custom, as the header popup's list: show / hide, sort, and for
	/// custom ones rename and delete.
	/// </summary>
	[ObservableProperty]
	public partial IReadOnlyList<WatchlistColumnOptionViewModel> Columns { get; private set; } = [];

	/// <summary>A column was shown or hidden: the view shows or hides its grid column.</summary>
	public event Action? ColumnVisibilityChanged;

	/// <summary>Whether a column is on the grid; a custom one by its id.</summary>
	public bool IsColumnShown(WatchlistSortColumn column, string? customColumnId) =>
		column == WatchlistSortColumn.Custom
			? customColumnId == null || !_hiddenCustomColumns.Contains(customColumnId)
			: !_hiddenColumns.Contains(column);

	/// <summary>Whether a column can be shown or hidden: the last shown one can't be hidden.</summary>
	public bool CanToggleColumn(WatchlistSortColumn column, string? customColumnId) =>
		WatchlistColumnHelpers.CanToggle(IsColumnShown(column, customColumnId), ShownColumnCount);

	/// <summary>A column's check box in the header popup was flipped.</summary>
	public void SetColumnShown(WatchlistColumnOptionViewModel option, bool shown) =>
		SetColumnShown(option.Column, option.CustomColumnId, shown);

	/// <summary>
	/// Shows or hides a column (header popup, header menu); hiding the last shown one is refused.
	/// The sort is kept: rows stay sorted by a hidden column.
	/// </summary>
	public void SetColumnShown(WatchlistSortColumn column, string? customColumnId, bool shown)
	{
		if (
			column == WatchlistSortColumn.None
			|| (column == WatchlistSortColumn.Custom && customColumnId == null)
			|| IsColumnShown(column, customColumnId) == shown
		)
			return;
		if (shown || CanToggleColumn(column, customColumnId))
		{
			if (column == WatchlistSortColumn.Custom)
			{
				if (shown)
					_hiddenCustomColumns.Remove(customColumnId!);
				else
					_hiddenCustomColumns.Add(customColumnId!);
			}
			else if (shown)
				_hiddenColumns.Remove(column);
			else
				_hiddenColumns.Add(column);
			ColumnVisibilityChanged?.Invoke();
		}
		// A refused flip puts its check box back
		UpdateColumnOptions();
	}

	// The hidden columns never leave the grid empty (every built-in one hidden and the last shown
	// custom one deleted, or on another watchlist: Symbol comes back); the view and the list follow
	private void ApplyHiddenColumns()
	{
		_hiddenColumns = WatchlistColumnHelpers.ResolveHidden(
			_hiddenColumns,
			CustomColumns.Count(x => !_hiddenCustomColumns.Contains(x.Id))
		);
		ColumnVisibilityChanged?.Invoke();
		UpdateColumnOptions();
	}

	private int ShownColumnCount =>
		WatchlistColumnHelpers.BuiltInColumns.Count(x => !_hiddenColumns.Contains(x))
		+ CustomColumns.Count(x => !_hiddenCustomColumns.Contains(x.Id));

	// The list is rebuilt when the custom columns change (and once at first)
	private void RebuildColumnOptions()
	{
		Columns =
		[
			.. WatchlistColumnHelpers.BuiltInColumns.Select(x => new WatchlistColumnOptionViewModel(
				this,
				x,
				GetBuiltInName(x),
				null
			)),
			.. CustomColumns.Select(x => new WatchlistColumnOptionViewModel(
				this,
				WatchlistSortColumn.Custom,
				x.Name,
				x
			)),
		];
		UpdateColumnOptions();
	}

	// Every row of the list follows the shown columns and the sort; so does the hidden sort line
	private void UpdateColumnOptions()
	{
		var shownCount = ShownColumnCount;
		foreach (var option in Columns)
		{
			var shown = IsColumnShown(option.Column, option.CustomColumnId);
			option.Update(
				shown,
				WatchlistColumnHelpers.CanToggle(shown, shownCount),
				option.Column == SortColumn && option.CustomColumnId == SortCustomColumnId,
				SortDirection == ListSortDirection.Descending
			);
		}
		OnPropertyChanged(nameof(HasSort));
		OnPropertyChanged(nameof(IsSortHidden));
		OnPropertyChanged(nameof(HiddenSortText));
	}

	/// <summary>A custom column's header was clicked: sort by its values, or flip the direction.</summary>
	public void SortByCustom(string columnId) => Sort(WatchlistSortColumn.Custom, columnId);

	private void Sort(WatchlistSortColumn column, string? customColumnId)
	{
		(SortColumn, SortCustomColumnId, SortDirection) = WatchlistSortHelpers.NextSort(
			SortColumn,
			SortCustomColumnId,
			SortDirection,
			column,
			customColumnId
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
		if (row != null && Run(() => _watchlistManager.Pin(_watchlistId, row.Contract)))
			LoadPins();
	}

	[RelayCommand]
	private void Unpin(WatchlistRowViewModel? row)
	{
		row ??= SelectedRow;
		if (row != null && Run(() => _watchlistManager.Unpin(_watchlistId, row.Contract)))
			LoadPins();
	}

	// Pin places from the saved watchlist, then the rows in display order
	private void LoadPins()
	{
		var pinned = _watchlistManager.GetPinned(_watchlistId);
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
			(row, column) =>
				column == WatchlistSortColumn.Custom
					? WatchlistSortHelpers.GetCustomSortKey(row.GetCustomValue(SortCustomColumnId))
					: row.GetSortKey(column)
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
		// Also where the next new watchlist widget opens
		if (!Run(() => _watchlistManager.SetActive(item.Id)))
			return;
		_watchlistId = item.Id;
		LoadWatchlists();
		LoadRows();
	}

	/// <summary>Creates "Watchlist N", switches to it and opens its name for editing.</summary>
	[RelayCommand]
	private void NewWatchlist()
	{
		if (
			!Run(() =>
				_watchlistId = _watchlistManager
					.SaveName(
						new SaveWatchlistNameRequest { Name = _watchlistManager.GetNewName() }
					)
					.Id
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
		// Deleting the shown watchlist shows the active one
		var id = WatchlistHelpers.ResolveShownId(
			_watchlistId,
			_watchlistManager.GetActive().Id,
			_watchlistManager.GetWatchlists()
		);
		var reload = id != _watchlistId;
		_watchlistId = id;
		LoadWatchlists();
		if (reload)
			LoadRows();
	}

	[RelayCommand]
	private void CancelDelete(WatchlistItemViewModel item) => item.IsConfirmingDelete = false;

	// Watchlist changes report problems (taken name, last list) in the widget's error line
	private bool Run(Action change)
	{
		_changing = true;
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
		finally
		{
			_changing = false;
		}
	}

	private void LoadWatchlists()
	{
		Watchlists.Clear();
		foreach (var watchlist in _watchlistManager.GetWatchlists())
			Watchlists.Add(
				new WatchlistItemViewModel(this, watchlist, watchlist.Id == _watchlistId)
			);
		ActiveWatchlist = Watchlists.FirstOrDefault(x => x.IsActive);
		OnPropertyChanged(nameof(CanDelete));
	}

	// The shown watchlist's rows, streaming when connected
	private void LoadRows()
	{
		foreach (var row in Rows)
			Stop(row);
		Rows.Clear();
		_savedOrder.Clear();
		foreach (var contract in _watchlistManager.GetItems(_watchlistId))
		{
			var row = new WatchlistRowViewModel(contract);
			_savedOrder.Add(row);
			Rows.Add(row);
		}
		LoadColumns();
		LoadPins();
		SelectedRow = Rows.FirstOrDefault();
		if (IsConnected)
			foreach (var row in Rows)
				Start(row);
	}

	/// <summary>The watchlist's custom columns, in their order (grid columns and the header popup's list).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanAddColumn))]
	public partial IReadOnlyList<WatchlistColumnItemViewModel> CustomColumns { get; private set; } =
	[];

	/// <summary>Another custom column fits (the header popup's "+ Add column").</summary>
	public bool CanAddColumn => CustomColumns.Count < WatchlistConstants.MaxCustomColumns;

	/// <summary>An import is running (its symbols are being looked up): no second one meanwhile.</summary>
	[ObservableProperty]
	public partial bool IsImporting { get; private set; }

	/// <summary>The last import or export result ("Imported …"); errors go to ErrorMessage.</summary>
	[ObservableProperty]
	public partial string? StatusMessage { get; private set; }

	/// <summary>Adds "Column N" after the others and opens its name for editing.</summary>
	[RelayCommand]
	private void AddColumn()
	{
		string? id = null;
		if (
			!Run(() =>
				id = _watchlistManager
					.SaveColumn(
						new SaveWatchlistColumnRequest
						{
							WatchlistId = _watchlistId,
							Name = _watchlistManager.GetNewColumnName(_watchlistId),
						}
					)
					.Id
			)
		)
			return;
		LoadColumns();
		if (CustomColumns.FirstOrDefault(x => x.Id == id) is { } item)
			BeginRenameColumn(item);
	}

	[RelayCommand]
	private void BeginRenameColumn(WatchlistColumnItemViewModel item)
	{
		foreach (var column in CustomColumns)
			column.IsConfirmingDelete = column.IsRenaming = false;
		item.RenameText = item.Name;
		item.IsRenaming = true;
	}

	/// <summary>Saves the typed column name; on an error (blank, taken) the name box stays open.</summary>
	[RelayCommand]
	private void CommitRenameColumn(WatchlistColumnItemViewModel item)
	{
		if (!item.IsRenaming)
			return;
		// Closed first: hiding the box fires its LostFocus, which must not commit again
		item.IsRenaming = false;
		if (item.RenameText.Trim() == item.Name)
			return;
		if (
			Run(() =>
				_watchlistManager.SaveColumn(
					new SaveWatchlistColumnRequest
					{
						WatchlistId = _watchlistId,
						ColumnId = item.Id,
						Name = item.RenameText,
					}
				)
			)
		)
			LoadColumns();
		else
			item.IsRenaming = true;
	}

	[RelayCommand]
	private void CancelRenameColumn(WatchlistColumnItemViewModel item)
	{
		item.IsRenaming = false;
		ErrorMessage = null;
	}

	[RelayCommand]
	private void AskDeleteColumn(WatchlistColumnItemViewModel item)
	{
		foreach (var column in CustomColumns)
			column.IsConfirmingDelete = column.IsRenaming = false;
		item.IsConfirmingDelete = true;
	}

	[RelayCommand]
	private void ConfirmDeleteColumn(WatchlistColumnItemViewModel item)
	{
		if (Run(() => _watchlistManager.DeleteColumn(_watchlistId, item.Id)))
			LoadColumns();
	}

	[RelayCommand]
	private void CancelDeleteColumn(WatchlistColumnItemViewModel item) =>
		item.IsConfirmingDelete = false;

	/// <summary>
	/// A custom cell's edit was committed: saves the value (blank clears it) and re-sorts when
	/// sorted by that column. A refused value (too long) leaves the cell as it was.
	/// </summary>
	public void SetCell(WatchlistRowViewModel row, string columnId, string? text)
	{
		if (row.Cells.FirstOrDefault(x => x.ColumnId == columnId) is not { } cell)
			return;
		var value = text?.Trim() ?? string.Empty;
		if (value == cell.Text)
			return;
		if (
			!Run(() =>
				_watchlistManager.SetCell(
					new SetWatchlistCellRequest
					{
						WatchlistId = _watchlistId,
						ColumnId = columnId,
						Contract = row.Contract,
						Value = value,
					}
				)
			)
		)
		{
			cell.EditText = cell.Text;
			return;
		}
		cell.Text = cell.EditText = value;
		if (SortColumn == WatchlistSortColumn.Custom && SortCustomColumnId == columnId)
			ApplyOrder();
	}

	// The custom columns and every row's values in them; the column list is replaced only when
	// columns were added, renamed, moved or removed (the grid rebuilds its columns then)
	private void LoadColumns()
	{
		var columns = _watchlistManager.GetColumns(_watchlistId);
		if (
			!WatchlistHelpers.IsSameColumns(columns, [.. CustomColumns.Select(x => (x.Id, x.Name))])
			|| Columns.Count == 0
		)
		{
			CustomColumns = [.. columns.Select(x => new WatchlistColumnItemViewModel(this, x))];
			RebuildColumnOptions();
			ApplyHiddenColumns();
		}
		foreach (var row in _savedOrder)
		{
			var values = columns.Select(x =>
				(x.Id, Value: WatchlistHelpers.GetCellValue(x, row.Contract))
			);
			if (row.Cells.Select(x => x.ColumnId).SequenceEqual(columns.Select(x => x.Id)))
			{
				foreach (var (cell, value) in row.Cells.Zip(values))
					if (cell.Text != value.Value)
						cell.Text = cell.EditText = value.Value;
			}
			else
				row.Cells = [.. values.Select(x => new WatchlistCellViewModel(x.Id, x.Value))];
		}
		if (SortColumn != WatchlistSortColumn.Custom)
			return;
		// A deleted sorted column (or one of another watchlist): back to the saved order
		if (columns.All(x => x.Id != SortCustomColumnId))
		{
			SortColumn = WatchlistSortColumn.None;
			SortCustomColumnId = null;
			SortDirection = ListSortDirection.Ascending;
		}
		ApplyOrder();
	}

	/// <summary>The shown watchlist as a JSON file (export).</summary>
	public string ExportWatchlist() => _watchlistManager.Export(_watchlistId);

	/// <summary>A file name to suggest for exporting the shown watchlist.</summary>
	public string GetExportFileName() =>
		WatchlistFileHelpers.GetFileName(ActiveWatchlist?.Name ?? string.Empty);

	/// <summary>The export was written (or failed, with the reason).</summary>
	public void OnExported(string fileName, Exception? error)
	{
		if (error != null)
		{
			StatusMessage = null;
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(error);
			return;
		}
		ErrorMessage = null;
		StatusMessage = $"Exported {ActiveWatchlist?.Name} to {fileName}";
	}

	/// <summary>A file that couldn't be read.</summary>
	public void ShowFileError(string message)
	{
		StatusMessage = null;
		ErrorMessage = message;
	}

	/// <summary>
	/// Adds the file's watchlist (an export, or a list of symbols looked up on IB) and shows it.
	/// A list of symbols, or an export with items without a conId (looked up by their fields),
	/// needs the connection.
	/// </summary>
	public async Task ImportAsync(string content, string fileName)
	{
		if (IsImporting)
			return;
		StatusMessage = null;
		ErrorMessage = null;
		var needsLookup = WatchlistFileHelpers.NeedsLookup(content);
		if (needsLookup && !IsConnected)
		{
			ErrorMessage = WatchlistValidationCodes.WVC_020.GetValidationMessage();
			return;
		}
		IsImporting = true;
		if (needsLookup)
			StatusMessage = $"Importing {fileName}…";
		try
		{
			var result = await _watchlistManager.ImportAsync(
				content,
				fileName,
				needsLookup ? _session : CancellationToken.None
			);
			// Shown here, and where the next new watchlist widget opens (the manager made it active)
			_watchlistId = result.Watchlist.Id;
			LoadWatchlists();
			LoadRows();
			StatusMessage =
				$"Imported {result.Watchlist.Name} ("
				+ (result.Watchlist.Count == 1 ? "1 symbol" : $"{result.Watchlist.Count} symbols")
				+ ")"
				+ (
					result.NotFound.Count > 0
						? $"; not found: {WatchlistFileHelpers.JoinSymbols(result.NotFound)}"
						: string.Empty
				);
		}
		catch (OperationCanceledException)
		{
			StatusMessage = null;
		}
		catch (Exception ex)
		{
			StatusMessage = null;
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			IsImporting = false;
		}
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
		_flushTimer.Stop();
		_resortTimer.Stop();
		_settingsManager.AppSettingsChanged -= OnAppSettingsChanged;
		_watchlistManager.Changed -= OnWatchlistsChanged;
		foreach (var row in Rows)
			Stop(row);
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
			_changing = true;
			_watchlistManager.Add(_watchlistId, contract);
			var row = new WatchlistRowViewModel(contract)
			{
				Cells =
				[
					.. CustomColumns.Select(x => new WatchlistCellViewModel(x.Id, string.Empty)),
				],
			};
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
		finally
		{
			_changing = false;
		}
	}

	[RelayCommand]
	private void Remove(WatchlistRowViewModel? row)
	{
		row ??= SelectedRow;
		if (row == null)
			return;

		Stop(row);
		_changing = true;
		try
		{
			_watchlistManager.Remove(_watchlistId, row.Contract);
		}
		finally
		{
			_changing = false;
		}
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
		var owner = ActiveWatchlist is { } watchlist
			? $"{MarketDataStreamOwners.Watchlist} · {watchlist.Name}"
			: MarketDataStreamOwners.Watchlist;
		_ = Task.Run(
			async () =>
			{
				try
				{
					await foreach (
						var quote in _marketDataManager
							.SubscribeQuotesAsync(row.Contract, token, owner: owner)
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
