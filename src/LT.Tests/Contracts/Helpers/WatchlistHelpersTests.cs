using LT.Contracts.Constants;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Contracts.Helpers;

public class WatchlistHelpersTests
{
	[Fact]
	public void A_fresh_install_starts_with_an_active_US_watchlist_of_starter_stocks()
	{
		LSCoreContainer.EnsureInitialized();
		var settings = new InMemorySettingsRepository();
		settings.Save(WatchlistHelpers.CreateFirstRunSettings());

		var manager = new WatchlistManager(settings);

		var watchlist = Assert.Single(manager.GetWatchlists());
		Assert.Equal(MarketDataConstants.StarterWatchlistName, watchlist.Name);
		Assert.Equal(
			[
				(756733, "SPY", "ARCA"),
				(272093, "MSFT", "NASDAQ"),
				(4815747, "NVDA", "NASDAQ"),
				(76792991, "TSLA", "NASDAQ"),
			],
			manager.GetItems().Select(x => (x.ConId, x.Symbol, x.PrimaryExchange))
		);
	}

	[Fact]
	public void Settings_without_watchlists_still_get_an_empty_Main_one()
	{
		var settings = new SettingsEntity();

		WatchlistHelpers.EnsureWatchlists(settings);

		var watchlist = Assert.Single(settings.Watchlists);
		Assert.Equal(
			(MarketDataConstants.DefaultWatchlistName, 0),
			(watchlist.Name, watchlist.Items.Count)
		);
	}

	[Fact]
	public void First_run_settings_connect_to_live_Gateway_with_live_market_data()
	{
		var settings = WatchlistHelpers.CreateFirstRunSettings();

		Assert.Equal(
			(
				"127.0.0.1",
				ConnectionConstants.GatewayLivePort,
				1,
				MarketDataType.Live,
				TradingMode.Live
			),
			(
				settings.Connection.Host,
				settings.Connection.Port,
				settings.Connection.ClientId,
				settings.General.MarketDataType,
				settings.Gateway.TradingMode
			)
		);
	}
}
