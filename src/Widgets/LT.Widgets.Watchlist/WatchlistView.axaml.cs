using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LT.Contracts.Constants;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Watchlist;

public partial class WatchlistView : UserControl
{
	private WatchlistViewModel? _subscribed;

	// The rows on screen are reported to the view model once per layout burst
	private bool _shownRowsPosted;
	private DataGridRowsPresenter? _rowsPresenter;

	public WatchlistView()
	{
		InitializeComponent();
		SearchBox.Suggest = (text, ct) =>
			ViewModel?.GetSuggestionsAsync(text, ct) ?? Task.FromResult(Enumerable.Empty<object>());
		SearchBox.Picked += item =>
		{
			SearchBox.Reset();
			ViewModel?.AddContract(item.Contract);
		};
		SearchBox.Entered += text =>
		{
			SearchBox.Reset();
			_ = ViewModel?.AddTickerAsync(text);
		};
		SearchBox.Cancelled += () => SearchBox.Reset();
		Grid.Loaded += (_, _) => UpdateSortGlyphs();
		// Rows realized, recycled or moved (scrolling, sorting, resizing) show up as layout passes;
		// a hidden or closed widget reports none, so its rows give their lines back
		Grid.LayoutUpdated += (_, _) => PostShownRows();
		Grid.DetachedFromVisualTree += (_, _) =>
		{
			_rowsPresenter = null;
			PostShownRows();
		};
		// Tunnel: runs before the grid's ContextMenu, which opens on the bubbling request
		Grid.AddHandler(ContextRequestedEvent, OnGridContextRequested, RoutingStrategies.Tunnel);
	}

	public void FocusSearch() => SearchBox.FocusBox();

