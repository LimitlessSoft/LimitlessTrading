using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.News;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The News widget without IB: the app's own source ("lt", always available) shows headlines while
/// disconnected, IB's providers come with the connection and go with it, default-off providers
/// start unselected, and "Matches only" keeps the headlines matching the alert keywords.
/// </summary>
public class NewsViewModelSourcesTests : IDisposable
{
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new();
	private readonly FakeConnectionManager _connection = new();
	private readonly FakeNewsSource _feeds = new("lt");
	private readonly FakeNewsSource _ib = new("ibkr");
	private readonly LinkGroupStore _links = new();
	private readonly TestNewsArchive _archive = new();
	private readonly SettingsManager _settings;
	private readonly NewsViewModel _news;

	public NewsViewModelSourcesTests()
	{
		LSCoreContainer.EnsureInitialized();
		_settings = new SettingsManager(_cache.Settings, _ibkr);
		var cnbc = _feeds.Provider("CNBC");
		cnbc.SelectedByDefault = false;
		_feeds.Providers = [_feeds.Provider("SEC"), cnbc];
		_feeds.History =
		[
			Headline(_feeds, "SEC", "1", 1, "Shutterstock 8-K: Officers / directors"),
			Headline(_feeds, "SEC", "2", 2, "Buyback approved"),
		];
		_ib.Providers = [_ib.Provider("BRFG")];
		_ib.IsAvailable = false;
		_links.Set(LinkGroup.Red, Aapl);
		_news = TestUiThread.Invoke(CreateNews);
	}

	[Fact]
	public async Task Headlines_show_while_disconnected()
	{
		await WaitFor(() => _news.Rows.Count == 2);

		Assert.False(_news.IsConnected);
		Assert.Equal(["SEC", "CNBC"], _news.Providers.Select(x => x.Label));
		Assert.Equal("AAPL", _feeds.HeadlineRequests[^1].Contract!.Symbol);
		Assert.Null(_news.ErrorMessage);
	}

	[Fact]
	public async Task Providers_off_by_default_start_unselected()
	{
		await WaitFor(() => _news.Providers.Count == 2);

		Assert.True(_news.Providers.Single(x => x.Label == "SEC").IsSelected);
		Assert.False(_news.Providers.Single(x => x.Label == "CNBC").IsSelected);
		Assert.Equal(["SEC"], _feeds.HeadlineRequests[^1].Providers.Select(x => x.Code));
	}

	[Fact]
	public async Task Ib_providers_come_with_the_connection_and_go_with_it()
	{
		await WaitFor(() => _news.Providers.Count == 2);

		_ib.IsAvailable = true;
		_connection.SetStatus(ConnectionState.Connected, "U1");
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		await WaitFor(() => _news.Providers.Count == 3);
		Assert.Equal(["SEC", "CNBC", "BRFG"], _news.Providers.Select(x => x.Label));

		_ib.IsAvailable = false;
		_connection.SetStatus(ConnectionState.Disconnected);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		await WaitFor(() => _news.Providers.Count == 2);
	}

	[Fact]
	public async Task Only_matches_keeps_the_headlines_with_an_alert_keyword()
	{
		await WaitFor(() => _news.Rows.Count == 2);
		var settings = _settings.GetAppSettings();
		_settings.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = settings.MarketDataType,
				HeartbeatSeconds = settings.HeartbeatSeconds,
				NewsFeeds = true,
				NewsAlertKeywords = "buyback",
			}
		);
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		await WaitFor(() => _news.Rows.Any(x => x.IsMatch));

		TestUiThread.Invoke(() => _news.OnlyMatches = true);

		Assert.Equal(["2"], _news.Rows.Select(x => x.Headline.ArticleId));
		var placement = new WidgetPlacementDto();
		_news.SaveState(placement);
		Assert.True(placement.NewsOnlyMatches);
	}

	[Fact]
	public async Task A_chip_choice_is_saved_with_a_marker_for_the_source()
	{
		await WaitFor(() => _news.Providers.Count == 2);

		TestUiThread.Invoke(() => _news.Providers.Single(x => x.Label == "SEC").IsSelected = false);
		var placement = new WidgetPlacementDto();
		_news.SaveState(placement);

		Assert.Equal(["lt:"], placement.NewsProviders);
	}

	public void Dispose()
	{
		TestUiThread.Invoke(_news.Dispose);
		_archive.Dispose();
		GC.SuppressFinalize(this);
	}

	private static NewsHeadlineDto Headline(
		FakeNewsSource source,
		string provider,
		string id,
		int minute,
		string text
	)
	{
		var headline = source.Headline(provider, id, minute);
		headline.Headline = text;
		headline.Symbol = "AAPL";
		return headline;
	}

	private static async Task WaitFor(Func<bool> condition)
	{
		for (var i = 0; i < 300 && !condition(); i++)
			await Task.Delay(10, TestContext.Current.CancellationToken);
		Assert.True(condition());
	}

	private NewsViewModel CreateNews()
	{
		var contracts = new ContractManager(_ibkr, _cache.Settings, _cache.Manager);
		return new NewsViewModel(
			new NewsManager([_ib, _feeds], _archive.Manager, _cache.Time),
			contracts,
			_connection,
			new NewsAlertManager(
				_settings,
				new WatchlistManager(_cache.Settings, contracts),
				new FakeSoundPlayer(),
				_cache.Time
			),
			_settings,
			_links,
			new DisplayTimeStore(_settings, _cache.Time)
		);
	}
}
