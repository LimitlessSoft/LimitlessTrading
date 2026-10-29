using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.News;

namespace LT.App.Widgets.News;

/// <summary>
/// Headlines from every news source and the local archive (<see cref="INewsManager"/>). Works
/// without IB: the app's own feeds ("LimitlessTrading") are always there, IB's providers join
/// on connect and leave on disconnect. Linked: the link group's contract (by symbol while
/// disconnected), past headlines first, then live ones. All news: the broad feeds of the
/// selected providers, the archived ones first, then live. Search: the archive, every source.
/// Live headlines are read off the UI thread and added in batches; the list keeps
/// <see cref="NewsConstants.MaxHeadlinesShown"/>, newest first, the same story once. Headlines
/// matching the alert rules (<see cref="INewsAlertManager"/>) are marked, and "Matches only"
/// shows only those. Selecting one loads its article. The header title is a ticker box.
/// </summary>
public partial class NewsViewModel : ConnectedViewModelBase, ILinkable, IWidgetState, ITickerTitled
{
	private readonly INewsManager _newsManager;
	private readonly IContractManager _contractManager;
	private readonly INewsAlertManager _alertManager;
	private readonly ISettingsManager _settingsManager;
	private readonly DisplayTimeStore _displayTime;
	private readonly ZonedText _articleMeta;
	private readonly CancellationTokenSource _lifetime = new();
	private readonly List<NewsHeadlineDto> _all = [];
	private readonly HashSet<string> _keys = [];
	private readonly Dictionary<string, List<(DateTimeOffset Time, string Provider)>> _stories = [];
	private readonly ConcurrentQueue<(CancellationToken Feed, NewsHeadlineDto Headline)> _incoming =
		new();
	private ContractDto? _contract;
	private CancellationTokenSource? _feed;
	private CancellationTokenSource? _restart;
	private CancellationTokenSource? _article;
	private NewsArticleDto? _document;
	private string? _articleKey;
	private List<string>? _savedProviders;
	private List<NewsProviderDto> _feedProviders = [];
	private bool _newsFeeds;
	private bool _hasMore;
	private bool _applyingSelection;
	private bool _loadingProviders;
	private bool _providersLoaded;
	private int _providersVersion;
	private int _flushQueued;

	public NewsViewModel(
		INewsManager newsManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		INewsAlertManager alertManager,
		ISettingsManager settingsManager,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		_newsManager = newsManager;
		_contractManager = contractManager;
		_alertManager = alertManager;
		_settingsManager = settingsManager;
		_displayTime = displayTime;
		_articleMeta = new ZonedText(x => ArticleMeta = x ?? string.Empty);
		displayTime.ZoneChanged += OnZoneChanged;
		alertManager.RulesChanged += OnAlertRulesChanged;
		settingsManager.AppSettingsChanged += OnSettingsChanged;
		_newsFeeds = settingsManager.GetAppSettings().NewsFeeds;
		Link = new ContractLink(linkGroupStore, () => _contract);
		Link.ContractReceived += OnContractReceived;
		TickerEditor = new TickerTitleEditor(
			contractManager,
			() => IsConnected,
			OpenTickerAsync,
			OpenContract
		);
		_contract = linkGroupStore.Get(Link.Group);
		UpdateTitle();
		// After the base class has told a connection (which loads them too)
		Dispatcher.UIThread.Post(() =>
		{
			if (!_loadingProviders && !_providersLoaded)
				_ = LoadProvidersAsync();
		});
	}

	/// <summary>Linked mode follows the group's contract (watchlist, positions, a chart).</summary>
	public ContractLink Link { get; }

	/// <summary>The header title's ticker box: typing a ticker there shows its news.</summary>
	public TickerTitleEditor TickerEditor { get; }

	public ObservableCollection<NewsProviderChip> Providers { get; } = [];

