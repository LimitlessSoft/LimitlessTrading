using Avalonia;
using LSCore.DependencyInjection;
using LT.App.Pages.TradingPage;
using LT.App.Shell;
using LT.App.Widgets.ConnectionBar;
using LT.App.Widgets.Positions;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Domain.Managers;
using LT.Ibkr;
using LT.Repository.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TwsApi;

namespace LT.App;

public static class Program
{
	public static IServiceProvider Services { get; private set; } = null!;

	[STAThread]
	public static void Main(string[] args)
	{
		using var host = BuildHost(args);
		Services = host.Services;

		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

		host.Services.GetRequiredService<IConnectionManager>()
			.DisconnectAsync()
			.GetAwaiter()
			.GetResult();
	}

	// Also used by the Avalonia previewer in the IDE
	public static AppBuilder BuildAvaloniaApp() =>
		AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();

	private static IHost BuildHost(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);

		// Registers every Foo : IFoo from LT.* assemblies as transient, plus mappers and validators
		builder.AddLSCoreDependencyInjection("LT");
		builder.Services.AddTwsApi(_ => { });

		// Stateful services: registered after the scan so the singleton registration wins
		builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
		builder.Services.AddSingleton<IIbkrApiClient, IbkrApiClient>();
		builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

		// ViewModels have no interfaces, so the convention scan skips them
		builder.Services.AddTransient<MainWindowViewModel>();
		builder.Services.AddTransient<ConnectionBarViewModel>();
		builder.Services.AddTransient<TradingPageViewModel>();
		builder.Services.AddTransient<PositionsViewModel>();

		var host = builder.Build();
		host.UseLSCoreDependencyInjection();
		return host;
	}
}
