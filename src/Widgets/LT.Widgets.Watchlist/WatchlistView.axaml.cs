using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
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
	}

	public void FocusSearch() => SearchBox.FocusBox();

	private WatchlistViewModel? ViewModel => DataContext as WatchlistViewModel;

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_subscribed != null)
			_subscribed.PropertyChanged -= OnViewModelPropertyChanged;
		_subscribed = ViewModel;
		if (_subscribed != null)
			_subscribed.PropertyChanged += OnViewModelPropertyChanged;
		RebuildCustomColumns();
	}

	private void OnGridKeyDown(object? sender, KeyEventArgs e)
	{
		// Delete inside a custom cell's edit box edits the text, not the watchlist
		if (e.Key != Key.Delete || e.Source is TextBox)
			return;
		ViewModel?.RemoveCommand.Execute(null);
		e.Handled = true;
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
		// The new headers exist once the grid has laid them out
		Dispatcher.UIThread.Post(UpdateSortGlyphs, DispatcherPriority.Loaded);
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
			var column = Grid.Columns.FirstOrDefault(x =>
				x.Header != null && ReferenceEquals(x.Header, header.Content)
			);
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
