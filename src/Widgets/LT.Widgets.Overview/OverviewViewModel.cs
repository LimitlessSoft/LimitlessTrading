using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.Overview;

/// <summary>
/// One contract in depth: a live quote header above tabs (Basic info now; fundamentals, news,
/// options … later, see <see cref="OverviewTabViewModel"/>). The contract comes from the link
/// group, a ticker typed into the header title or a ticker dropped on the widget, and is saved
/// with the widget. Its IB details are read once per contract (cached) and handed to every tab;
/// the quote takes one market data line (with the 52-week range and average volume), only while
/// the widget is shown.
/// </summary>
public partial class OverviewViewModel
	: ConnectedViewModelBase,
		ILinkable,
		ITickerTitled,
		ITickerDropTarget,
		IWidgetState
{
	private readonly IContractManager _contractManager;
	private readonly IMarketDataManager _marketDataManager;
	private readonly LinkGroupStore _linkGroupStore;
	private readonly DispatcherTimer _flushTimer;
	private readonly OverviewField _bid = new("Bid", FormatHelpers.Empty);
	private readonly OverviewField _ask = new("Ask", FormatHelpers.Empty);
	private readonly OverviewField _open = new("Open", FormatHelpers.Empty);
	private readonly OverviewField _previousClose = new("Prev close", FormatHelpers.Empty);
	private readonly OverviewField _dayRange = new("Day range", FormatHelpers.Empty);
	private readonly OverviewField _yearRange = new("52-week range", FormatHelpers.Empty);
	private readonly OverviewField _volume = new("Volume", FormatHelpers.Empty);
	private readonly OverviewField _averageVolume = new(
		"Avg volume",
		FormatHelpers.Empty,
		"90-day average daily volume"
	);
	private CancellationToken _session = new(true);
	private ContractDto? _contract;
	private ContractDetailsDto? _details;

	// Per contract: cancelled when another one is shown, so nothing late writes into it
	private CancellationTokenSource _load = new();
	private CancellationTokenSource? _quotes;

	// Replaced per stream, so a stream cancelled with the previous contract can't write here
	private LatestValue<QuoteDto> _pendingQuote = new();
	private bool _detailsLoaded;

	public OverviewViewModel(
		IContractManager contractManager,
		IMarketDataManager marketDataManager,
		ISharesOutstandingManager sharesOutstandingManager,
		IConnectionManager connectionManager,
		LinkGroupStore linkGroupStore,
		TimeProvider timeProvider
	)
		: base(connectionManager)
	{
		_contractManager = contractManager;
		_marketDataManager = marketDataManager;
		_linkGroupStore = linkGroupStore;
		Tabs = [new BasicInfoTabViewModel(sharesOutstandingManager, timeProvider)];
		SelectedTab = Tabs[0];
		QuoteStats =
		[
			_bid,
			_ask,
			_open,
			_previousClose,
			_dayRange,
			_yearRange,
			_volume,
			_averageVolume,
		];

		Link = new ContractLink(linkGroupStore, () => _contract);
		Link.ContractReceived += OnContractReceived;
		TickerEditor = new TickerTitleEditor(
			contractManager,
			() => IsConnected,
			OpenTickerAsync,
			OpenContract
		);
		SetContract(linkGroupStore.Get(Link.Group));

		_flushTimer = new DispatcherTimer(
			MarketDataConstants.UiFlushInterval,
			DispatcherPriority.Background,
			(_, _) => FlushQuote()
		);
		_flushTimer.Start();
	}

	/// <summary>The contract comes from the link group (watchlist, chart, positions …).</summary>
	public ContractLink Link { get; }

	/// <summary>The header title's ticker box: a ticker typed there is shown here.</summary>
	public TickerTitleEditor TickerEditor { get; }

	/// <summary>The tabs, in order; each shows its part of the contract.</summary>
	public ObservableCollection<OverviewTabViewModel> Tabs { get; }

	/// <summary>Bid, ask, open, previous close, ranges and volumes under the price.</summary>
	public IReadOnlyList<OverviewField> QuoteStats { get; }

	/// <summary>Frame header title: the contract's ticker.</summary>
	[ObservableProperty]
	public partial string Title { get; set; } = "Overview";

	[ObservableProperty]
	public partial OverviewTabViewModel? SelectedTab { get; set; }

	/// <summary>A contract is shown (else <see cref="InfoText"/> says how to pick one).</summary>
	[ObservableProperty]
	public partial bool HasContract { get; set; }

	[ObservableProperty]
	public partial string? InfoText { get; set; }

	/// <summary>IB's long name ("SPDR S&amp;P 500 ETF TRUST"); the symbol until the details load.</summary>
	[ObservableProperty]
	public partial string? NameText { get; set; }

	/// <summary>Listing exchange · currency · type, e.g. "ARCA · USD · Stock".</summary>
	[ObservableProperty]
	public partial string? ListingText { get; set; }

	/// <summary>Loading / not connected / not found for the details; null once they are shown.</summary>
	[ObservableProperty]
	public partial string? DetailsStatus { get; set; }

	[ObservableProperty]
	public partial bool IsLoadingDetails { get; set; }

	[ObservableProperty]
	public partial string PriceText { get; set; } = FormatHelpers.Empty;

	/// <summary>"+3.12 (+0.46%)" against the previous close; null without both prices.</summary>
	[ObservableProperty]
	public partial string? ChangeText { get; set; }

	[ObservableProperty]
	public partial bool IsUp { get; set; }

	[ObservableProperty]
	public partial bool IsDown { get; set; }

	/// <summary>Waiting / not connected / IB's refusal; "Delayed" or "Halted" next to a live quote.</summary>
	[ObservableProperty]
	public partial string? QuoteStatus { get; set; }

	/// <summary>Shows the newest quote the line brought since the last flush (the UI timer's tick).</summary>
	public void FlushQuote()
	{
		if (_pendingQuote.Take() is { } quote)
			ShowQuote(quote);
	}

	public void LoadState(WidgetPlacementDto placement)
	{
		SelectedTab = Tabs.FirstOrDefault(x => x.Kind == placement.OverviewTab) ?? Tabs[0];
		// The link group's contract, when it has one, wins: linked widgets agree
		if (
			placement.OverviewContract is not { ConId: > 0 } saved
			|| Link.Group != LinkGroup.None && _linkGroupStore.Get(Link.Group) != null
		)
			return;
		SetContract(saved);
		Link.Publish(saved);
	}

	public void SaveState(WidgetPlacementDto placement)
	{
		placement.OverviewContract = _contract;
		placement.OverviewTab = SelectedTab?.Kind;
	}

	/// <summary>Ticker box: resolves the ticker and shows it.</summary>
	public async Task OpenTickerAsync(string ticker)
	{
		ErrorMessage = null;
		try
		{
			Show(await _contractManager.ResolveTickerAsync(ticker));
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
		Show(contract);
	}

	/// <summary>A ticker dragged from a list: shown here and sent to the link group.</summary>
	public void DropContract(ContractDto contract)
	{
		ErrorMessage = null;
		_contractManager.AddRecentContract(contract);
		Show(contract);
	}

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		_ = LoadDetailsAsync();
	}

	protected override void OnDisconnected()
	{
		StopQuotes();
		if (_contract != null)
			QuoteStatus = "Not connected";
		if (!_detailsLoaded)
			_ = LoadDetailsAsync();
	}

	protected override void OnStreamsStarted(CancellationToken streams) => StartQuotes(streams);

	// Hidden: the line goes back, the last quote stays on screen
	protected override void OnStreamsStopped() => StopQuotes();

	public override void Dispose()
	{
		_flushTimer.Stop();
		Link.Dispose();
		StopQuotes();
		_load.Cancel();
		foreach (var tab in Tabs)
			tab.Dispose();
		base.Dispose();
	}

	// Typed or dropped here: the link group follows, like a chart's
	private void Show(ContractDto contract)
	{
		if (contract.ConId != _contract?.ConId)
			SetContract(contract);
		Link.Publish(contract);
	}

	private void OnContractReceived(ContractDto contract)
	{
		if (contract.ConId != _contract?.ConId)
			SetContract(contract);
	}

	private void SetContract(ContractDto? contract)
	{
		// Not disposed: work of the old contract may still hold its token (no timer to free)
		_load.Cancel();
		_load = new CancellationTokenSource();
		StopQuotes();
		_contract = contract;
		_details = null;
		_detailsLoaded = false;
		ErrorMessage = null;
		ClearQuote();

		HasContract = contract != null;
		Title = contract == null ? "Overview" : ContractHelpers.GetDisplayName(contract);
		InfoText =
			contract == null
				? "Pick a contract in a widget of the same link group, type a ticker in the title or drop one here."
				: null;
		ShowHeading();
		ShowTabs();
		_ = LoadDetailsAsync();
		if (IsStreaming)
			StartQuotes(StreamsToken);
	}

	// Once per contract and connection; kept through a disconnect (definitions barely change)
	private async Task LoadDetailsAsync()
	{
		var contract = _contract;
		var load = _load.Token;
		DetailsStatus = null;
		IsLoadingDetails = false;
		if (contract == null || _detailsLoaded)
			return;
		if (contract.ConId <= 0)
		{
			DetailsStatus = $"{Title} isn't a resolved IB contract: no details to load.";
			return;
		}
		if (!IsConnected)
		{
			DetailsStatus = "Not connected to IB: the details and the quote load once connected.";
			return;
		}

		using var linked = CancellationTokenSource.CreateLinkedTokenSource(load, _session);
		IsLoadingDetails = true;
		DetailsStatus = "Loading details…";
		try
		{
			var details = await _contractManager.GetDetailsAsync(contract, linked.Token);
			if (load.IsCancellationRequested)
				return;
			_detailsLoaded = true;
			_details = details;
			DetailsStatus =
				details == null
					? $"IB has no details for {Title} (contract ID {contract.ConId})."
					: null;
			ShowHeading();
			ShowTabs();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (load.IsCancellationRequested)
				return;
			DetailsStatus = null;
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
		finally
		{
			if (!load.IsCancellationRequested)
				IsLoadingDetails = false;
		}
	}

	private void ShowHeading()
	{
		if (_contract is not { } contract)
		{
			NameText = ListingText = null;
			return;
		}
		NameText = _details?.LongName is { Length: > 0 } name ? name : contract.Symbol;
		ListingText = string.Join(
			" · ",
			new[]
			{
				ContractHelpers.GetListingExchange(contract),
				contract.Currency,
				OverviewHelpers.GetSecurityTypeName(contract.SecurityType),
			}.Where(x => !string.IsNullOrWhiteSpace(x))
		);
	}

	private void ShowTabs()
	{
		foreach (var tab in Tabs)
			tab.Show(_contract, _details, _load.Token);
	}

	private void StartQuotes(CancellationToken streams)
	{
		if (_contract is not { } contract || _quotes != null)
			return;
		_quotes = CancellationTokenSource.CreateLinkedTokenSource(streams);
		var token = _quotes.Token;
		var pending = _pendingQuote = new LatestValue<QuoteDto>();
		if (!HasPrice())
			QuoteStatus = "Waiting for a quote…";
		_ = Task.Run(
			async () =>
			{
				try
				{
					await foreach (
						var quote in _marketDataManager
							.SubscribeQuotesAsync(
								contract,
								token,
								MarketDataConstants.AverageVolumeGenericTick,
								MarketDataStreamOwners.Overview
							)
							.ConfigureAwait(false)
					)
						pending.Set(quote);
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					var message = ErrorMessageHelpers.ToUserMessage(ex);
					Dispatcher.UIThread.Post(() =>
					{
						if (!token.IsCancellationRequested)
							QuoteStatus = $"No quote: {message}";
					});
				}
			},
			token
		);
	}

	private void StopQuotes()
	{
		_quotes?.Cancel();
		_quotes?.Dispose();
		_quotes = null;
	}

	private bool HasPrice() => PriceText != FormatHelpers.Empty;

	private void ShowQuote(QuoteDto quote)
	{
		if (_quotes == null)
			return;
		var price = TickTypeHelpers.GetDisplayPrice(quote);
		PriceText = FormatHelpers.Price(price);
		var change = TickTypeHelpers.GetChange(quote);
		ChangeText = change is { } c
			? $"{FormatHelpers.SignedMoney(c)} ({FormatHelpers.SignedPercent(TickTypeHelpers.GetChangePercent(quote))})"
			: null;
		IsUp = change > 0;
		IsDown = change < 0;
		QuoteStatus =
			quote.IsHalted ? "Halted"
			: quote.IsDelayed ? "Delayed"
			: TickTypeHelpers.HasPrice(quote) ? null
			: "Waiting for a quote…";

		_bid.Value = FormatSide(quote.Bid, quote.BidSize);
		_ask.Value = FormatSide(quote.Ask, quote.AskSize);
		_open.Value = FormatHelpers.Price(quote.Open);
		_previousClose.Value = FormatHelpers.Price(quote.Close);
		_dayRange.Value = FormatRange(quote.Low, quote.High);
		_yearRange.Value = FormatRange(quote.Week52Low, quote.Week52High);
		_volume.Value = FormatSize(quote.Volume);
		_averageVolume.Value = FormatHelpers.Volume(quote.AverageVolume);
		foreach (var tab in Tabs)
			tab.OnQuote(quote);
	}

	private void ClearQuote()
	{
		PriceText = FormatHelpers.Empty;
		ChangeText = null;
		IsUp = IsDown = false;
		QuoteStatus = _contract != null && !IsConnected ? "Not connected" : null;
		foreach (var field in QuoteStats)
			field.Value = FormatHelpers.Empty;
		foreach (var tab in Tabs)
			tab.OnQuote(null);
	}

	private static string FormatSide(double? price, decimal? size) =>
		price == null ? FormatHelpers.Empty
		: size is > 0 ? $"{FormatHelpers.Price(price)} × {FormatSize(size)}"
		: FormatHelpers.Price(price);

	// Crypto sizes are fractions of a coin: whole numbers would read 0
	private static string FormatSize(decimal? size) =>
		size >= 100_000 ? FormatHelpers.Volume(size) : FormatHelpers.Quantity(size);

	private static string FormatRange(double? low, double? high) =>
		low == null && high == null
			? FormatHelpers.Empty
			: $"{FormatHelpers.Price(low)} – {FormatHelpers.Price(high)}";
}
