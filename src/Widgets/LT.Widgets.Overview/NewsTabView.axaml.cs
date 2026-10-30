using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LT.App.Widgets.Overview;

public partial class NewsTabView : UserControl
{
	public NewsTabView() => InitializeComponent();

	// The story on the web, in the system's browser
	private async void OnOpenInBrowserClick(object? sender, RoutedEventArgs e)
	{
		if (
			DataContext is not NewsTabViewModel viewModel
			|| !Uri.TryCreate(viewModel.ArticleUrl, UriKind.Absolute, out var uri)
		)
			return;
		try
		{
			if (
				TopLevel.GetTopLevel(this)?.Launcher is not { } launcher
				|| !await launcher.LaunchUriAsync(uri)
			)
				viewModel.ShowArticleError($"Couldn't open the browser; the link is {uri}.");
		}
		catch (Exception ex)
		{
			viewModel.ShowArticleError(ex.Message);
		}
	}
}
