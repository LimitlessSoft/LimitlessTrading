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
/// Headlines from every news source (<see cref="INewsManager"/>). Linked: the link group's
/// contract, past headlines first, then live ones. All news: the broad tapes of the selected
/// providers, live. Live headlines are read off the UI thread and added in batches; the list keeps
/// <see cref="NewsConstants.MaxHeadlinesShown"/>, newest first. Selecting one loads its article.
/// The header title is a ticker box: a ticker typed there becomes the linked contract.
/// </summary>
public partial class NewsViewModel : ConnectedViewModelBase, ILinkable, IWidgetState, ITickerTitled
{
	private readonly INewsManager _newsManager;
	private readonly IContractManager _contractManager;
	private readonly DisplayTimeStore _displayTime;
	private readonly ZonedText _articleMeta;
	private readonly List<NewsHeadlineDto> _all = [];
	private readonly HashSet<string> _keys = [];
	private readonly ConcurrentQueue<(CancellationToken Feed, NewsHeadlineDto Headline)> _incoming =
		new();
	private CancellationToken _session = new(true);
	private ContractDto? _contract;
	private CancellationTokenSource? _feed;
	private CancellationTokenSource? _restart;
	private CancellationTokenSource? _article;
	private NewsArticleDto? _document;
	private string? _articleKey;
	private HashSet<string>? _savedProviders;
	private bool _hasMore;
	private bool _applyingSelection;
	private bool _loadingProviders;
	private bool _providersLoaded;
	private int _flushQueued;

	public NewsViewModel(
		INewsManager newsManager,
		IContractManager contractManager,
		IConnectionManager connectionManager,
		LinkGroupStore linkGroupStore,
		DisplayTimeStore displayTime
	)
		: base(connectionManager)
	{
		_newsManager = newsManager;
		_contractManager = contractManager;
		_displayTime = displayTime;
		_articleMeta = new ZonedText(x => ArticleMeta = x ?? string.Empty);
		displayTime.ZoneChanged += OnZoneChanged;
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
		InfoText = "Connect to IB to see news.";
	}

	/// <summary>Linked mode follows the group's contract (watchlist, positions, a chart).</summary>
	public ContractLink Link { get; }

	/// <summary>The header title's ticker box: typing a ticker there shows its news.</summary>
	public TickerTitleEditor TickerEditor { get; }

	public ObservableCollection<NewsProviderChip> Providers { get; } = [];