	/// <summary>Headlines passing the text filter (and "Matches only"), newest first.</summary>
	public ObservableCollection<NewsRowViewModel> Rows { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(
		nameof(IsLinkedMode),
		nameof(IsAllMode),
		nameof(IsSearchMode),
		nameof(FilterPlaceholder)
	)]
	public partial NewsMode Mode { get; set; } = NewsMode.Linked;

	public bool IsLinkedMode
	{
		get => Mode == NewsMode.Linked;
		set => SetMode(value, NewsMode.Linked);
	}

	public bool IsAllMode
	{
		get => Mode == NewsMode.All;
		set => SetMode(value, NewsMode.All);
	}

	/// <summary>The text box searches the archive (every source) instead of filtering the list.</summary>
	public bool IsSearchMode
	{
		get => Mode == NewsMode.Search;
		set => SetMode(value, NewsMode.Search);
	}

	public string FilterPlaceholder =>
		Mode == NewsMode.Search ? "Search the news archive" : "Filter headlines";

	/// <summary>Frame header title: "News · AAPL", "News · All", "News · Search".</summary>
	[ObservableProperty]
	public partial string Title { get; set; } = "News";

	[ObservableProperty]
	public partial string? FilterText { get; set; }

	/// <summary>Only headlines matching the news alert rules (keywords, watchlist symbols).</summary>
	[ObservableProperty]
	public partial bool OnlyMatches { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasArticle))]
	public partial NewsRowViewModel? SelectedRow { get; set; }

	[ObservableProperty]
	public partial string? InfoText { get; set; }

	[ObservableProperty]
	public partial bool IsLoading { get; set; }

	/// <summary>A live subscription is running.</summary>
	[ObservableProperty]
	public partial bool IsLive { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LoadOlderCommand))]
	public partial bool CanLoadOlder { get; set; }

	[ObservableProperty]
	public partial bool IsLoadingOlder { get; set; }

	public bool HasArticle => SelectedRow != null;

	[ObservableProperty]
	public partial string ArticleTitle { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ArticleMeta { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string ArticleText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool IsArticleLoading { get; set; }

	/// <summary>The article is a document (PDF …): opened with the OS instead of shown.</summary>
	[ObservableProperty]
	public partial bool IsArticleDocument { get; set; }

	[ObservableProperty]
	public partial string? ArticleError { get; set; }

	/// <summary>The story on the web ("Open in browser"); null when the source gives none.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasArticleUrl))]
	public partial string? ArticleUrl { get; set; }

	public bool HasArticleUrl => !string.IsNullOrEmpty(ArticleUrl);

	public void LoadState(WidgetPlacementDto placement)
	{
		_savedProviders = placement.NewsProviders is { } saved ? [.. saved] : null;
		if (Providers.Count > 0)
			ApplySavedSelection();
		OnlyMatches = placement.NewsOnlyMatches == true;
		var mode =
			placement.NewsMode is { } savedMode && Enum.IsDefined(savedMode)
				? savedMode
				: NewsMode.Linked;
		if (mode != Mode)
			Mode = mode; // restarts the feed
		else
			StartFeed();
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.NewsMode = Mode;
		placement.NewsProviders = _savedProviders is { } saved ? [.. saved] : null;
		placement.NewsOnlyMatches = OnlyMatches ? true : null;
	}

	/// <summary>
	/// Writes the selected document article to a temp file for the view to open; null (and an
	/// error in the reading pane) when it can't.
	/// </summary>
	public string? SaveDocument()
	{
		if (_document is not { } document || SelectedRow is not { } row)
			return null;
		try
		{
			var content = NewsHelpers.DecodeBinary(document);
			var path = Path.Combine(
				Path.GetTempPath(),
				NewsHelpers.GetDocumentFileName(row.Headline, content)
			);
			File.WriteAllBytes(path, content);
			return path;
		}
		catch (Exception ex)
			when (ex is FormatException or IOException or UnauthorizedAccessException)
		{
			ArticleError = ex.Message;
			return null;
		}
	}

	public void ShowDocumentError(string message) => ArticleError = message;

	/// <summary>
	/// Ticker box: resolves the ticker and shows its news; while disconnected IB can't resolve it,
	/// so it is shown by its symbol (the app's feeds and the archive), not passed to the group.
	/// </summary>
	public async Task OpenTickerAsync(string ticker)
	{
		ErrorMessage = null;
		if (!IsConnected)
		{
			ShowContract(
				new ContractDto
				{
					Symbol = ticker,
					SecurityType = MarketDataConstants.StockType,
					Currency = "USD",
				},
				publish: false
			);
			return;
		}
		try
		{
			ShowContract(await _contractManager.ResolveTickerAsync(ticker));
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	/// <summary>Ticker box: a recent or suggested listing, already resolved.</summary>
	public void OpenContract(ContractDto contract)
	{
		_contractManager.AddRecentContract(contract);
		ShowContract(contract);
	}

	public override void Dispose()
	{
		_displayTime.ZoneChanged -= OnZoneChanged;
		_alertManager.RulesChanged -= OnAlertRulesChanged;
		_settingsManager.AppSettingsChanged -= OnSettingsChanged;
		Link.Dispose();
		StopFeed();
		CancelArticle();
		_restart?.Cancel();
		_lifetime.Cancel();
		base.Dispose();
	}

	// IB's providers join on connect and leave on disconnect; ours stay
	protected override void OnConnected(CancellationToken session) => _ = LoadProvidersAsync();

	protected override void OnDisconnected() => _ = LoadProvidersAsync();

	partial void OnModeChanged(NewsMode value)
	{
		foreach (var chip in Providers)
			chip.Mode = value;
		UpdateTitle();
		StartFeed();
	}

	partial void OnFilterTextChanged(string? value)
	{
		if (Mode == NewsMode.Search)
			RestartSoon(NewsConstants.SearchDelay);
		else
			RebuildRows();
	}

	partial void OnOnlyMatchesChanged(bool value) => RebuildRows();

	partial void OnSelectedRowChanged(NewsRowViewModel? value)
	{
		if (value == null)
		{
			CancelArticle();
			_articleKey = null;
			_document = null;
			ArticleUrl = null;
			return;
		}
		if (value.Key != _articleKey)
			_ = LoadArticleAsync(value);
	}

	[RelayCommand(CanExecute = nameof(CanLoadOlder))]
	private async Task LoadOlderAsync()
	{
		if (_feed is not { } feed || _all.Count == 0)
			return;

		var token = feed.Token;
		var before = _all[^1].Time;
		CanLoadOlder = false;
		IsLoadingOlder = true;
		try
		{
			var page = Mode switch
			{
				NewsMode.Search => await _newsManager.SearchArchiveAsync(
					new SearchNewsRequest { Query = FilterText ?? string.Empty, Before = before },
					token
				),
				NewsMode.All => await _newsManager.GetHeadlinesAsync(
					new GetNewsHeadlinesRequest { Providers = _feedProviders, Before = before },
					token
				),
				_ when _contract is { } contract => await _newsManager.GetHeadlinesAsync(
					new GetNewsHeadlinesRequest
					{
						Contract = contract,
						Providers = _feedProviders,
						Before = before,
					},
					token
				),
				_ => new NewsHeadlinesPageDto(),
			};
			if (token.IsCancellationRequested)
				return;
			foreach (var headline in page.Headlines)
				AddHeadline(headline, trim: false);
			_hasMore = page.HasMore;
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!token.IsCancellationRequested)
			{
				IsLoadingOlder = false;
				CanLoadOlder = _hasMore && _all.Count > 0; // paging goes back from the oldest shown
			}
		}
	}

	private void SetMode(bool on, NewsMode mode)
	{
		if (on)
			Mode = mode;
		else
		{
			// A mode button can't be switched off by clicking it again; it stays checked
			OnPropertyChanged(nameof(IsLinkedMode));
			OnPropertyChanged(nameof(IsAllMode));
			OnPropertyChanged(nameof(IsSearchMode));
		}
	}

	// A ticker typed here switches to Linked mode and moves the link group along, like a chart's
	private void ShowContract(ContractDto contract, bool publish = true)
	{
		var changed = contract.ConId != _contract?.ConId || contract.Symbol != _contract?.Symbol;
		_contract = contract;
		UpdateTitle();
		if (publish)
			Link.Publish(contract);
		if (Mode != NewsMode.Linked)
			Mode = NewsMode.Linked; // restarts the feed
		else if (changed)
			StartFeed();
	}

	private void OnContractReceived(ContractDto contract)
	{
		if (contract.ConId == _contract?.ConId && contract.Symbol == _contract?.Symbol)
			return;
		_contract = contract;
		UpdateTitle();
		if (Mode == NewsMode.Linked)
			StartFeed();
	}

	private void UpdateTitle() =>
		Title = Mode switch
		{
			NewsMode.All => "News · All",
			NewsMode.Search => "News · Search",
			_ when _contract is { } contract =>
				$"News · {ContractHelpers.GetDisplayName(contract)}",
			_ => "News",
		};

	private async Task LoadProvidersAsync()
	{
		var version = ++_providersVersion;
		_loadingProviders = true;
		StopFeed();
		IsLoading = true;
		InfoText = null;
		ErrorMessage = null;
		try
		{
			var providers = await _newsManager.GetProvidersAsync(_lifetime.Token);
			if (version != _providersVersion)
				return;
			foreach (var chip in Providers)
				chip.PropertyChanged -= OnChipChanged;
			Providers.Clear();
			// Ours first in the catalog's order (the default ones lead), then IB's by code
			foreach (
				var provider in providers
					.Select((x, i) => (Provider: x, Index: i))
					.OrderBy(x => x.Provider.SourceId == FeedNewsConstants.SourceId ? 0 : 1)
					.ThenBy(x => x.Provider.SourceId == FeedNewsConstants.SourceId ? x.Index : 0)
					.ThenBy(x => x.Provider.Code, StringComparer.Ordinal)
					.Select(x => x.Provider)
			)
			{
				var chip = new NewsProviderChip(provider) { Mode = Mode };
				chip.PropertyChanged += OnChipChanged;
				Providers.Add(chip);
			}
			ApplySavedSelection();
			_loadingProviders = false;
			_providersLoaded = true;
			StartFeed();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (version != _providersVersion)
				return;
			_loadingProviders = false;
			IsLoading = false;
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	private void ApplySavedSelection()
	{
		var saved = _savedProviders?.ToHashSet();
		_applyingSelection = true;
		try
		{
			foreach (var chip in Providers)
				chip.IsSelected = NewsHelpers.IsProviderSelected(saved, chip.Provider);
		}
		finally
		{
			_applyingSelection = false;
		}
	}

	// Chip clicks in quick succession restart the feed once
	private void OnChipChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(NewsProviderChip.IsSelected) || _applyingSelection)
			return;
		_savedProviders = NewsHelpers.MergeProviderSelection(
			[.. Providers.Select(x => (x.Provider, x.IsSelected))],
			_savedProviders
		);
		RestartSoon(NewsConstants.ProviderChangeDelay);
	}

	private void RestartSoon(TimeSpan delay)
	{
		_restart?.Cancel();
		_restart = new CancellationTokenSource();
		var token = _restart.Token;
		_ = Task.Delay(delay, token)
			.ContinueWith(
				_ =>
					Dispatcher.UIThread.Post(() =>
					{
						if (!token.IsCancellationRequested)
							StartFeed();
					}),
				token,
				TaskContinuationOptions.OnlyOnRanToCompletion,
				TaskScheduler.Default
			);
	}

	// The LimitlessTrading feeds were turned on or off in Settings: the chips change
	private void OnSettingsChanged(AppSettingsDto settings) =>
		Dispatcher.UIThread.Post(() =>
		{
			if (settings.NewsFeeds == _newsFeeds)
				return;
			_newsFeeds = settings.NewsFeeds;
			_ = LoadProvidersAsync();
		});

	private void OnAlertRulesChanged() => Dispatcher.UIThread.Post(RebuildRows);

	private List<NewsProviderDto> GetSelectedProviders() =>
		[.. Providers.Where(x => x.IsSelected).Select(x => x.Provider)];

	// New mode, contract, providers, search or connection: drop what is shown and start over
	private void StartFeed()
	{
		StopFeed();
		ClearHeadlines();
		ErrorMessage = null;
		InfoText = null;
		_hasMore = false;
		CanLoadOlder = false;

		if (_loadingProviders)
			return;
		if (!_providersLoaded)
		{
			// The provider list failed earlier: try again
			_ = LoadProvidersAsync();
			return;
		}
		_feed = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
		if (Mode == NewsMode.Search)
		{
			// The whole archive, whatever the chips say (IB's archived news too while disconnected)
			_ = RunSearchAsync(FilterText?.Trim() ?? string.Empty, _feed.Token);
			return;
		}
		if (Providers.Count == 0)
		{
			InfoText = IsConnected
				? "No news providers on this account."
				: "No news sources: turn on the LimitlessTrading feeds in Settings › News, or connect to IB.";
			return;
		}
		var selected = GetSelectedProviders();
		if (selected.Count == 0)
		{
			InfoText = "Pick at least one provider.";
			return;
		}

		if (Mode == NewsMode.All)
			_ = RunAllAsync(selected, _feed.Token);
		else if (_contract is { } contract)
		{
			var bySymbol = selected
				.Where(x => x.Capabilities.HasFlag(NewsSourceCapabilities.BySymbol))
				.ToList();
			if (bySymbol.Count == 0)
				InfoText =
					"None of the selected providers has news per symbol; pick SEC, PRN, YF … or use All news.";
			else
				_ = RunLinkedAsync(contract, bySymbol, _feed.Token);
		}
		else
			InfoText =
				"Pick a symbol in a widget of the same link group, or type one in the title.";
	}

	private async Task RunLinkedAsync(
		ContractDto contract,
		List<NewsProviderDto> providers,
		CancellationToken feed
	)
	{
		_feedProviders = providers;
		if (
			await LoadHistoryAsync(
				new GetNewsHeadlinesRequest { Contract = contract, Providers = providers },
				$"No recent news for {ContractHelpers.GetDisplayName(contract)}.",
				feed
			)
		)
			StartLive(
				new SubscribeNewsRequest { Contract = contract, Providers = providers },
				feed
			);
	}

	private async Task RunAllAsync(List<NewsProviderDto> providers, CancellationToken feed)
	{
		IsLoading = true;
		List<NewsProviderDto> tapes;
		try
		{
			// Every provider is checked once (the source remembers), so the chips show which have one
			var withTape = (
				await _newsManager.GetBroadTapeProvidersAsync(
					Providers.Select(x => x.Provider),
					feed
				)
			)
				.Select(NewsHelpers.GetProviderKey)
				.ToHashSet();
			if (feed.IsCancellationRequested)
				return;
			foreach (var chip in Providers)
				chip.HasBroadTape = withTape.Contains(chip.Key);
			tapes = [.. providers.Where(x => withTape.Contains(NewsHelpers.GetProviderKey(x)))];
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return;
		}
		finally
		{
			if (!feed.IsCancellationRequested)
				IsLoading = false;
		}

		var without = providers.Where(x => !tapes.Contains(x)).Select(x => x.Code).ToList();
		var available = Providers.Where(x => x.HasBroadTape == true).Select(x => x.Label).ToList();
		if (tapes.Count == 0)
		{
			InfoText =
				available.Count == 0
					? "No provider has a broad feed; use Linked mode."
					: $"No broad feed for the selected providers. Pick one of: {string.Join(", ", available)}.";
			return;
		}
		_feedProviders = tapes;
		var info = without.Count == 0 ? null : $"No broad feed: {string.Join(", ", without)}.";
		if (await LoadHistoryAsync(new GetNewsHeadlinesRequest { Providers = tapes }, null, feed))
		{
			InfoText ??= info;
			StartLive(new SubscribeNewsRequest { Providers = tapes }, feed);
		}
	}

	// Past headlines first; false when the feed was replaced meanwhile
	private async Task<bool> LoadHistoryAsync(
		GetNewsHeadlinesRequest request,
		string? emptyText,
		CancellationToken feed
	)
	{
		IsLoading = true;
		try
		{
			var page = await _newsManager.GetHeadlinesAsync(request, feed);
			if (feed.IsCancellationRequested)
				return false;
			foreach (var headline in page.Headlines)
				AddHeadline(headline, trim: false);
			_hasMore = page.HasMore;
			CanLoadOlder = _hasMore && _all.Count > 0; // paging goes back from the oldest shown
			if (_all.Count == 0 && emptyText != null)
				InfoText = emptyText;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!feed.IsCancellationRequested)
				IsLoading = false;
		}
		return !feed.IsCancellationRequested;
	}

	private async Task RunSearchAsync(string query, CancellationToken feed)
	{
		if (query.Length < NewsConstants.SearchMinLength)
		{
			InfoText =
				"Type in the box to search every headline kept in the news archive (all sources).";
			return;
		}
		IsLoading = true;
		try
		{
			var page = await _newsManager.SearchArchiveAsync(
				new SearchNewsRequest { Query = query },
				feed
			);
			if (feed.IsCancellationRequested)
				return;
			foreach (var headline in page.Headlines)
				AddHeadline(headline, trim: false);
			_hasMore = page.HasMore;
			CanLoadOlder = _hasMore && _all.Count > 0; // paging goes back from the oldest shown
			if (_all.Count == 0)
				InfoText = $"Nothing in the news archive for \"{query}\".";
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!feed.IsCancellationRequested)
				IsLoading = false;
		}
	}

	// The stream is read on the thread pool; headlines reach the list in batches on the UI thread
	private void StartLive(SubscribeNewsRequest request, CancellationToken feed)
	{
		IsLive = true;
		_ = Task.Run(
			async () =>
			{
				string? error = null;
				try
				{
					await foreach (
						var headline in _newsManager
							.SubscribeHeadlinesAsync(request, feed)
							.ConfigureAwait(false)
					)
					{
						_incoming.Enqueue((feed, headline));
						if (Interlocked.Exchange(ref _flushQueued, 1) == 0)
							Dispatcher.UIThread.Post(FlushIncoming);
					}
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					error = ErrorMessageHelpers.ToUserMessage(ex);
				}
				Dispatcher.UIThread.Post(() =>
				{
					if (feed.IsCancellationRequested)
						return;
					IsLive = false;
					if (error != null)
						ErrorMessage = error;
				});
			},
			feed
		);
	}

	private void FlushIncoming()
	{
		Interlocked.Exchange(ref _flushQueued, 0);
		var added = false;
		while (_incoming.TryDequeue(out var item))
			if (!item.Feed.IsCancellationRequested)
				added |= AddHeadline(item.Headline, trim: true);
		if (added && Mode == NewsMode.Linked && _all.Count > 0)
			InfoText = null; // "No recent news" no longer holds
	}

	// The text filter applies to the list, except in Search mode (the results are the search)
	private bool PassesFilter(NewsRowViewModel row) =>
		(Mode == NewsMode.Search || NewsHelpers.Matches(row.Headline, FilterText))
		&& (!OnlyMatches || row.IsMatch);

	// Live headlines push the oldest out past the cap; "Load older" may grow the list beyond it
	private bool AddHeadline(NewsHeadlineDto headline, bool trim)
	{
		if (
			_keys.Contains(NewsHelpers.GetKey(headline))
			|| NewsHelpers.IsRepeatedStory(_stories, headline, NewsConstants.StoryWindow)
		)
			return false;
		NewsHelpers.Insert(_all, _keys, headline);
		var row = new NewsRowViewModel(headline, _displayTime, _alertManager.Matches(headline));
		if (PassesFilter(row))
			Rows.Insert(NewsHelpers.GetInsertIndex(Rows, x => x.Time, headline.Time), row);

		while (trim && _all.Count > NewsConstants.MaxHeadlinesShown)
		{
			var oldest = _all[^1];
			_all.RemoveAt(_all.Count - 1);
			_keys.Remove(NewsHelpers.GetKey(oldest));
			NewsHelpers.ForgetStory(_stories, oldest);
			if (Rows.Count > 0 && Rows[^1].Headline == oldest)
				Rows.RemoveAt(Rows.Count - 1);
			_hasMore = true;
			CanLoadOlder = !IsLoadingOlder;
		}
		return true;
	}

	private void OnZoneChanged()
	{
		foreach (var row in Rows)
			row.RefreshTime();
		_articleMeta.Rebuild();
	}

	private void RebuildRows()
	{
		var selected = SelectedRow?.Key;
		Rows.Clear();
		foreach (var headline in _all)
		{
			var row = new NewsRowViewModel(headline, _displayTime, _alertManager.Matches(headline));
			if (PassesFilter(row))
				Rows.Add(row);
		}
		SelectedRow = Rows.FirstOrDefault(x => x.Key == selected);
	}

	private void ClearHeadlines()
	{
		_all.Clear();
		_keys.Clear();
		_stories.Clear();
		Rows.Clear();
		SelectedRow = null;
	}

	private void StopFeed()
	{
		_feed?.Cancel();
		_feed?.Dispose();
		_feed = null;
		IsLive = false;
		IsLoadingOlder = false;
		IsLoading = _loadingProviders;
	}

	private async Task LoadArticleAsync(NewsRowViewModel row)
	{
		CancelArticle();
		_article = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
		var token = _article.Token;
		_articleKey = row.Key;
		_document = null;
		ArticleTitle = row.Text;
		ArticleUrl = row.Headline.Url;
		var provider =
			Providers
				.FirstOrDefault(x =>
					x.Provider.SourceId == row.Headline.SourceId && x.Provider.Code == row.Provider
				)
				?.Tip
			?? row.Provider;
		_articleMeta.Set(() =>
			string.Join(
				" · ",
				new[]
				{
					_displayTime.Format(row.Headline.Time, "yyyy-MM-dd HH:mm"),
					provider,
					string.Join(", ", row.Headline.Symbols.DefaultIfEmpty(row.Headline.Symbol)),
				}.Where(x => !string.IsNullOrEmpty(x))
			)
		);
		ArticleText = string.Empty;
		ArticleError = null;
		IsArticleDocument = false;

		IsArticleLoading = true;
		try
		{
			var article = await _newsManager.GetArticleAsync(row.Headline, token);
			if (token.IsCancellationRequested)
				return;
			ArticleUrl = article.Url ?? row.Headline.Url;
			if (article.Kind == NewsArticleKind.Binary)
			{
				_document = article;
				IsArticleDocument = true;
				ArticleText = "This article is a document; open it to read it.";
			}
			else
			{
				ArticleText = NewsHelpers.ToPlainText(article);
				if (ArticleText.Length == 0 && HasArticleUrl)
					ArticleText =
						"The feed gives only the headline; open it in the browser to read it.";
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ArticleError =
				row.Headline.SourceId == NewsConstants.IbkrSourceId && !IsConnected
					? "Connect to IB to read the article."
					: ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!token.IsCancellationRequested)
				IsArticleLoading = false;
		}
	}

	private void CancelArticle()
	{
		_article?.Cancel();
		_article?.Dispose();
		_article = null;
		IsArticleLoading = false;
	}
}
