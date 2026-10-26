using Avalonia;
using LSCore.DependencyInjection;
using LT.App.Pages.TradingPage;
using LT.App.Services;
using LT.App.Shell;
using LT.App.Stores;
using LT.App.Widgets.ConnectionBar;
using LT.App.Widgets.Gateway;
using LT.App.Widgets.PatchNotes;
using LT.App.Widgets.Settings;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IEngines;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;
using LT.Contracts.Interfaces.IProcesses;
using LT.Contracts.Interfaces.IRepositories;
using LT.Domain.Managers;
using LT.Domain.NewsSources;
using LT.Ibkr;
using LT.Ibkr.Flex;
using LT.Ibkr.Gateway;
using LT.Indicators.Scripting;
using LT.Repository.Repositories;
using LT.Sec;
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
		builder.Services.AddSingleton<ITradeJournalRepository, TradeJournalRepository>();
		builder.Services.AddSingleton<IFlexImportStateRepository, FlexImportStateRepository>();
		builder.Services.AddSingleton<ISharesOutstandingRepository, SharesOutstandingRepository>();
		builder.Services.AddSingleton<IPatchNotesRepository, PatchNotesRepository>();
		builder.Services.AddSingleton<IIbkrApiClient, IbkrApiClient>();
		// One HttpClient and one pacing state for every Flex request (IB's limits are per token)
		builder.Services.AddSingleton<IFlexApiClient>(services => new FlexApiClient(
			FlexApiClient.CreateHttpClient(),
			services.GetRequiredService<TimeProvider>()
		));
		// One HttpClient and one pacing state for every SEC request (10 per second per client)
		builder.Services.AddSingleton<ISecApiClient>(services => new SecApiClient(
			SecApiClient.CreateHttpClient(),
			services.GetRequiredService<TimeProvider>()
		));
		builder.Services.AddSingleton<IMessageLogManager, MessageLogManager>();
		builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
		builder.Services.AddSingleton<IGatewayProcess, GatewayProcess>();
		builder.Services.AddSingleton<IGatewayManager, GatewayManager>();
		builder.Services.AddSingleton<IMarketDataManager, MarketDataManager>();
		builder.Services.AddSingleton<IMarketDepthManager, MarketDepthManager>();
		builder.Services.AddSingleton<ISettingsManager, SettingsManager>();
		builder.Services.AddSingleton<IAppUpdater, AppUpdater>();
		builder.Services.AddSingleton<IUpdateManager, UpdateManager>();
		// One cached list and one refresh at a time for every patch notes window
		builder.Services.AddSingleton<IPatchNotesManager, PatchNotesManager>();
		builder.Services.AddSingleton<IAccountPerformanceManager, AccountPerformanceManager>();
		builder.Services.AddSingleton<ITradeJournalManager, TradeJournalManager>();
		// Remembers the fills already heard of and the one sound playing, app-wide
		builder.Services.AddSingleton<ISoundPlayer, SoundPlayer>();
		builder.Services.AddSingleton<IFillAlertManager, FillAlertManager>();
		builder.Services.AddSingleton<IFlexImportManager, FlexImportManager>();
		// Shares one SEC lookup per ticker between watchlists
		builder.Services.AddSingleton<ISharesOutstandingManager, SharesOutstandingManager>();
		// Holds the quick exits still working, app-wide (every Positions widget)
		builder.Services.AddSingleton<IOrderManager, OrderManager>();
		// One Changed event for every watchlist widget, so two showing one list stay in step
		builder.Services.AddSingleton<IWatchlistManager, WatchlistManager>();

		// Chart indicators: one engine (compiled scripts, their load contexts), one catalog, one
		// watcher on the scripts folder
		builder.Services.AddSingleton<IIndicatorEngine, IndicatorEngine>();
		builder.Services.AddSingleton<IIndicatorScriptRepository, IndicatorScriptRepository>();
		builder.Services.AddSingleton<IIndicatorManager, IndicatorManager>();
		// One for all charts: those following a template hear each other's saves through it
		builder.Services.AddSingleton<IChartTemplateManager, ChartTemplateManager>();

		// News: every INewsSource is aggregated by the news manager (more sources go here)
		builder.Services.AddSingleton<INewsSource, IbkrNewsSource>();
		builder.Services.AddSingleton<INewsManager, NewsManager>();

		// App-wide UI state shared between widgets (the zStore of this app)
		builder.Services.AddSingleton<AccountStore>();
		builder.Services.AddSingleton<LinkGroupStore>();
		builder.Services.AddSingleton<AppearanceStore>();
		builder.Services.AddSingleton<DisplayTimeStore>();
		builder.Services.AddSingleton<SettingsNavigator>();
		builder.Services.AddSingleton<OrderTicketNavigator>();

		// Creates widget view models per spawned instance (several charts)
		builder.Services.AddSingleton<WidgetViewModelFactory>();
		// The main window's workspace and every extra workspace window (their view models are
		// created by it, each carrying its saved layout)
		builder.Services.AddSingleton<WorkspaceWindows>();

		builder.Services.AddHostedService<ConnectionWatchdog>();
		builder.Services.AddHostedService<UpdateScheduler>();
		builder.Services.AddHostedService<CacheMaintenance>();
		builder.Services.AddHostedService<AccountPerformanceRecorder>();
		builder.Services.AddHostedService<TradeJournalRecorder>();
		builder.Services.AddHostedService<FillAlertService>();
		builder.Services.AddHostedService<FlexImportScheduler>();
		builder.Services.AddHostedService<IndicatorScriptHost>();

		// ViewModels have no interfaces, so the convention scan skips them
		builder.Services.AddTransient<MainWindowViewModel>();
		builder.Services.AddTransient<UpdateViewModel>();
		builder.Services.AddTransient<ConnectionBarViewModel>();
		builder.Services.AddTransient<TradingPageViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<GatewayViewModel>();
		builder.Services.AddTransient<PatchNotesViewModel>();
		foreach (var widget in WidgetCatalog.Definitions)
			builder.Services.AddTransient(widget.ViewModelType);

		var host = builder.Build();
		host.UseLSCoreDependencyInjection();
		return host;
	}
}