	/// <summary>Headlines passing the text filter, newest first.</summary>
	public ObservableCollection<NewsRowViewModel> Rows { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLinkedMode), nameof(IsAllMode))]
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

	/// <summary>Frame header title: "News · AAPL", "News · All".</summary>
	[ObservableProperty]
	public partial string Title { get; set; } = "News";

	[ObservableProperty]
	public partial string? FilterText { get; set; }

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

	public void LoadState(WidgetPlacementDto placement)
	{
		_savedProviders = placement.NewsProviders?.ToHashSet();
		if (Providers.Count > 0)
			ApplySavedSelection();
		var mode =
			placement.NewsMode is { } saved && Enum.IsDefined(saved) ? saved : NewsMode.Linked;
		if (mode != Mode)
			Mode = mode; // restarts the feed
		else
			StartFeed();
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.NewsMode = Mode;
		placement.NewsProviders =
			Providers.Count == 0 ? _savedProviders?.ToList()
			: Providers.All(x => x.IsSelected) ? null
			: [.. Providers.Where(x => x.IsSelected).Select(x => x.Key)];
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

	/// <summary>Ticker box: resolves the ticker and shows its news.</summary>
	public async Task OpenTickerAsync(string ticker)
	{
		ErrorMessage = null;
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
		Link.Dispose();
		StopFeed();
		CancelArticle();
		_restart?.Cancel();
		base.Dispose();
	}

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		_ = LoadProvidersAsync(session);
	}

	protected override void OnDisconnected()
	{
		_loadingProviders = false;
		StopFeed();
		CancelArticle();
		InfoText = "Connect to IB to see news.";
	}

	partial void OnModeChanged(NewsMode value)
	{
		foreach (var chip in Providers)
			chip.InAllMode = value == NewsMode.All;
		UpdateTitle();
		StartFeed();
	}

	partial void OnFilterTextChanged(string? value) => RebuildRows();

	partial void OnSelectedRowChanged(NewsRowViewModel? value)
	{
		if (value == null)
		{
			CancelArticle();
			_articleKey = null;
			_document = null;
			return;
		}
		if (value.Key != _articleKey)
			_ = LoadArticleAsync(value);
	}

	[RelayCommand(CanExecute = nameof(CanLoadOlder))]
	private async Task LoadOlderAsync()
	{
		if (_contract is not { } contract || _feed is not { } feed || _all.Count == 0)
			return;

		var token = feed.Token;
		CanLoadOlder = false;
		IsLoadingOlder = true;
		try
		{
			var page = await _newsManager.GetHeadlinesAsync(
				new GetNewsHeadlinesRequest
				{
					Contract = contract,
					Providers = GetSelectedProviders(),
					Before = _all[^1].Time,
				},
				token
			);
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
				CanLoadOlder = _hasMore && Mode == NewsMode.Linked;
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
		}
	}

	// A ticker typed here switches to Linked mode and moves the link group along, like a chart's
	private void ShowContract(ContractDto contract)
	{
		var changed = contract.ConId != _contract?.ConId;
		_contract = contract;
		UpdateTitle();
		Link.Publish(contract);
		if (Mode != NewsMode.Linked)
			Mode = NewsMode.Linked; // restarts the feed
		else if (changed)
			StartFeed();
	}

	private void OnContractReceived(ContractDto contract)
	{
		if (contract.ConId == _contract?.ConId)
			return;
		_contract = contract;
		UpdateTitle();
		if (Mode == NewsMode.Linked)
			StartFeed();
	}

	private void UpdateTitle() =>
		Title =
			Mode == NewsMode.All ? "News · All"
			: _contract is { } contract ? $"News · {ContractHelpers.GetDisplayName(contract)}"
			: "News";

	private async Task LoadProvidersAsync(CancellationToken session)
	{
		_loadingProviders = true;
		IsLoading = true;
		InfoText = null;
		ErrorMessage = null;
		try
		{
			var providers = await _newsManager.GetProvidersAsync(session);
			if (session.IsCancellationRequested)
				return;
			foreach (var chip in Providers)
				chip.PropertyChanged -= OnChipChanged;
			Providers.Clear();
			foreach (var provider in providers.OrderBy(x => x.Code, StringComparer.Ordinal))
			{
				var chip = new NewsProviderChip(provider) { InAllMode = Mode == NewsMode.All };
				chip.PropertyChanged += OnChipChanged;
				Providers.Add(chip);
			}
			ApplySavedSelection();
			_loadingProviders = false;
			_providersLoaded = true;
			StartFeed();
		}
		// Disconnected meanwhile: OnDisconnected has reset the state
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			_loadingProviders = false;
			IsLoading = false;
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	private void ApplySavedSelection()
	{
		_applyingSelection = true;
		try
		{
			foreach (var chip in Providers)
				chip.IsSelected = _savedProviders == null || _savedProviders.Contains(chip.Key);
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
		_savedProviders = [.. Providers.Where(x => x.IsSelected).Select(x => x.Key)];
		_restart?.Cancel();
		_restart = new CancellationTokenSource();
		var token = _restart.Token;
		_ = Task.Delay(NewsConstants.ProviderChangeDelay, token)
			.ContinueWith(
				_ => StartFeed(),
				token,
				TaskContinuationOptions.OnlyOnRanToCompletion,
				TaskScheduler.FromCurrentSynchronizationContext()
			);
	}

	private List<NewsProviderDto> GetSelectedProviders() =>
		[.. Providers.Where(x => x.IsSelected).Select(x => x.Provider)];

	// New mode, contract, providers or connection: drop what is shown and start over
	private void StartFeed()
	{
		StopFeed();
		ClearHeadlines();
		ErrorMessage = null;
		_hasMore = false;
		CanLoadOlder = false;

		if (!IsConnected)
		{
			InfoText = "Connect to IB to see news.";
			return;
		}
		if (_loadingProviders)
			return;
		if (!_providersLoaded)
		{
			// The provider list failed earlier: try again
			_ = LoadProvidersAsync(_session);
			return;
		}
		if (Providers.Count == 0)
		{
			InfoText = "No news providers on this account.";
			return;
		}
		var selected = GetSelectedProviders();
		if (selected.Count == 0)
		{
			InfoText = "Pick at least one provider.";
			return;
		}

		_feed = CancellationTokenSource.CreateLinkedTokenSource(_session);
		if (Mode == NewsMode.All)
			_ = RunAllAsync(selected, _feed.Token);
		else if (_contract is { } contract)
			_ = RunLinkedAsync(contract, selected, _feed.Token);
		else
			InfoText = "Pick a symbol in a widget of the same link group.";
	}

	private async Task RunLinkedAsync(
		ContractDto contract,
		List<NewsProviderDto> providers,
		CancellationToken feed
	)
	{
		InfoText = null;
		IsLoading = true;
		try
		{
			var page = await _newsManager.GetHeadlinesAsync(
				new GetNewsHeadlinesRequest { Contract = contract, Providers = providers },
				feed
			);
			if (feed.IsCancellationRequested)
				return;
			foreach (var headline in page.Headlines)
				AddHeadline(headline, trim: false);
			_hasMore = page.HasMore;
			CanLoadOlder = _hasMore;
			if (_all.Count == 0)
				InfoText = $"No recent news for {ContractHelpers.GetDisplayName(contract)}.";
		}
		catch (OperationCanceledException)
		{
			return;
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

		StartLive(new SubscribeNewsRequest { Contract = contract, Providers = providers }, feed);
	}

	private async Task RunAllAsync(List<NewsProviderDto> providers, CancellationToken feed)
	{
		InfoText = null;
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
					? "No provider on this account has a broad tape; use Linked mode."
					: $"No broad tape for the selected providers. Pick one of: {string.Join(", ", available)}.";
			return;
		}
		InfoText = without.Count == 0 ? null : $"No broad tape: {string.Join(", ", without)}.";
		StartLive(new SubscribeNewsRequest { Providers = tapes }, feed);
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

	// Live headlines push the oldest out past the cap; "Load older" may grow the list beyond it
	private bool AddHeadline(NewsHeadlineDto headline, bool trim)
	{
		if (NewsHelpers.Insert(_all, _keys, headline) < 0)
			return false;
		if (NewsHelpers.Matches(headline, FilterText))
			Rows.Insert(
				NewsHelpers.GetInsertIndex(Rows, x => x.Time, headline.Time),
				new NewsRowViewModel(headline, _displayTime)
			);

		while (trim && _all.Count > NewsConstants.MaxHeadlinesShown)
		{
			var oldest = _all[^1];
			_all.RemoveAt(_all.Count - 1);
			_keys.Remove(NewsHelpers.GetKey(oldest));
			if (Rows.Count > 0 && Rows[^1].Headline == oldest)
				Rows.RemoveAt(Rows.Count - 1);
			_hasMore = true;
			CanLoadOlder = Mode == NewsMode.Linked && !IsLoadingOlder;
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
		foreach (var headline in _all.Where(x => NewsHelpers.Matches(x, FilterText)))
			Rows.Add(new NewsRowViewModel(headline, _displayTime));
		SelectedRow = Rows.FirstOrDefault(x => x.Key == selected);
	}

	private void ClearHeadlines()
	{
		_all.Clear();
		_keys.Clear();
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
		_article = CancellationTokenSource.CreateLinkedTokenSource(_session);
		var token = _article.Token;
		_articleKey = row.Key;
		_document = null;
		ArticleTitle = row.Text;
		var provider =
			Providers.FirstOrDefault(x => x.Provider.Code == row.Provider)?.Tip ?? row.Provider;
		_articleMeta.Set(() =>
			string.Join(
				" · ",
				new[]
				{
					_displayTime.Format(row.Headline.Time, "yyyy-MM-dd HH:mm"),
					provider,
					row.Headline.Symbol,
				}.Where(x => !string.IsNullOrEmpty(x))
			)
		);
		ArticleText = string.Empty;
		ArticleError = null;
		IsArticleDocument = false;
		if (!IsConnected)
		{
			ArticleError = "Connect to IB to read the article.";
			return;
		}

		IsArticleLoading = true;
		try
		{
			var article = await _newsManager.GetArticleAsync(row.Headline, token);
			if (token.IsCancellationRequested)
				return;
			if (article.Kind == NewsArticleKind.Binary)
			{
				_document = article;
				IsArticleDocument = true;
				ArticleText = "This article is a document; open it to read it.";
			}
			else
				ArticleText = NewsHelpers.ToPlainText(article);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ArticleError = ErrorMessageHelpers.ToUserMessage(ex);
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