	private WatchlistViewModel? ViewModel => DataContext as WatchlistViewModel;

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_subscribed != null)
		{
			_subscribed.PropertyChanged -= OnViewModelPropertyChanged;
			_subscribed.ColumnVisibilityChanged -= ApplyColumnVisibility;
		}
		_subscribed = ViewModel;
		if (_subscribed != null)
		{
			_subscribed.PropertyChanged += OnViewModelPropertyChanged;
			_subscribed.ColumnVisibilityChanged += ApplyColumnVisibility;
		}
		RebuildCustomColumns();
		PostShownRows();
	}

	private void PostShownRows()
	{
		if (_shownRowsPosted)
			return;
		_shownRowsPosted = true;
		Dispatcher.UIThread.Post(ReportShownRows, DispatcherPriority.Background);
	}

	// The rows the grid has laid out inside its viewport (recycled rows are hidden or outside it)
	private void ReportShownRows()
	{
		_shownRowsPosted = false;
		if (ViewModel is not { } viewModel)
			return;
		var shown = new List<WatchlistRowViewModel>();
		if (
			Grid.IsEffectivelyVisible
			&& TopLevel.GetTopLevel(Grid) != null
			&& Grid.Bounds.Height > 0
		)
		{
			_rowsPresenter ??= Grid.GetVisualDescendants()
				.OfType<DataGridRowsPresenter>()
				.FirstOrDefault();
			if (_rowsPresenter != null)
				foreach (var row in _rowsPresenter.Children.OfType<DataGridRow>())
					if (
						row.IsVisible
						&& row.DataContext is WatchlistRowViewModel item
						&& row.TranslatePoint(default, _rowsPresenter) is { } top
						&& top.Y + row.Bounds.Height > 0
						&& top.Y < _rowsPresenter.Bounds.Height
					)
						shown.Add(item);
		}
		viewModel.SetShownRows(shown);
	}

	private void OnGridKeyDown(object? sender, KeyEventArgs e)
	{
		// Delete inside a custom cell's edit box edits the text, not the watchlist
		if (e.Key != Key.Delete || e.Source is TextBox)
			return;
		ViewModel?.RemoveCommand.Execute(null);
		e.Handled = true;
	}

	// The row menu is the grid's, so it would also open on a column header or the empty area
	// below the rows; only a row opens it. A column header opens the column menu instead
	private void OnGridContextRequested(object? sender, ContextRequestedEventArgs e)
	{
		if (e.Source is not Visual source || source.FindAncestorOfType<DataGridRow>(true) != null)
			return;
		e.Handled = true;
		if (
			source.FindAncestorOfType<DataGridColumnHeader>(true) is { } header
			&& FindColumn(header) is { } column
		)
			OpenColumnMenu(header, column);
	}

	// Sort the column either way, clear the sort, hide it; below, every column to show or hide
	// (hidden ones too, so they can come back)
	private void OpenColumnMenu(Control header, DataGridColumn column)
	{
		if (ViewModel is not { } viewModel || ToColumn(column.Tag) is not (var sortColumn, var id))
			return;
		var sorted =
			viewModel.SortColumn == sortColumn
			&& (sortColumn != WatchlistSortColumn.Custom || viewModel.SortCustomColumnId == id);
		var menu = new MenuFlyout();
		menu.Items.Add(
			SortItem(
				viewModel,
				"Sort low to high",
				sortColumn,
				id,
				ListSortDirection.Ascending,
				sorted
			)
		);
		menu.Items.Add(
			SortItem(
				viewModel,
				"Sort high to low",
				sortColumn,
				id,
				ListSortDirection.Descending,
				sorted
			)
		);
		var clear = new MenuItem
		{
			Header = "Clear sort",
			IsEnabled = viewModel.SortColumn != WatchlistSortColumn.None,
		};
		clear.Click += (_, _) => viewModel.ClearSortCommand.Execute(null);
		menu.Items.Add(clear);
		var hide = new MenuItem
		{
			Header = "Hide column",
			IsEnabled = viewModel.CanToggleColumn(sortColumn, id),
		};
		hide.Click += (_, _) => viewModel.SetColumnShown(sortColumn, id, false);
		menu.Items.Add(hide);
		menu.Items.Add(new Separator());
		foreach (var option in viewModel.Columns)
		{
			var item = new MenuItem
			{
				Header = option.Name,
				ToggleType = MenuItemToggleType.CheckBox,
				IsChecked = option.IsShown,
				IsEnabled = option.CanToggle,
			};
			item.Click += (_, _) =>
				viewModel.SetColumnShown(option.Column, option.CustomColumnId, !option.IsShown);
			menu.Items.Add(item);
		}
		menu.ShowAt(header, showAtPointer: true);
	}

	private static MenuItem SortItem(
		WatchlistViewModel viewModel,
		string text,
		WatchlistSortColumn column,
		string? customColumnId,
		ListSortDirection direction,
		bool sorted
	)
	{
		var item = new MenuItem
		{
			Header = text,
			ToggleType = MenuItemToggleType.Radio,
			IsChecked = sorted && viewModel.SortDirection == direction,
		};
		item.Click += (_, _) => viewModel.SortBy(column, customColumnId, direction);
		return item;
	}

	// A grid column's tag as the column it is: built-in by its sort column, custom by its id
	private static (WatchlistSortColumn Column, string? CustomColumnId)? ToColumn(object? tag) =>
		tag switch
		{
			WatchlistSortColumn column => (column, null),
			string customColumnId => (WatchlistSortColumn.Custom, customColumnId),
			_ => null,
		};

	private DataGridColumn? FindColumn(DataGridColumnHeader header) =>
		Grid.Columns.FirstOrDefault(x =>
			x.Header != null && ReferenceEquals(x.Header, header.Content)
		);

	// Hidden columns are off the grid; the rows may still be sorted by one
	private void ApplyColumnVisibility()
	{
		if (ViewModel is not { } viewModel)
			return;
		foreach (var column in Grid.Columns)
			if (ToColumn(column.Tag) is (var sortColumn, var id))
				column.IsVisible = viewModel.IsColumnShown(sortColumn, id);
		// Shown again: its header needs its sort arrow
		Dispatcher.UIThread.Post(UpdateSortGlyphs, DispatcherPriority.Loaded);
	}

	// The grid's own sort can't keep pinned rows on top: the view model sorts instead
	private void OnGridSorting(object? sender, DataGridColumnEventArgs e)
	{
		e.Handled = true;
		if (e.Column.Tag is WatchlistSortColumn column)
			ViewModel?.SortBy(column);
		else if (e.Column.Tag is string customColumnId)
			ViewModel?.SortByCustom(customColumnId);
	}

	// A custom cell's edit box opens with its saved value, focused and selected
	private void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
	{
		if (
			e.Column.Tag is not string columnId
			|| e.Row.DataContext is not WatchlistRowViewModel row
			|| row.Cells.FirstOrDefault(x => x.ColumnId == columnId) is not { } cell
		)
			return;
		cell.EditText = cell.Text;
		if (
			e.EditingElement.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault() is
			{ } box
		)
			Dispatcher.UIThread.Post(
				() =>
				{
					box.Focus();
					box.SelectAll();
				},
				DispatcherPriority.Loaded
			);
	}

	// Enter / leaving the cell saves it; Esc leaves the saved value
	private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
	{
		if (
			e.Column.Tag is not string columnId
			|| e.Row.DataContext is not WatchlistRowViewModel row
			|| row.Cells.FirstOrDefault(x => x.ColumnId == columnId) is not { } cell
		)
			return;
		if (e.EditAction == DataGridEditAction.Commit)
			ViewModel?.SetCell(row, columnId, cell.EditText);
		else
			cell.EditText = cell.Text;
	}

	// The custom columns go after the fixed ones, tagged with their id (sorting, editing); their
	// cells bind by index into the row's Cells, in the same order
	private void RebuildCustomColumns()
	{
		// A cell still being edited belongs to a column about to go
		Grid.CancelEdit();
		foreach (var column in Grid.Columns.Where(x => x.Tag is string).ToList())
			Grid.Columns.Remove(column);
		if (ViewModel is { } viewModel)
			for (var i = 0; i < viewModel.CustomColumns.Count; i++)
				Grid.Columns.Add(CreateCustomColumn(viewModel.CustomColumns[i], i));
		// The new headers exist once the grid has laid them out (posted there)
		ApplyColumnVisibility();
	}

	private static DataGridTemplateColumn CreateCustomColumn(
		WatchlistColumnItemViewModel column,
		int index
	) =>
		new()
		{
			Header = column.Name,
			Tag = column.Id,
			Width = DataGridLength.Auto,
			MinWidth = 70,
			MaxWidth = 240,
			SortMemberPath = $"Cells[{index}].Text",
			CellTemplate = new FuncDataTemplate<WatchlistRowViewModel>(
				(_, _) =>
					new TextBlock
					{
						Margin = new Avalonia.Thickness(8, 0),
						VerticalAlignment = VerticalAlignment.Center,
						TextTrimming = TextTrimming.CharacterEllipsis,
						[!TextBlock.TextProperty] = new Binding($"Cells[{index}].Text"),
						[!ToolTip.TipProperty] = new Binding($"Cells[{index}].Text"),
					}
			),
			CellEditingTemplate = new FuncDataTemplate<WatchlistRowViewModel>(
				(_, _) =>
					new TextBox
					{
						Margin = new Avalonia.Thickness(2, 0),
						VerticalAlignment = VerticalAlignment.Center,
						MaxLength = WatchlistConstants.CellValueMaxLength,
						[!TextBox.TextProperty] = new Binding($"Cells[{index}].EditText")
						{
							Mode = BindingMode.TwoWay,
						},
					}
			),
		};

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(WatchlistViewModel.CustomColumns))
			RebuildCustomColumns();
		else if (
			e.PropertyName
			is nameof(WatchlistViewModel.SortColumn)
				or nameof(WatchlistViewModel.SortDirection)
				or nameof(WatchlistViewModel.SortCustomColumnId)
		)
			UpdateSortGlyphs();
	}

	// The grid shows its sort arrows only for its own sort; set them for the view model's
	private void UpdateSortGlyphs()
	{
		if (ViewModel is not { } viewModel)
			return;
		foreach (var header in Grid.GetVisualDescendants().OfType<DataGridColumnHeader>())
		{
			var column = FindColumn(header);
			var sorted = column?.Tag switch
			{
				WatchlistSortColumn sortColumn => sortColumn == viewModel.SortColumn,
				string customColumnId => viewModel.SortColumn == WatchlistSortColumn.Custom
					&& customColumnId == viewModel.SortCustomColumnId,
				_ => false,
			};
			IPseudoClasses classes = header.Classes;
			classes.Set(
				":sortascending",
				sorted && viewModel.SortDirection == ListSortDirection.Ascending
			);
			classes.Set(
				":sortdescending",
				sorted && viewModel.SortDirection == ListSortDirection.Descending
			);
		}
	}
}
