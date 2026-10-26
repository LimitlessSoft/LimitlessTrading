using Avalonia.Controls;
using Avalonia.Input;
using LT.App.Workspace;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Watchlist;

public class WatchlistWidget : IWidgetDefinition
{
	public WorkspaceTool Tool => WorkspaceTool.Watchlist;

	public string Title => "Watchlist";

	/// <summary>An eye.</summary>
	public string Icon =>
		"M1,12C3.5,6.5 7.5,4 12,4C16.5,4 20.5,6.5 23,12C20.5,17.5 16.5,20 12,20C7.5,20 3.5,17.5 1,12Z "
		+ "M3.3,12C5.5,7.8 8.6,6 12,6C15.4,6 18.5,7.8 20.7,12C18.5,16.2 15.4,18 12,18C8.6,18 5.5,16.2 3.3,12Z "
		+ "M12,8.5A3.5,3.5 0 1,1 12,15.5A3.5,3.5 0 1,1 12,8.5Z";

	public Key? FocusKey => Key.W;

	public Type ViewModelType => typeof(WatchlistViewModel);

	public Control CreateView() => new WatchlistView();

	public Control? CreateTitle() => new WatchlistTitleView();
}
