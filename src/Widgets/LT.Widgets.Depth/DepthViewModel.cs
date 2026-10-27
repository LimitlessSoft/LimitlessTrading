using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketDepth;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.MarketDepth;

namespace LT.App.Widgets.Depth;

/// <summary>
/// The order book of the link group's contract as a ladder: asks above (best at the bottom),
/// bids below (best on top), each row with its price, size, running total and exchange. SMART
/// depth by default; the toggle switches to the listing exchange's own depth. Updates are read
/// on the thread pool and drawn at most every <see cref="MarketDepthConstants.UiFlushInterval"/>.
/// </summary>
public partial class DepthViewModel : ConnectedViewModelBase, ILinkable, IWidgetOpenState
{
	private readonly DepthFeed _feed;
	private readonly DispatcherTimer _flushTimer;
	private CancellationToken _session = new(true);
	private ContractDto? _contract;

	public DepthViewModel(
		IMarketDepthManager marketDepthManager,
		IConnectionManager connectionManager,
		LinkGroupStore linkGroupStore
	)
		: base(connectionManager)
	{
		_feed = new DepthFeed(marketDepthManager, OnFeedError);
		for (var i = 0; i < MarketDepthConstants.DefaultRows; i++)
		{
			Asks.Add(new DepthLevelViewModel(false));
			Bids.Add(new DepthLevelViewModel(true));
		}
		Link = new ContractLink(linkGroupStore, () => _contract);
		Link.ContractReceived += OnContractReceived;
		_contract = linkGroupStore.Get(Link.Group);
		ShowContract();

		_flushTimer = new DispatcherTimer(
			MarketDepthConstants.UiFlushInterval,
			DispatcherPriority.Background,
			(_, _) =>
			{
				if (_feed.Pending.Take() is { } book)
					ShowBook(book);
			}
		);
		_flushTimer.Start();
	}

	/// <summary>The contract comes from the link group (watchlist, chart, positions …).</summary>
	public ContractLink Link { get; }

	/// <summary>Worst ask first, so the best ask sits right above the spread.</summary>
	public ObservableCollection<DepthLevelViewModel> Asks { get; } = [];

	/// <summary>Best bid first.</summary>
	public ObservableCollection<DepthLevelViewModel> Bids { get; } = [];

	/// <summary>Frame header title: the contract's ticker.</summary>
	[ObservableProperty]
	public partial string Title { get; set; } = "Depth";

	/// <summary>SMART depth (every exchange with a depth subscription) or the listing's own.</summary>
	[ObservableProperty]
	public partial bool IsSmartDepth { get; set; } = true;

	/// <summary>The contract is routed through SMART; one on a single exchange has only its own.</summary>
	[ObservableProperty]
	public partial bool CanUseSmartDepth { get; set; } = true;

	[ObservableProperty]
	public partial string? SpreadText { get; set; }

	/// <summary>Size resting on the bid side of the shown rows, with its share, e.g. "1,200 · 60%".</summary>
	[ObservableProperty]
	public partial string? BidTotalText { get; set; }

	/// <summary>Size resting on the ask side of the shown rows, with its share.</summary>
	[ObservableProperty]
	public partial string? AskTotalText { get; set; }

	/// <summary>The bid side's share of the size on both sides, 0–1; the bar's split.</summary>
	[ObservableProperty]
	public partial double BidShare { get; set; } = 0.5;

	/// <summary>Either side has size, so the bid/ask totals bar shows.</summary>
	[ObservableProperty]
	public partial bool HasTotals { get; set; }

	/// <summary>Where the rows come from, e.g. "SMART depth" or "IEX depth".</summary>
	[ObservableProperty]
	public partial string? SourceText { get; set; }

	[ObservableProperty]
	public partial string? InfoText { get; set; }

	[ObservableProperty]
	public partial bool IsWaiting { get; set; }

	/// <summary>Raised on the UI thread when a new book starts, so the view can centre the spread.</summary>
	public event Action? BookStarted;

	partial void OnIsSmartDepthChanged(bool value) => Restart();

