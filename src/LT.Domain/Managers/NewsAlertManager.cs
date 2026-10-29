using LT.Contracts.Constants;
using LT.Contracts.Dtos.News;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IProcesses;

namespace LT.Domain.Managers;

/// <summary>
/// The news alert rules from Settings (keywords; symbols of every watchlist when that is on),
/// read again when the settings or a watchlist change. Singleton (Program.cs); fed by
/// <c>NewsAlertService</c> with every headline the archive sees for the first time.
/// </summary>
public class NewsAlertManager : INewsAlertManager
{
	private readonly ISettingsManager _settingsManager;
	private readonly IWatchlistManager _watchlistManager;
	private readonly ISoundPlayer _soundPlayer;
	private readonly TimeProvider _timeProvider;
	private readonly Lock _lock = new();
	private IReadOnlyList<string> _keywords = [];
	private IReadOnlySet<string> _symbols = new HashSet<string>();
	private bool _sound;

	public NewsAlertManager(
		ISettingsManager settingsManager,
		IWatchlistManager watchlistManager,
		ISoundPlayer soundPlayer,
		TimeProvider timeProvider
	)
	{
		_settingsManager = settingsManager;
		_watchlistManager = watchlistManager;
		_soundPlayer = soundPlayer;
		_timeProvider = timeProvider;
		settingsManager.AppSettingsChanged += Reload;
		watchlistManager.Changed += OnWatchlistsChanged;
		Reload(settingsManager.GetAppSettings());
	}

	public event Action? RulesChanged;

	public bool HasRules
	{
		get
		{
			lock (_lock)
				return _keywords.Count > 0 || _symbols.Count > 0;
		}
	}

	public bool Matches(NewsHeadlineDto headline)
	{
		IReadOnlyList<string> keywords;
		IReadOnlySet<string> symbols;
		lock (_lock)
			(keywords, symbols) = (_keywords, _symbols);
		return NewsHelpers.MatchesAlert(headline, keywords, symbols);
	}

	public int OnHeadlines(IReadOnlyList<NewsHeadlineDto> headlines)
	{
		try
		{
			var now = _timeProvider.GetUtcNow();
			var matched = headlines.Count(x =>
				now - x.Time <= NewsConstants.AlertMaxAge && Matches(x)
			);
			bool sound;
			lock (_lock)
				sound = _sound;
			// One sound for a batch: players started together would only overlap
			if (matched > 0 && sound)
				_soundPlayer.PlayNews();
			return matched;
		}
		catch (Exception)
		{
			// A sound is never worth failing the news over
			return 0;
		}
	}

	private void OnWatchlistsChanged() => Reload(_settingsManager.GetAppSettings());

	private void Reload(AppSettingsDto settings)
	{
		var keywords = NewsHelpers.ParseKeywords(settings.NewsAlertKeywords);
		var symbols = settings.NewsAlertWatchlist
			? _watchlistManager
				.GetWatchlists()
				.SelectMany(x => _watchlistManager.GetItems(x.Id))
				.Select(x => NewsFeedHelpers.ToFeedSymbol(x.Symbol))
				.Where(x => x.Length > 0)
				.ToHashSet()
			: [];
		bool changed;
		lock (_lock)
		{
			changed = !keywords.SequenceEqual(_keywords) || !symbols.SetEquals(_symbols);
			_keywords = keywords;
			_symbols = symbols;
			_sound = settings.NewsAlertSound;
		}
		if (changed)
			RulesChanged?.Invoke();
	}
}
