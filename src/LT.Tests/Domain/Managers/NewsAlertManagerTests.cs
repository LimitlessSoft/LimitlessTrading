using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class NewsAlertManagerTests
{
	private readonly TestCache _cache = new();
	private readonly FakeSoundPlayer _sound = new();
	private readonly SettingsManager _settings;
	private readonly WatchlistManager _watchlists;
	private readonly NewsAlertManager _alerts;

	public NewsAlertManagerTests()
	{
		_settings = new SettingsManager(_cache.Settings, new FakeIbkrApiClient());
		_watchlists = new WatchlistManager(
			_cache.Settings,
			new ContractManager(new FakeIbkrApiClient(), _cache.Settings, _cache.Manager)
		);
		_alerts = new NewsAlertManager(_settings, _watchlists, _sound, _cache.Time);
	}

	private NewsHeadlineDto Headline(string text, int minutesAgo = 1, params string[] symbols) =>
		new()
		{
			SourceId = "lt",
			ProviderCode = "PRN",
			ArticleId = text,
			Time = _cache.Time.GetUtcNow().AddMinutes(-minutesAgo),
			Headline = text,
			Symbol = symbols.FirstOrDefault() ?? string.Empty,
			Symbols = symbols,
		};

	private void Save(string keywords, bool watchlist = false, bool sound = true)
	{
		var settings = _settings.GetAppSettings();
		_settings.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = settings.MarketDataType,
				HeartbeatSeconds = settings.HeartbeatSeconds,
				NewsAlertKeywords = keywords,
				NewsAlertWatchlist = watchlist,
				NewsAlertSound = sound,
			}
		);
	}

	[Fact]
	public void Without_rules_nothing_matches()
	{
		Assert.False(_alerts.HasRules);
		Assert.False(_alerts.Matches(Headline("FDA approval")));
	}

	[Fact]
	public void Saved_keywords_apply_at_once_and_say_the_rules_changed()
	{
		var changed = 0;
		_alerts.RulesChanged += () => changed++;

		Save("fda approval, buyback");

		Assert.Equal(1, changed);
		Assert.True(_alerts.Matches(Headline("Drug wins FDA approval")));
		Assert.False(_alerts.Matches(Headline("Quiet day")));
	}

	[Fact]
	public void Watchlist_symbols_match_when_that_is_on_and_follow_the_watchlists()
	{
		Save(string.Empty, watchlist: true);
		Assert.False(_alerts.Matches(Headline("x", 1, "KMB")));

		_watchlists.Add(
			_watchlists.GetActive().Id,
			new ContractDto
			{
				ConId = 1,
				Symbol = "KMB",
				SecurityType = "STK",
			}
		);

		Assert.True(_alerts.Matches(Headline("x", 1, "KMB")));
	}

	[Fact]
	public void One_sound_for_new_matching_headlines_none_for_old_ones_or_with_the_sound_off()
	{
		Save("buyback");

		Assert.Equal(
			2,
			_alerts.OnHeadlines([Headline("buyback 1"), Headline("buyback 2"), Headline("other")])
		);
		Assert.Equal(1, _sound.NewsPlayed);

		Assert.Equal(0, _alerts.OnHeadlines([Headline("old buyback", minutesAgo: 60)]));
		Assert.Equal(1, _sound.NewsPlayed);

		Save("buyback", sound: false);
		Assert.Equal(1, _alerts.OnHeadlines([Headline("buyback 3")]));
		Assert.Equal(1, _sound.NewsPlayed);
	}

	[Fact]
	public void A_failing_player_never_throws()
	{
		Save("buyback");
		_sound.Error = new InvalidOperationException("no audio");

		Assert.Equal(0, _alerts.OnHeadlines([Headline("buyback")]));
	}
}
