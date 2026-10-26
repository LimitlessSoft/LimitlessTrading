using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LT.App.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App;

public class App : Application
{
	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow
			{
				DataContext = Program.Services.GetRequiredService<MainWindowViewModel>(),
			};
			// Extra workspace windows aren't owned by the main window; closing it still exits
			desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
		}

		base.OnFrameworkInitializationCompleted();
	}
}
