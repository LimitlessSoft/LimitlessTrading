using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.ViewModels;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Settings;

namespace LT.App.Widgets.Settings;

/// <summary>
/// The settings window: a searchable tree of pages on the left, the selected page's tabs on the
/// right. Changes apply on Save.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
	private readonly ISettingsManager _settingsManager;
	private readonly ICacheManager _cacheManager;

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
		"Off by default: the Gateway stays logged in between runs and the next Log in reuses it without 2FA.",
		"ibc login 2fa exit quit"
	);

	private readonly BoolSettingViewModel _checkForUpdates = new(
		"Check for updates",
		"On start and every 6 hours; a newer version shows in the title bar. Installing always waits for you.",
		"version release upgrade"
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

	public SettingsViewModel(ISettingsManager settingsManager, ICacheManager cacheManager)
	{
		_settingsManager = settingsManager;
		_cacheManager = cacheManager;
		_clearCache = new ActionSettingViewModel(
			"Cache on disk",
			"Clear cache",
			ClearCache,
			"Clearing deletes every cached entry right away; it doesn't wait for Save.",
			"cache clear delete size used disk space"
		);
		var settings = settingsManager.GetAppSettings();
		_marketDataType.Value = settings.MarketDataType;
		_autoReconnect.Value = settings.AutoReconnect;
		_heartbeatSeconds.Value = settings.HeartbeatSeconds;
		_stopGatewayOnExit.Value = settings.StopGatewayOnExit;
		_checkForUpdates.Value = settings.CheckForUpdates;
		_layoutStyle.Value = settings.LayoutStyle;
		_cacheEnabled.Value = settings.CacheEnabled;
		_cacheChartBars.Value = settings.CacheChartBars;
		_cacheContracts.Value = settings.CacheContracts;
		_cacheOptionChains.Value = settings.CacheOptionChains;
		_cacheExecutions.Value = settings.CacheExecutions;
		_cacheSnapshots.Value = settings.CacheSnapshots;
		_cacheMaxSizeMb.Value = settings.CacheMaxSizeMb;
		RefreshCacheSize();

		Pages =
		[
			new SettingsPageViewModel(
				"General",
				children: [new SettingsPageViewModel("Updates", [new("Updates", _checkForUpdates)])]
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

	private void ClearCache()
	{
		_clearCache.Status = "Clearing…";
		Task.Run(_cacheManager.Clear).ContinueWith(_ => RefreshCacheSize());
	}

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
