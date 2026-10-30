using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.News;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.News;

namespace LT.App.Widgets.Overview;

/// <summary>
/// The contract's recent headlines, newest first, from every news source through
/// <see cref="INewsManager"/>: IB's API providers of the account (BRFG, DJ-N …) while connected,
/// the app's own feeds when they are on in Settings, and the local archive (so IB's headlines
/// read before still show while disconnected). The providers are the ones a News widget starts
/// with (<see cref="NewsProviderDto.SelectedByDefault"/>) that have news per symbol. Past
/// headlines only, no live subscription (no market data line): loaded the first time the tab is
/// shown for a contract, again on connect / disconnect (IB's providers come and go) and on
/// Refresh; "Load older" pages back. Selecting a headline loads its article.
/// </summary>
public partial class NewsTabViewModel : OverviewTabViewModel
{
	private readonly INewsManager _newsManager;
	private readonly DisplayTimeStore _displayTime;
	private readonly HashSet<string> _keys = [];
	private ContractDto? _contract;
	private CancellationToken _load;
	private CancellationTokenSource? _feed;
	private CancellationTokenSource? _article;
	private IReadOnlyList<NewsProviderDto> _providers = [];
	private bool _selected;
	private bool _requested;
	private bool _connected;

	public NewsTabViewModel(INewsManager newsManager, DisplayTimeStore displayTime)
	{
		_newsManager = newsManager;
		_displayTime = displayTime;
		displayTime.ZoneChanged += OnZoneChanged;
	}

	public override OverviewTab Kind => OverviewTab.News;

	public override string Header => "News";

