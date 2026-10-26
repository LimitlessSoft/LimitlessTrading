using Avalonia;
using Avalonia.Controls;

namespace LT.App.Widgets.Depth;

public partial class DepthView : UserControl
{
	private DepthViewModel? _viewModel;

	public DepthView()
	{
		InitializeComponent();
		LadderScroll.SizeChanged += (_, _) => CenterSpread();
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_viewModel != null)
			_viewModel.BookStarted -= CenterSpread;
		_viewModel = DataContext as DepthViewModel;
		if (_viewModel != null)
			_viewModel.BookStarted += CenterSpread;
	}

	// The spread line in the middle of the visible ladder
	private void CenterSpread()
	{
		var top = SpreadLine.Bounds.Top + SpreadLine.Bounds.Height / 2;
		LadderScroll.Offset = new Vector(
			LadderScroll.Offset.X,
			Math.Max(0, top - LadderScroll.Viewport.Height / 2)
		);
	}
}
