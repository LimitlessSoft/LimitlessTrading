using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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
	}

	// Rows above the old ATM may have been removed or inserted: keep the new ATM row in sight
	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_viewModel != null)
			_viewModel.Recentered -= OnRecentered;
		_viewModel = DataContext as OptionChainViewModel;
		if (_viewModel != null)
			_viewModel.Recentered += OnRecentered;
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
