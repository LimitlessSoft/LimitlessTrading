using Avalonia;
using LSCore.DependencyInjection;
using LT.App.Pages.TradingPage;
using LT.App.Services;
using LT.App.Shell;
using LT.App.Stores;
using LT.App.Widgets.ConnectionBar;
using LT.App.Widgets.Gateway;
using LT.App.Widgets.PatchNotes;
using LT.App.Widgets.PriceAlerts;
using LT.App.Widgets.Settings;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.ICalendarSources;
using LT.Contracts.Interfaces.IEarningsResultSources;
using LT.Contracts.Interfaces.IEngines;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.INewsSources;
using LT.Contracts.Interfaces.IProcesses;
using LT.Contracts.Interfaces.IRepositories;
using LT.Domain.CalendarSources;
using LT.Domain.EarningsResultSources;
using LT.Domain.Managers;
using LT.Domain.NewsSources;
using LT.Ibkr;
using LT.Ibkr.Flex;
using LT.Ibkr.Gateway;
using LT.Indicators.Scripting;
using LT.Nasdaq;
using LT.Repository.Repositories;
using LT.Sec;
using LT.Yahoo;
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
		builder.Services.AddSingleton<INotesRepository, NotesRepository>();
		// One lock and one record of what each footprint trade file covers, for every chart
		builder.Services.AddSingleton<IFootprintTapeRepository, FootprintTapeRepository>();
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
		// One HttpClient and one pacing state for every Nasdaq calendar request
		builder.Services.AddSingleton<INasdaqApiClient>(services => new NasdaqApiClient(
			NasdaqApiClient.CreateHttpClient(),
			services.GetRequiredService<TimeProvider>()
		));
		// One HttpClient and one pacing state for every Yahoo Finance request
		builder.Services.AddSingleton<IYahooApiClient>(services => new YahooApiClient(
			YahooApiClient.CreateHttpClient(),
			services.GetRequiredService<TimeProvider>()
		));
		builder.Services.AddSingleton<IMessageLogManager, MessageLogManager>();
		builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
		builder.Services.AddSingleton<IGatewayProcess, GatewayProcess>();
		builder.Services.AddSingleton<IGatewayManager, GatewayManager>();
		builder.Services.AddSingleton<IMarketDataManager, MarketDataManager>();
		builder.Services.AddSingleton<IMarketDepthManager, MarketDepthManager>();
		// Calendar: every ICalendarSource is merged by the calendar manager, in this order (the
		// first source reporting an event is the one shown); each keeps its own answers
		builder.Services.AddSingleton<ICalendarSource, WshCalendarSource>();
		builder.Services.AddSingleton<ICalendarSource, NasdaqCalendarSource>();
		builder.Services.AddSingleton<ICalendarManager, CalendarManager>();
		// Earnings results for the calendar's rows without one, fastest source first (the first
		// result per symbol and day is kept); each keeps its own answers and interval
		builder.Services.AddSingleton<IEarningsResultSource, SecEarningsResultSource>();
		builder.Services.AddSingleton<IEarningsResultSource, YahooEarningsResultSource>();
		builder.Services.AddSingleton<IEarningsResultSource, NasdaqEarningsResultSource>();
		builder.Services.AddSingleton<IEarningsResultManager, EarningsResultManager>();
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
		// One list of price alerts for every chart and the watcher (each fires once)
		builder.Services.AddSingleton<IPriceAlertRepository, PriceAlertRepository>();
		builder.Services.AddSingleton<IPriceAlertManager, PriceAlertManager>();
		// One list of drawings per contract for every chart of it
		builder.Services.AddSingleton<IChartDrawingRepository, ChartDrawingRepository>();
		builder.Services.AddSingleton<IChartDrawingManager, ChartDrawingManager>();
		// The fired alerts waiting in the popup and the one alarm repeating for them
		builder.Services.AddSingleton<IPriceAlertAlarmManager, PriceAlertAlarmManager>();
		builder.Services.AddSingleton<IFlexImportManager, FlexImportManager>();
		// Shares one SEC lookup per ticker between watchlists
		builder.Services.AddSingleton<ISharesOutstandingManager, SharesOutstandingManager>();
		// Holds the quick exits still working, app-wide (every Positions widget)
		builder.Services.AddSingleton<IOrderManager, OrderManager>();
		// One Changed event for every watchlist widget, so two showing one list stay in step
		builder.Services.AddSingleton<IWatchlistManager, WatchlistManager>();
		// One copy of the notes and one pending save for every Notes widget (flushed on exit)
		builder.Services.AddSingleton<INotesManager, NotesManager>();

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
		// The drawing copied on one chart, pasted on any
		builder.Services.AddSingleton<ChartDrawingClipboardStore>();
		builder.Services.AddSingleton<AppearanceStore>();
		builder.Services.AddSingleton<DisplayTimeStore>();
		builder.Services.AddSingleton<PrivacyStore>();
		builder.Services.AddSingleton<SettingsNavigator>();
		builder.Services.AddSingleton<OrderTicketNavigator>();
		// One reading of the working orders for every chart's order lines
		builder.Services.AddSingleton<OpenOrdersStore>();

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
		builder.Services.AddHostedService<PriceAlertService>();
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
		builder.Services.AddTransient<PriceAlertPopupViewModel>();
		foreach (var widget in WidgetCatalog.Definitions)
			builder.Services.AddTransient(widget.ViewModelType);

		var host = builder.Build();
		host.UseLSCoreDependencyInjection();
		return host;
	}
}