	/// <summary>Headlines, newest first.</summary>
	public ObservableCollection<NewsTabRow> Rows { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasArticle))]
	public partial NewsTabRow? SelectedRow { get; set; }

	/// <summary>Loading, or why there are no headlines (no providers, no news, an error).</summary>
	[ObservableProperty]
	public partial string? StatusText { get; set; }

	[ObservableProperty]
	public partial bool IsError { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
	public partial bool IsLoading { get; set; }

	/// <summary>The providers asked, e.g. "BRFG, DJ-N · IB"; null before a load.</summary>
	[ObservableProperty]
	public partial string? SourceText { get; set; }

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

	[ObservableProperty]
	public partial string? ArticleError { get; set; }

	/// <summary>The story on the web ("Open in browser"); null when the source gives none.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasArticleUrl))]
	public partial string? ArticleUrl { get; set; }

	public bool HasArticleUrl => !string.IsNullOrEmpty(ArticleUrl);

	public override void Show(
		ContractDto? contract,
		ContractDetailsDto? details,
		CancellationToken load
	)
	{
		_load = load;
		if (contract?.ConId == _contract?.ConId && contract?.Symbol == _contract?.Symbol)
			return;
		_contract = contract;
		Reset();
	}

	public override void OnSelected(bool selected)
	{
		_selected = selected;
		LoadIfShown();
	}

	// IB's providers join on connect and leave on disconnect: load again (now, or when shown)
	public override void OnConnectionChanged(bool connected)
	{
		if (connected == _connected)
			return;
		_connected = connected;
		if (_contract != null)
			Reset();
	}

	/// <summary>The view couldn't open the article's link.</summary>
	public void ShowArticleError(string message) => ArticleError = message;

	public override void Dispose()
	{
		_displayTime.ZoneChanged -= OnZoneChanged;
		StopFeed();
		CancelArticle();
		base.Dispose();
	}

	partial void OnSelectedRowChanged(NewsTabRow? value)
	{
		if (value == null)
		{
			CancelArticle();
			ArticleUrl = null;
			return;
		}
		_ = LoadArticleAsync(value);
	}

	private bool CanRefresh() => !IsLoading;

	[RelayCommand(CanExecute = nameof(CanRefresh))]
	private void Refresh()
	{
		if (_contract != null)
			Reset();
	}

	[RelayCommand(CanExecute = nameof(CanLoadOlder))]
	private async Task LoadOlderAsync()
	{
		if (_feed is not { } feed || _contract is not { } contract || Rows.Count == 0)
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
					Providers = _providers,
					Before = Rows[^1].Headline.Time,
				},
				token
			);
			if (token.IsCancellationRequested)
				return;
			Add(page.Headlines);
			CanLoadOlder = page.HasMore && Rows.Count > 0;
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!token.IsCancellationRequested)
				ShowError(ex);
		}
		finally
		{
			if (!token.IsCancellationRequested)
				IsLoadingOlder = false;
		}
	}

	// Another contract or connection: drop what is shown and load again once the tab is shown
	private void Reset()
	{
		StopFeed();
		_requested = false;
		_keys.Clear();
		Rows.Clear();
		SelectedRow = null;
		StatusText = SourceText = null;
		IsError = IsLoading = CanLoadOlder = false;
		LoadIfShown();
	}

	private void LoadIfShown()
	{
		if (!_selected || _requested || _contract is not { } contract)
			return;
		_requested = true;
		_feed = CancellationTokenSource.CreateLinkedTokenSource(_load);
		_ = LoadAsync(contract, _feed.Token);
	}

	// Called and continued on the UI thread: every await comes back to it
	private async Task LoadAsync(ContractDto contract, CancellationToken feed)
	{
		IsLoading = true;
		StatusText = "Loading news…";
		try
		{
			var all = await _newsManager.GetProvidersAsync(feed);
			if (feed.IsCancellationRequested)
				return;
			_providers = NewsHelpers.GetSymbolNewsProviders(all);
			if (_providers.Count == 0)
			{
				StatusText = GetNoProvidersText(all.Count > 0);
				return;
			}
			SourceText = NewsHelpers.FormatProviders(_providers);

			var page = await _newsManager.GetHeadlinesAsync(
				new GetNewsHeadlinesRequest { Contract = contract, Providers = _providers },
				feed
			);
			if (feed.IsCancellationRequested)
				return;
			Add(page.Headlines);
			CanLoadOlder = page.HasMore && Rows.Count > 0;
			StatusText =
				Rows.Count == 0
					? $"No recent news for {ContractHelpers.GetDisplayName(contract)}."
					: null;
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (!feed.IsCancellationRequested)
				ShowError(ex);
		}
		finally
		{
			if (!feed.IsCancellationRequested)
				IsLoading = false;
		}
	}

	private string GetNoProvidersText(bool hasProviders) =>
		hasProviders ? "None of the news providers has news per symbol."
		: _connected
			? "No news providers: IB's come with the account's news subscriptions (Client Portal › Settings › Market Data Subscriptions); the app's own feeds can be turned on in Settings › News."
		: "Not connected to IB: connect for IB's news, or turn on the app's own feeds in Settings › News.";

	private void ShowError(Exception ex)
	{
		StatusText = ErrorMessageHelpers.ToUserMessage(ex);
		IsError = true;
	}

	private void Add(IEnumerable<NewsHeadlineDto> headlines)
	{
		foreach (var headline in headlines)
		{
			var row = new NewsTabRow(headline, _displayTime);
			if (!_keys.Add(row.Key))
				continue;
			Rows.Insert(NewsHelpers.GetInsertIndex(Rows, x => x.Headline.Time, headline.Time), row);
		}
	}

	private async Task LoadArticleAsync(NewsTabRow row)
	{
		CancelArticle();
		_article = CancellationTokenSource.CreateLinkedTokenSource(_load);
		var token = _article.Token;
		ArticleTitle = row.Text;
		ArticleUrl = row.Headline.Url;
		ArticleMeta = FormatMeta(row);
		ArticleText = string.Empty;
		ArticleError = null;
		IsArticleLoading = true;
		try
		{
			var article = await _newsManager.GetArticleAsync(row.Headline, token);
			if (token.IsCancellationRequested)
				return;
			ArticleUrl = article.Url ?? row.Headline.Url;
			ArticleText =
				article.Kind == NewsArticleKind.Binary
					? "This article is a document; open it from the News widget to read it."
					: NewsHelpers.ToPlainText(article);
			if (ArticleText.Length == 0 && HasArticleUrl)
				ArticleText =
					"The feed gives only the headline; open it in the browser to read it.";
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (token.IsCancellationRequested)
				return;
			ArticleError =
				row.Headline.SourceId == NewsConstants.IbkrSourceId && !_connected
					? "Connect to IB to read the article."
					: ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!token.IsCancellationRequested)
				IsArticleLoading = false;
		}
	}

	private void OnZoneChanged()
	{
		foreach (var row in Rows)
			row.RefreshTime();
		if (SelectedRow is { } selected)
			ArticleMeta = FormatMeta(selected);
	}

	private string FormatMeta(NewsTabRow row) =>
		$"{_displayTime.Format(row.Headline.Time, "yyyy-MM-dd HH:mm")} · {row.SourceTip}";

	private void StopFeed()
	{
		_feed?.Cancel();
		_feed?.Dispose();
		_feed = null;
		IsLoadingOlder = false;
	}

	private void CancelArticle()
	{
		_article?.Cancel();
		_article?.Dispose();
		_article = null;
		IsArticleLoading = false;
	}
}
