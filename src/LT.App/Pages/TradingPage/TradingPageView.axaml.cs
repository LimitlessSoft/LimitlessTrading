using Avalonia.Controls;
using Avalonia.Threading;
using LT.App.Widgets.Watchlist;
using LT.Contracts.Enums;

namespace LT.App.Pages.TradingPage;

public partial class TradingPageView : UserControl
{
	public TradingPageView() => InitializeComponent();

	public void FocusSearch()
	{
		(DataContext as TradingPageViewModel)?.ShowTool(WorkspaceTool.Watchlist);
		// The watchlist may have just been spawned or expanded; focus once it is laid out
		Dispatcher.UIThread.Post(
			() =>
				(Workspace.GetWidgetView(WorkspaceTool.Watchlist) as WatchlistView)?.FocusSearch(),
			DispatcherPriority.Loaded
		);
	}

	/// <summary>Copies the live layout (panel widths, floating bounds) into the view model.</summary>
	public void StoreLayout() => Workspace.StoreLayout();
}
