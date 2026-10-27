using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
		// Tunnel: runs before the grid's ContextMenu, which opens on the bubbling request
		Grid.AddHandler(ContextRequestedEvent, OnGridContextRequested, RoutingStrategies.Tunnel);
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
		UpdateSortGlyphs();
	}

	private void OnGridKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Delete)
			return;
		ViewModel?.RemoveCommand.Execute(null);
		e.Handled = true;
	}

	// The row menu is the grid's, so it would also open on a column header or the empty area
	// below the rows; only a row opens it
	private void OnGridContextRequested(object? sender, ContextRequestedEventArgs e)
	{
		if (e.Source is not Visual source || source.FindAncestorOfType<DataGridRow>(true) == null)
			e.Handled = true;
	}

	// The grid's own sort can't keep pinned rows on top: the view model sorts instead
	private void OnGridSorting(object? sender, DataGridColumnEventArgs e)
	{
		e.Handled = true;
		if (e.Column.Tag is WatchlistSortColumn column)
			ViewModel?.SortBy(column);
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (
			e.PropertyName
			is nameof(WatchlistViewModel.SortColumn)
				or nameof(WatchlistViewModel.SortDirection)
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
			var sorted =
				column?.Tag is WatchlistSortColumn sortColumn && sortColumn == viewModel.SortColumn;
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
