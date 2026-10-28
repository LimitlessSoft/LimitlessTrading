using Avalonia.Controls;
using Avalonia.Threading;

namespace LT.App.Widgets.Calendar;

public partial class CalendarView : UserControl
{
	private CalendarViewModel? _viewModel;

	public CalendarView() => InitializeComponent();

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);
		if (_viewModel != null)
			_viewModel.ScrollRequested -= OnScrollRequested;
		_viewModel = DataContext as CalendarViewModel;
		if (_viewModel != null)
			_viewModel.ScrollRequested += OnScrollRequested;
	}

	// After the new lines are laid out. Bringing the last line into view first leaves the target
	// at the top when it then comes into view, with what follows it below
	private void OnScrollRequested(CalendarItemViewModel target) =>
		Dispatcher.UIThread.Post(
			() =>
			{
				var index = EventList.Items.IndexOf(target);
				if (index < 0)
					return;
				EventList.ScrollIntoView(EventList.ItemCount - 1);
				EventList.UpdateLayout();
				EventList.ScrollIntoView(index);
			},
			DispatcherPriority.Background
		);
}