	// The workspace closed or reopened the widget: a closed one holds no depth subscription
	public void SetOpen(bool isOpen)
	{
		if (isOpen == _feed.IsOpen)
			return;
		_feed.SetOpen(isOpen);
		ClearBook();
		ErrorMessage = null;
		ShowWaiting();
	}

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		Restart();
	}

	protected override void OnDisconnected()
	{
		_feed.Subscribe(null, _session);
		ClearBook();
		IsWaiting = false;
	}

	public override void Dispose()
	{
		_flushTimer.Stop();
		Link.Dispose();
		_feed.Dispose();
		base.Dispose();
	}

	private void OnContractReceived(ContractDto contract)
	{
		if (_contract is { } current && ContractHelpers.IsSameListing(current, contract))
			return;
		_contract = contract;
		Restart();
	}

	private void ShowContract()
	{
		Title = _contract == null ? "Depth" : ContractHelpers.GetDisplayName(_contract);
		InfoText = _contract == null ? "Pick a contract in a widget of the same link group." : null;
		SourceText = null;
		if (_contract == null)
			return;
		CanUseSmartDepth = OrderBookHelpers.IsSmartRouted(_contract);
		var (depth, smart) = OrderBookHelpers.GetDepthContract(_contract, IsSmartDepth);
		SourceText = smart
			? "SMART depth · exchanges you have depth for"
			: $"{depth.Exchange} depth";
	}

	// New contract, connection or mode: drop the old book and subscribe again
	private void Restart()
	{
		ClearBook();
		ErrorMessage = null;
		ShowContract();
		_feed.Subscribe(
			_contract is { } contract && IsConnected
				? new SubscribeOrderBookRequest
				{
					Contract = contract,
					Rows = MarketDepthConstants.DefaultRows,
					PreferSmartDepth = IsSmartDepth,
				}
				: null,
			_session
		);
		ShowWaiting();
	}

	private void ShowWaiting()
	{
		IsWaiting = _feed.IsSubscribed;
		if (_feed.IsSubscribed)
			BookStarted?.Invoke();
	}

	private void OnFeedError(Exception ex, CancellationToken stream) =>
		Dispatcher.UIThread.Post(() =>
		{
			if (stream.IsCancellationRequested)
				return;
			IsWaiting = false;
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		});

	private void ShowBook(OrderBookDto book)
	{
		IsWaiting = false;
		var decimals = OrderBookHelpers.GetPriceDecimals(book);
		var bidTotals = OrderBookHelpers.GetCumulativeSizes(book.Bids);
		var askTotals = OrderBookHelpers.GetCumulativeSizes(book.Asks);
		var largest = book.Bids.Concat(book.Asks).Select(x => x.Size).DefaultIfEmpty().Max();

		for (var i = 0; i < Bids.Count; i++)
			Fill(Bids[i], book.Bids, bidTotals, i, decimals, largest);
		// Asks are listed worst first: the best ask is the last row, right above the spread
		for (var i = 0; i < Asks.Count; i++)
			Fill(Asks[Asks.Count - 1 - i], book.Asks, askTotals, i, decimals, largest);
		ShowTotals(book);

		if (book.Bids.Count == 0 || book.Asks.Count == 0)
		{
			SpreadText = null;
			return;
		}
		var bid = book.Bids[0].Price;
		var ask = book.Asks[0].Price;
		var mid = (bid + ask) / 2;
		var midDecimals =
			Math.Abs(Math.Round(mid, decimals) - mid) < 1e-9 ? decimals : decimals + 1;
		SpreadText =
			$"Spread {FormatPrice(ask - bid, decimals)}  ·  Mid {FormatPrice(mid, midDecimals)}";
	}

	private static void Fill(
		DepthLevelViewModel row,
		IReadOnlyList<OrderBookLevelDto> side,
		IReadOnlyList<decimal> totals,
		int index,
		int decimals,
		decimal largest
	)
	{
		if (index >= side.Count)
		{
			row.Clear();
			return;
		}
		var level = side[index];
		row.Set(
			FormatPrice(level.Price, decimals),
			FormatSize(level.Size),
			FormatSize(totals[index]),
			level.MarketMaker,
			largest > 0 ? (double)(level.Size / largest) : 0,
			index == 0
		);
	}

	private void ShowTotals(OrderBookDto book)
	{
		var (bids, asks, bidShare) = OrderBookHelpers.GetSideTotals(book);
		HasTotals = bidShare != null;
		if (bidShare is not { } share)
			return;
		BidShare = share;
		BidTotalText = $"{FormatSize(bids)} · {share:P0}";
		AskTotalText = $"{1 - share:P0} · {FormatSize(asks)}";
	}

	private void ClearBook()
	{
		foreach (var row in Asks.Concat(Bids))
			row.Clear();
		SpreadText = null;
		HasTotals = false;
		BidTotalText = null;
		AskTotalText = null;
		BidShare = 0.5;
	}

	private static string FormatPrice(double price, int decimals) =>
		price.ToString("#,0." + new string('0', decimals), CultureInfo.CurrentCulture);

	// Forex books are in millions; crypto sizes are fractions of a coin
	private static string FormatSize(decimal size) =>
		size >= 100_000 ? FormatHelpers.Volume(size) : FormatHelpers.Quantity(size);
}
