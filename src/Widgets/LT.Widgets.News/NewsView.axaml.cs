using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace LT.App.Widgets.News;

public partial class NewsView : UserControl
{
	public NewsView() => InitializeComponent();

	// A document article (PDF …) is written to a temp file and opened with the OS
	private async void OnOpenDocumentClick(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not NewsViewModel viewModel || viewModel.SaveDocument() is not { } path)
			return;
		try
		{
			if (
				TopLevel.GetTopLevel(this)?.Launcher is not { } launcher
				|| !await launcher.LaunchFileInfoAsync(new FileInfo(path))
			)
				viewModel.ShowDocumentError($"Couldn't open the document; it is saved as {path}.");
		}
		catch (Exception ex)
		{
			viewModel.ShowDocumentError(ex.Message);
		}
	}

	// The story on the web, in the system's browser
	private async void OnOpenInBrowserClick(object? sender, RoutedEventArgs e)
	{
		if (
			DataContext is not NewsViewModel viewModel
			|| !Uri.TryCreate(viewModel.ArticleUrl, UriKind.Absolute, out var uri)
		)
			return;
		try
		{
			if (
				TopLevel.GetTopLevel(this)?.Launcher is not { } launcher
				|| !await launcher.LaunchUriAsync(uri)
			)
				viewModel.ShowDocumentError($"Couldn't open the browser; the link is {uri}.");
		}
		catch (Exception ex)
		{
			viewModel.ShowDocumentError(ex.Message);
		}
	}
}
