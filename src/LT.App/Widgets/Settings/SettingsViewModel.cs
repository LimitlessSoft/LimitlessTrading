using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Flex;
using LT.Contracts.Requests.Settings;

namespace LT.App.Widgets.Settings;

/// <summary>
/// The settings window: a searchable tree of pages on the left, the selected page's tabs on the
/// right. Changes apply on Save.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
	private readonly ISettingsManager _settingsManager;
	private readonly ICacheManager _cacheManager;
	private readonly IFlexImportManager _flexImportManager;
	private readonly INewsManager _newsManager;
	private readonly DisplayTimeStore _displayTime;

	private readonly GuideSettingViewModel _flexSetup = FlexSetupGuide.Create();

	private readonly TextSettingViewModel _flexToken = new(
		"Flex Web Service token",
		isSecret: true,
		placeholder: "token from Flex Web Service Configuration",
		description: "Stored in settings.json on this computer; it can only read reports, not trade. Leave all three empty to turn the import off.",
		keywords: "flex token secret",
		normalize: FlexHelpers.NormalizeToken
	);

	private readonly TextSettingViewModel _flexActivityQuery = new(
		"Activity Flex query ID",
		placeholder: "e.g. 123456",
		description: "Daily NAV, deposits / withdrawals and trades. Imported back to the account's start once, then daily (it covers through the previous business day).",
		keywords: "flex query activity nav history",
		normalize: FlexHelpers.ExtractQueryId
	);

	private readonly TextSettingViewModel _flexTradeConfirmQuery = new(
		"Trade Confirmation Flex query ID (optional)",
		placeholder: "e.g. 123457",
		description: "Today's trades, a few minutes after each fill; imported every 5 minutes while the app runs and on F5.",
		keywords: "flex query trade confirmation today fills executions",
		normalize: FlexHelpers.ExtractQueryId
	);

	private readonly ActionSettingViewModel _flexImport;

	private readonly ChoiceSettingViewModel _marketDataType = new(
		"Market data type",
		[.. Enum.GetValues<MarketDataType>().Cast<object>()],
		"Delayed still shows live data for instruments you have a subscription for. Applies to new subscriptions.",
		"live frozen delayed quotes subscription"
	);

	private readonly ChoiceSettingViewModel _layoutStyle = new(
		"Layout style",
		[.. Enum.GetValues<LayoutStyle>().Cast<object>()],
		"Cards: widgets are rounded tiles with gaps between them. Flush: widgets fill the window edge to edge, split by thin lines.",
		"theme look appearance rounded corners gaps spacing compact edge"
	);

	private readonly ChoiceSettingViewModel _displayTimeZone;

	private readonly BoolSettingViewModel _autoReconnect = new(
		"Reconnect automatically when the connection drops",
		keywords: "auto reconnect retry backoff"
	);

	private readonly NumberSettingViewModel _heartbeatSeconds = new(
		"Heartbeat interval (seconds)",
		ConnectionConstants.HeartbeatSecondsMin,
		ConnectionConstants.HeartbeatSecondsMax,
		"How often the connection is checked; a missed heartbeat counts as a dropped connection.",
		"ping keepalive health"
	);

	private readonly BoolSettingViewModel _stopGatewayOnExit = new(
		"Stop IB Gateway when the app closes",
		"Off by default: the Gateway stays logged in between runs and the next start reuses it without logging in again.",
		"login 2fa exit quit close"
	);

	private readonly BoolSettingViewModel _checkForUpdates = new(
		"Check for updates",
		"On start and every 6 hours; a newer version shows in the title bar. Installing always waits for you.",
		"version release upgrade"
	);

	private readonly BoolSettingViewModel _playFillSound = new(
		"Play a sound when an order fills",
		"Every new fill (each partial fill too) on any account, from this app or TWS. Fills are checked every 30 seconds, so the sound can come up to that late; those IB replays on connect stay silent.",
		"sound audio beep chime alert notification fill execution trade"
	);

	private readonly BoolSettingViewModel _calendarNasdaq = new(
		"Nasdaq",
		"Earnings dates (with the time and the EPS estimate), ex-dividend dates, splits and economic releases from nasdaq.com's public calendars. No account or setup. Unofficial: Nasdaq may change it without notice.",
		"calendar nasdaq earnings dividends splits economic releases macro source"
	);

	private readonly BoolSettingViewModel _calendarWallStreetHorizon = new(
		"IB Wall Street Horizon",
		"Corporate events from IB (earnings, dividends, conferences, meetings …). Needs IB's paid \"Wall Street Horizon Corporate Event Data\" subscription and a connection; without it IB refuses and the calendar says so. Events both sources report are shown once.",
		"calendar wsh wall street horizon ib corporate events subscription source"
	);

	private readonly BoolSettingViewModel _newsFeeds = new(
		FeedNewsConstants.SourceName,
		"The app's own news, fetched by this computer from public feeds: SEC filings (8-K and a symbol's filings, tagged with tickers), press releases (PR Newswire, Business Wire), the Federal Reserve, BLS releases, FDA, CNBC, MarketWatch, and per-symbol news from Yahoo Finance and Seeking Alpha. No account, no IB connection. SEC, PR Newswire, Fed and BLS are on by default; the unofficial ones only once picked as a chip in a News widget. Each headline links to the story on the web.",
		"news feeds rss sec edgar filings press releases pr newswire business wire fed federal reserve bls fda cnbc marketwatch yahoo seeking alpha source limitlesstrading"
	);

	private readonly NumberSettingViewModel _newsRetentionDays = new(
		"Keep news for (days)",
		NewsConstants.RetentionDaysMin,
		NewsConstants.RetentionDaysMax,
		"Every headline the app sees (IB's too) is kept on this computer, so history and search work later and offline. Older days are deleted on start; 0 keeps everything.",
		"news archive history retention days keep delete disk"
	);

	private readonly ActionSettingViewModel _clearNewsArchive;

	private readonly TextSettingViewModel _newsAlertKeywords = new(
		"Keywords",
		placeholder: "e.g. FDA approval, acquire, guidance",
		description: "Words or phrases, separated by commas: a headline (or its summary) with any of them, as a whole word, is a match. Matches are marked in News widgets; \"Matches\" there shows only them.",
		keywords: "news alert keywords filter words match highlight"
	);

	private readonly BoolSettingViewModel _newsAlertWatchlist = new(
		"Headlines about a watchlist symbol",
		"A headline about a symbol on any watchlist is a match (SEC filings and press releases are tagged with the tickers they name).",
		"news alert watchlist symbols filter match"
	);

	private readonly BoolSettingViewModel _newsAlertSound = new(
		"Play a sound on a matching headline",
		"When a new headline matches (one sound for several at once); not for the backlog a feed lists when the app starts.",
		"news alert sound audio chime notification match keyword"
	);

	private readonly BoolSettingViewModel _cacheEnabled = new(
		"Cache data on disk",
		"Keeps data fetched from IB to open things faster and to show the last known values, marked as cached, while IB can't be reached. Off: nothing is read from or written to the cache.",
		"cache offline disk storage"
	);

	private readonly BoolSettingViewModel _cacheChartBars = new(
		"Chart bars",
		"Historical bars: about a minute for intraday charts, 30 minutes for daily and weekly.",
		"cache history candles historical"
	);

	private readonly BoolSettingViewModel _cacheContracts = new(
		"Contract lookups",
		"Ticker and symbol searches, for 3 days.",
		"cache symbol ticker search instrument"
	);

	private readonly BoolSettingViewModel _cacheOptionChains = new(
		"Option chain structure",
		"Expirations and strikes, for a day. Prices and greeks are always live.",
		"cache options strikes expirations"
	);

	private readonly BoolSettingViewModel _cacheExecutions = new(
		"Executions",
		"Fills, for 10 seconds; shown as cached while IB can't be reached.",
		"cache trades fills"
	);

	private readonly BoolSettingViewModel _cacheSnapshots = new(
		"Last-known positions, orders and account",
		"Shown only while disconnected, marked as cached, per account. Never used for orders.",
		"cache snapshot offline positions orders account summary"
	);

	private readonly NumberSettingViewModel _cacheMaxSizeMb = new(
		"Maximum cache size (MB)",
		CacheConstants.MaxSizeMbMin,
		CacheConstants.MaxSizeMbMax,
		"Over this, the least recently written entries are deleted.",
		"cache disk space limit megabytes"
	);

	private readonly ActionSettingViewModel _clearCache;

	private readonly ChoiceSettingViewModel _chartLiveUpdates = new(
		"Chart live updates",
		[.. ChartLiveUpdateOption.All],
		"5-second bars: IB's real-time bars, the chart moves every 5 seconds. Every trade: the last bar also follows each last-trade price (the midpoint for forex), like TWS. Quotes take one market data line per open chart. Tick-by-tick is IB's own trade-by-trade feed with IB's timestamps; IB allows only 5 at a time, so further charts use quotes. Without a free line a chart stays on 5-second bars; the status line shows the source in use. Throttled: redrawn at most once per redraw interval. Open charts reload when this changes.",
		"chart live real-time realtime bars quotes ticks trades update speed refresh line"
	);

	private readonly NumberSettingViewModel _chartRedrawInterval = new(
		"Chart redraw interval (ms)",
		ChartConstants.RedrawIntervalMsMin,
		ChartConstants.RedrawIntervalMsMax,
		"Only for the throttled modes: the least time between two redraws of a chart.",
		"chart redraw throttle interval milliseconds refresh"
	);

	private readonly NumberSettingViewModel _chartFootprintImbalance = new(
		"Footprint imbalance (%)",
		FootprintConstants.ImbalancePercentMin,
		FootprintConstants.ImbalancePercentMax,
		"Footprint bars: a cell is highlighted once its volume is at least this share of the cell diagonally opposite — the ask against the bid one price lower (blue, buyers), the bid against the ask one price higher (red, sellers). 300 % is the usual default. Open footprint charts redraw when this changes.",
		"chart footprint imbalance ratio percent diagonal bid ask order flow"
	);

	public SettingsViewModel(
		ISettingsManager settingsManager,
		ICacheManager cacheManager,
		IFlexImportManager flexImportManager,
		INewsManager newsManager,
		DisplayTimeStore displayTime
	)
	{
		_settingsManager = settingsManager;
		_cacheManager = cacheManager;
		_flexImportManager = flexImportManager;
		_newsManager = newsManager;
		_displayTime = displayTime;
		_flexImport = new ActionSettingViewModel(
			"Flex import",
			"Save & import now",
			ImportFlexNow,
			"Saves the three Flex fields right away and imports what is due, plus today's trades. The first import fetches a year at a time back to the account's start (IB allows 10 requests a minute), so it can take a few minutes.",
			"flex import now test status last error"
		);
		var flex = settingsManager.GetFlexSettings();
		_flexToken.Value = flex.Token;
		_flexActivityQuery.Value = flex.ActivityQueryId;
		_flexTradeConfirmQuery.Value = flex.TradeConfirmQueryId;
		RefreshFlexStatus();
		flexImportManager.StatusChanged += OnFlexStatusChanged;
		_clearCache = new ActionSettingViewModel(
			"Cache on disk",
			"Clear cache",
			ClearCache,
			"Clearing deletes every cached entry right away; it doesn't wait for Save.",
			"cache clear delete size used disk space"
		);
		_clearNewsArchive = new ActionSettingViewModel(
			"News archive",
			"Clear news archive",
			ClearNewsArchive,
			"Deletes every kept headline and article right away; it doesn't wait for Save. The feeds still list their latest items, so a few come back.",
			"news archive clear delete size disk"
		);
		var settings = settingsManager.GetAppSettings();
		var zones = TimeZoneOption.CreateAll(settings.DisplayTimeZoneId);
		_displayTimeZone = new ChoiceSettingViewModel(
			"Time zone",
			zones,
			"Every time the app shows (charts, news, fills, status lines) is in this zone. Exchange time is New York, where US markets trade. Trading days stay counted in New York time whatever is picked here.",
			"time zone clock local exchange new york eastern utc display"
		)
		{
			Value = zones.FirstOrDefault(x => x.Id == settings.DisplayTimeZoneId) ?? zones[0],
		};
		_marketDataType.Value = settings.MarketDataType;
		_autoReconnect.Value = settings.AutoReconnect;
		_heartbeatSeconds.Value = settings.HeartbeatSeconds;
		_stopGatewayOnExit.Value = settings.StopGatewayOnExit;
		_checkForUpdates.Value = settings.CheckForUpdates;
		_playFillSound.Value = settings.PlayFillSound;
		_calendarNasdaq.Value = settings.CalendarNasdaq;
		_calendarWallStreetHorizon.Value = settings.CalendarWallStreetHorizon;
		_newsFeeds.Value = settings.NewsFeeds;
		_newsRetentionDays.Value = settings.NewsRetentionDays;
		_newsAlertKeywords.Value = settings.NewsAlertKeywords;
		_newsAlertWatchlist.Value = settings.NewsAlertWatchlist;
		_newsAlertSound.Value = settings.NewsAlertSound;
		_layoutStyle.Value = settings.LayoutStyle;
		_cacheEnabled.Value = settings.CacheEnabled;
		_cacheChartBars.Value = settings.CacheChartBars;
		_cacheContracts.Value = settings.CacheContracts;
		_cacheOptionChains.Value = settings.CacheOptionChains;
		_cacheExecutions.Value = settings.CacheExecutions;
		_cacheSnapshots.Value = settings.CacheSnapshots;
		_cacheMaxSizeMb.Value = settings.CacheMaxSizeMb;
		_chartLiveUpdates.Value = ChartLiveUpdateOption.Find(settings.ChartLiveUpdateMode);
		_chartRedrawInterval.Value = settings.ChartRedrawIntervalMs;
		_chartFootprintImbalance.Value = settings.ChartFootprintImbalancePercent;
		UpdateChartRedrawInterval();
		_chartLiveUpdates.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(ChoiceSettingViewModel.Value))
				UpdateChartRedrawInterval();
		};
		RefreshCacheSize();
		RefreshNewsArchiveSize();

		Pages =
		[
			new SettingsPageViewModel(
				"General",
				children:
				[
					new SettingsPageViewModel("Updates", [new("Updates", _checkForUpdates)]),
					new SettingsPageViewModel("Time zone", [new("Time zone", _displayTimeZone)]),
					new SettingsPageViewModel("Sounds", [new("Fills", _playFillSound)]),
					new SettingsPageViewModel(
						"Charts",
						[
							new("Live updates", _chartLiveUpdates, _chartRedrawInterval),
							new("Footprint", _chartFootprintImbalance),
						]
					),
				]
			),
			new SettingsPageViewModel("Appearance", [new("Layout", _layoutStyle)]),
			new SettingsPageViewModel(
				"Connection",
				[
					new("Reconnect", _autoReconnect, _heartbeatSeconds),
					new("IB Gateway", _stopGatewayOnExit),
				]
			),
			new SettingsPageViewModel("Market data", [new("Quotes", _marketDataType)]),
			new SettingsPageViewModel(
				SettingsPageNames.Calendar,
				[new("Sources", _calendarNasdaq, _calendarWallStreetHorizon)]
			),
			new SettingsPageViewModel(
				SettingsPageNames.News,
				[
					new("Sources", _newsFeeds),
					new("Alerts", _newsAlertKeywords, _newsAlertWatchlist, _newsAlertSound),
					new("Archive", _newsRetentionDays, _clearNewsArchive),
				]
			),
			new SettingsPageViewModel(
				"Account history",
				[
					new(
						"IB Flex",
						_flexSetup,
						_flexToken,
						_flexActivityQuery,
						_flexTradeConfirmQuery,
						_flexImport
					),
				]
			),
			new SettingsPageViewModel(
				"Cache",
				[
					new(
						"Cache",
						_cacheEnabled,
						_cacheChartBars,
						_cacheContracts,
						_cacheOptionChains,
						_cacheExecutions,
						_cacheSnapshots
					),
					new("Disk", _cacheMaxSizeMb, _clearCache),
				]
			),
		];
		ApplySearch();
	}

	/// <summary>Every page of the tree, as defined; the tree shows <see cref="VisiblePages"/>.</summary>
	public IReadOnlyList<SettingsPageViewModel> Pages { get; }

	/// <summary>Top-level pages left by the search.</summary>
	public ObservableCollection<SettingsPageViewModel> VisiblePages { get; } = [];

	[ObservableProperty]
	public partial string? SearchText { get; set; }

	/// <summary>The tree's selection: a page, or a group (which shows its first page).</summary>
	[ObservableProperty]
	public partial SettingsPageViewModel? SelectedNode { get; set; }

	/// <summary>The page shown on the right.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasPage))]
	public partial SettingsPageViewModel? SelectedPage { get; set; }

	public bool HasPage => SelectedPage != null;

	/// <summary>Nothing matches the search.</summary>
	public bool HasNoResults => VisiblePages.Count == 0;

	[ObservableProperty]
	public partial string? ErrorMessage { get; set; }

	partial void OnSearchTextChanged(string? value) => ApplySearch();

	partial void OnSelectedNodeChanged(SettingsPageViewModel? value)
	{
		if (value != null)
			SelectedPage = value.FirstPage;
	}

	/// <summary>Returns true when the settings were saved and the dialog can close.</summary>
	public bool Save()
	{
		try
		{
			_settingsManager.SaveFlexSettings(GetFlexRequest());
			_settingsManager.SaveAppSettings(
				new SaveAppSettingsRequest
				{
					MarketDataType = (MarketDataType)_marketDataType.Value!,
					AutoReconnect = _autoReconnect.Value,
					HeartbeatSeconds = (int)(_heartbeatSeconds.Value ?? 0),
					StopGatewayOnExit = _stopGatewayOnExit.Value,
					CheckForUpdates = _checkForUpdates.Value,
					LayoutStyle = (LayoutStyle)_layoutStyle.Value!,
					CacheEnabled = _cacheEnabled.Value,
					CacheChartBars = _cacheChartBars.Value,
					CacheContracts = _cacheContracts.Value,
					CacheOptionChains = _cacheOptionChains.Value,
					CacheExecutions = _cacheExecutions.Value,
					CacheSnapshots = _cacheSnapshots.Value,
					CacheMaxSizeMb = (int)(_cacheMaxSizeMb.Value ?? 0),
					ChartLiveUpdateMode = SelectedChartLiveUpdateMode,
					ChartRedrawIntervalMs = (int)(_chartRedrawInterval.Value ?? 0),
					ChartFootprintImbalancePercent = (int)(
						_chartFootprintImbalance.Value ?? FootprintConstants.DefaultImbalancePercent
					),
					DisplayTimeZoneId = (_displayTimeZone.Value as TimeZoneOption)?.Id,
					PlayFillSound = _playFillSound.Value,
					CalendarNasdaq = _calendarNasdaq.Value,
					CalendarWallStreetHorizon = _calendarWallStreetHorizon.Value,
					NewsFeeds = _newsFeeds.Value,
					NewsRetentionDays = (int)(
						_newsRetentionDays.Value ?? NewsConstants.DefaultRetentionDays
					),
					NewsAlertKeywords = _newsAlertKeywords.Value,
					NewsAlertWatchlist = _newsAlertWatchlist.Value,
					NewsAlertSound = _newsAlertSound.Value,
				}
			);
			// a lowered limit applies now, not at the next cache write
			Task.Run(_cacheManager.EnforceSizeLimit);
			return true;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return false;
		}
	}

	/// <summary>Opens on the page titled <paramref name="title"/> (a widget's "Set up…"), search cleared.</summary>
	public void ShowPage(string title)
	{
		SearchText = null;
		var page = FindPage(Pages, title);
		if (page == null)
			return;
		SelectedNode = page;
		SelectedPage = page.FirstPage;
	}

	private static SettingsPageViewModel? FindPage(
		IEnumerable<SettingsPageViewModel> pages,
		string title
	)
	{
		foreach (var page in pages)
		{
			if (page.Title == title)
				return page;
			if (FindPage(page.Children, title) is { } child)
				return child;
		}
		return null;
	}

	public void Dispose() => _flexImportManager.StatusChanged -= OnFlexStatusChanged;

	private SaveFlexSettingsRequest GetFlexRequest() =>
		new()
		{
			Token = _flexToken.Value,
			ActivityQueryId = _flexActivityQuery.Value,
			TradeConfirmQueryId = _flexTradeConfirmQuery.Value,
		};

	private void ImportFlexNow()
	{
		try
		{
			_settingsManager.SaveFlexSettings(GetFlexRequest());
			ErrorMessage = null;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return;
		}
		// Progress and errors land in the status line
		_flexImport.IsBusy = true;
		_flexImport.Status = "Starting the import…";
		_ = Task.Run(() => _flexImportManager.ImportNowAsync())
			.ContinueWith(_ => Dispatcher.UIThread.Post(RefreshFlexStatus));
	}

	private void OnFlexStatusChanged() => Dispatcher.UIThread.Post(RefreshFlexStatus);

	private void RefreshFlexStatus()
	{
		var status = _flexImportManager.GetStatus();
		_flexImport.Status = FlexStatusHelpers.Describe(
			status,
			_displayTime.Zone,
			_displayTime.Now
		);
		_flexImport.IsBusy = status.IsRunning;
	}

	private ChartLiveUpdateMode SelectedChartLiveUpdateMode =>
		(_chartLiveUpdates.Value as ChartLiveUpdateOption)?.Mode
		?? ChartLiveUpdateMode.RealtimeBars;

	// The redraw interval applies only to the throttled modes
	private void UpdateChartRedrawInterval() =>
		_chartRedrawInterval.IsEnabled =
			ChartLiveUpdateHelpers.GetRedrawInterval(SelectedChartLiveUpdateMode, 0)
			> TimeSpan.Zero;

	private void ClearCache()
	{
		_clearCache.Status = "Clearing…";
		Task.Run(_cacheManager.Clear).ContinueWith(_ => RefreshCacheSize());
	}

	private void ClearNewsArchive()
	{
		_clearNewsArchive.Status = "Clearing…";
		Task.Run(_newsManager.ClearArchive).ContinueWith(_ => RefreshNewsArchiveSize());
	}

	private void RefreshNewsArchiveSize() =>
		Task.Run(_newsManager.GetArchiveSizeBytes)
			.ContinueWith(t =>
				Dispatcher.UIThread.Post(() =>
					_clearNewsArchive.Status = t.IsCompletedSuccessfully
						? $"{CacheHelpers.FormatSize(t.Result)} used"
						: "Size unknown"
				)
			);

	// Walks the cache folder off the UI thread
	private void RefreshCacheSize() =>
		Task.Run(_cacheManager.GetSizeBytes)
			.ContinueWith(t =>
				Dispatcher.UIThread.Post(() =>
					_clearCache.Status = t.IsCompletedSuccessfully
						? $"{CacheHelpers.FormatSize(t.Result)} used"
						: "Size unknown"
				)
			);

	// Filters the tree; the shown page stays if it still matches, else the first match opens
	private void ApplySearch()
	{
		VisiblePages.Clear();
		foreach (var page in Pages)
			if (page.Filter(SearchText))
				VisiblePages.Add(page);
		OnPropertyChanged(nameof(HasNoResults));

		if (SelectedPage is { } shown && IsVisible(shown))
			return;
		var first = VisiblePages.Select(x => x.FirstPage).FirstOrDefault(x => x != null);
		SelectedNode = first;
		SelectedPage = first;
	}

	private bool IsVisible(SettingsPageViewModel page) =>
		page.Parent is { } parent
			? parent.VisibleChildren.Contains(page) && IsVisible(parent)
			: VisiblePages.Contains(page);
}
