using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.OptionChain;

public partial class OptionChainView : UserControl
{
	// Pixels per wheel notch, and how much of the visible strip an arrow click moves
	private const double WheelStep = 60;
	private const double ArrowPage = 0.8;

	private OptionChainViewModel? _viewModel;

	public OptionChainView()
	{
		InitializeComponent();
		ExpirationScroll.ScrollChanged += (_, _) => UpdateArrows();
		ExpirationScroll.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
		ScrollLeftButton.Click += (_, _) => ScrollBy(-ExpirationScroll.Viewport.Width * ArrowPage);
		ScrollRightButton.Click += (_, _) => ScrollBy(ExpirationScroll.Viewport.Width * ArrowPage);
		ColumnsButton.Click += (_, _) => OpenColumnMenu(ColumnsButton, showAtPointer: false);
		StrikesGrid.ContextRequested += OnGridContextRequested;
	}

	// Rows above the old ATM may have been removed or inserted: keep the new ATM row in sight
	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_viewModel != null)
		{
			_viewModel.Recentered -= OnRecentered;
			_viewModel.ColumnVisibilityChanged -= ApplyColumnVisibility;
		}
		_viewModel = DataContext as OptionChainViewModel;
		if (_viewModel != null)
		{
			_viewModel.Recentered += OnRecentered;
			_viewModel.ColumnVisibilityChanged += ApplyColumnVisibility;
		}
		ApplyColumnVisibility();
	}

	// A hidden quote column is off the grid on both sides; the strike (no tag) always shows
	private void ApplyColumnVisibility()
	{
		if (_viewModel is not { } viewModel)
			return;
		foreach (var column in StrikesGrid.Columns)
			if (column.Tag is OptionQuoteColumn quoteColumn)
				column.IsVisible = viewModel.IsColumnShown(quoteColumn);
	}

	// Right-clicking a column header opens the column menu; rows have no menu
	private void OnGridContextRequested(object? sender, ContextRequestedEventArgs e)
	{
		if (
			e.Source is not Visual source
			|| source.FindAncestorOfType<DataGridColumnHeader>(true) is not { } header
		)
			return;
		e.Handled = true;
		OpenColumnMenu(header, showAtPointer: true);
	}

	// One check box per quote column, each hiding or showing it on both sides
	private void OpenColumnMenu(Control target, bool showAtPointer)
	{
		if (_viewModel is not { } viewModel)
			return;
		var menu = new MenuFlyout();
		menu.Items.Add(
			new MenuItem
			{
				Header = "Strike",
				ToggleType = MenuItemToggleType.CheckBox,
				IsChecked = true,
				IsEnabled = false,
			}
		);
		foreach (var column in OptionChainHelpers.QuoteColumns)
		{
			var shown = viewModel.IsColumnShown(column);
			var item = new MenuItem
			{
				Header = $"{OptionChainHelpers.GetColumnName(column)} (calls and puts)",
				ToggleType = MenuItemToggleType.CheckBox,
				IsChecked = shown,
			};
			item.Click += (_, _) => viewModel.SetColumnShown(column, !shown);
			menu.Items.Add(item);
		}
		menu.ShowAt(target, showAtPointer);
	}

	private void OnRecentered(OptionRowViewModel atm) => StrikesGrid.ScrollIntoView(atm, null);

	// The strip only scrolls sideways: a vertical wheel moves it too (up = earlier)
	private void OnWheel(object? sender, PointerWheelEventArgs e)
	{
		var delta = e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;
		ScrollBy(-delta * WheelStep);
		e.Handled = true;
	}

	private void ScrollBy(double pixels)
	{
		var max = Math.Max(0, ExpirationScroll.Extent.Width - ExpirationScroll.Viewport.Width);
		ExpirationScroll.Offset = ExpirationScroll.Offset.WithX(
			Math.Clamp(ExpirationScroll.Offset.X + pixels, 0, max)
		);
	}

	// An arrow shows only while there is more to scroll to on its side
	private void UpdateArrows()
	{
		var max = ExpirationScroll.Extent.Width - ExpirationScroll.Viewport.Width;
		ScrollLeftButton.IsVisible = ExpirationScroll.Offset.X > 1;
		ScrollRightButton.IsVisible = ExpirationScroll.Offset.X < max - 1;
	}
}
