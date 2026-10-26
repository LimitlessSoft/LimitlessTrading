using Avalonia;
using LSCore.DependencyInjection;
using LT.App.Pages.TradingPage;
using LT.App.Services;
using LT.App.Shell;
using LT.App.Stores;
using LT.App.Widgets.ConnectionBar;
using LT.App.Widgets.Gateway;
using LT.App.Widgets.Settings;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;
using LT.Contracts.Interfaces.IProcesses;
using LT.Contracts.Interfaces.IRepositories;
using LT.Domain.Managers;
using LT.Domain.NewsSources;
using LT.Ibkr;
using LT.Ibkr.Gateway;
using LT.Repository.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LT.App;

public static class Program
{
	public static IServiceProvider Services { get; private set; } = null!;

	[STAThread]
	public static void Main(string[] args)
	{
		LegacyIbcCleanup.Run();
		using var host = BuildHost(args);
		Services = host.Services;
		host.Start();

		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

		// Off the UI thread: its dispatcher has stopped, so awaits that resume there would never
		// continue (closing froze). Bounded, so a stuck disconnect can't keep the process alive.
		Task.Run(() => ShutdownAsync(host)).Wait(AppConstants.ShutdownTimeout);
	}

	private static async Task ShutdownAsync(IHost host)
	{
		await host.StopAsync();
		await host.Services.GetRequiredService<IConnectionManager>().ShutdownAsync();
		// The Gateway we started keeps running after the app closes (the next Log in reuses it, no
		// new login) unless the user turned on stopping it; never while restarting into an update
		var restarting =
			host.Services.GetRequiredService<IUpdateManager>().Status.State
			== UpdateState.Restarting;
		if (
			!restarting
			&& host.Services.GetRequiredService<ISettingsManager>()
				.GetAppSettings()
				.StopGatewayOnExit
		)
			await host.Services.GetRequiredService<IGatewayManager>().StopAsync();
	}

	// Also used by the Avalonia previewer in the IDE
	public static AppBuilder BuildAvaloniaApp() =>
		AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();

	private static IHost BuildHost(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);

		// Registers every Foo : IFoo from LT.* assemblies as transient, plus mappers and validators
		builder.AddLSCoreDependencyInjection("LT");
		builder.Services.AddSingleton(TimeProvider.System);

		// Stateful services: registered after the scan so the singleton registration wins
		builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
		builder.Services.AddSingleton<ICacheRepository, FileCacheRepository>();
		builder.Services.AddSingleton<IAccountHistoryRepository, AccountHistoryRepository>();
		builder.Services.AddSingleton<IIbkrApiClient, IbkrApiClient>();
		builder.Services.AddSingleton<IMessageLogManager, MessageLogManager>();
		builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
		builder.Services.AddSingleton<IGatewayProcess, GatewayProcess>();
		builder.Services.AddSingleton<IGatewayManager, GatewayManager>();
		builder.Services.AddSingleton<IMarketDataManager, MarketDataManager>();
		builder.Services.AddSingleton<ISettingsManager, SettingsManager>();
		builder.Services.AddSingleton<IAppUpdater, AppUpdater>();
		builder.Services.AddSingleton<IUpdateManager, UpdateManager>();
		builder.Services.AddSingleton<IAccountPerformanceManager, AccountPerformanceManager>();

		// News: every INewsSource is aggregated by the news manager (more sources go here)
		builder.Services.AddSingleton<INewsSource, IbkrNewsSource>();
		builder.Services.AddSingleton<INewsManager, NewsManager>();

		// App-wide UI state shared between widgets (the zStore of this app)
		builder.Services.AddSingleton<AccountStore>();
		builder.Services.AddSingleton<LinkGroupStore>();
		builder.Services.AddSingleton<AppearanceStore>();

		// Creates widget view models per spawned instance (several charts)
		builder.Services.AddSingleton<WidgetViewModelFactory>();

		builder.Services.AddHostedService<ConnectionWatchdog>();
		builder.Services.AddHostedService<UpdateScheduler>();
		builder.Services.AddHostedService<CacheMaintenance>();
		builder.Services.AddHostedService<AccountPerformanceRecorder>();

		// ViewModels have no interfaces, so the convention scan skips them
		builder.Services.AddTransient<MainWindowViewModel>();
		builder.Services.AddTransient<UpdateViewModel>();
		builder.Services.AddTransient<ConnectionBarViewModel>();
		builder.Services.AddTransient<TradingPageViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<GatewayViewModel>();
		foreach (var widget in WidgetCatalog.Definitions)
			builder.Services.AddTransient(widget.ViewModelType);

		var host = builder.Build();
		host.UseLSCoreDependencyInjection();
		return host;
	}
}
