using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.App.Workspace;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Requests.Orders;

namespace LT.App.Widgets.OrderTicket;

/// <summary>A choice in a ticket drop-down: the value and how it reads.</summary>
public sealed record Choice<T>(T Value, string Label)
{
	public override string ToString() => Label;
}

/// <summary>
/// Places orders for the link group's contract. An order is reviewed first (validated, spelled
/// out with the account and endpoint) and only sent on confirm; its statuses follow below.
/// </summary>
public partial class OrderTicketViewModel : ConnectedViewModelBase, ILinkable
{
	private readonly IOrderManager _orderManager;
	private readonly IMarketDataManager _marketDataManager;
	private readonly IConnectionManager _connectionManager;
	private readonly IMessageLogManager _messageLogManager;
	private readonly AccountStore _accountStore;
	private readonly LatestValue<QuoteDto> _pendingQuote = new();
	private readonly DispatcherTimer _flushTimer;
	private CancellationToken _session = new(true);
	private CancellationTokenSource? _quotes;
	private ContractDto? _contract;
	private QuoteDto? _quote;
	private PlaceOrderRequest? _reviewed;
	private bool _pinned;

	public OrderTicketViewModel(
		IOrderManager orderManager,
		IMarketDataManager marketDataManager,
		IConnectionManager connectionManager,
		IMessageLogManager messageLogManager,
		AccountStore accountStore,
		LinkGroupStore linkGroupStore
	)
		: base(connectionManager)
	{
		_orderManager = orderManager;
		_marketDataManager = marketDataManager;
		_connectionManager = connectionManager;
		_messageLogManager = messageLogManager;
		_accountStore = accountStore;
		Link = new ContractLink(linkGroupStore, () => _contract);
		Link.ContractReceived += SetContract;
		SelectedAccount = accountStore.SelectedAccount;
		SetContract(linkGroupStore.Get(Link.Group));

		_flushTimer = new DispatcherTimer(
			MarketDataConstants.UiFlushInterval,
			DispatcherPriority.Background,
			(_, _) => FlushQuote()
		);
		_flushTimer.Start();
	}

	/// <summary>The contract comes from the link group (watchlist, positions, a chart).</summary>
	public ContractLink Link { get; }

	public ObservableCollection<string> Accounts => _accountStore.Accounts;

	private static readonly IReadOnlyList<Choice<OrderType>> OrderTypeChoices =
	[
		new(OrderType.Market, "Market"),
		new(OrderType.Limit, "Limit"),
	];

	public IReadOnlyList<Choice<OrderType>> OrderTypes => OrderTypeChoices;

	public ObservableCollection<Choice<TimeInForce>> TimeInForces { get; } = [];

	/// <summary>Orders sent from this ticket since the app started, newest first.</summary>
	public ObservableCollection<TicketOrderViewModel> Orders { get; } = [];

	public bool HasOrders => Orders.Count > 0;

	[ObservableProperty]
	public partial string Title { get; set; } = "Order ticket";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasContract))]
	public partial string? ContractText { get; set; }

	public bool HasContract => _contract != null;

	[ObservableProperty]
	public partial string? SelectedAccount { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsSell))]
	public partial bool IsBuy { get; set; } = true;

	public bool IsSell
	{
		get => !IsBuy;
		set => IsBuy = !value;
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLimit), nameof(CanUseAmount))]
	public partial Choice<OrderType> SelectedOrderType { get; set; } = OrderTypeChoices[0];

	public bool IsLimit => SelectedOrderType.Value == OrderType.Limit;

	/// <summary>An amount of money to spend works only for market orders.</summary>
	public bool CanUseAmount => SelectedOrderType.Value == OrderType.Market;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SizeLabel))]
	public partial bool IsAmount { get; set; }

	public string SizeLabel => IsAmount ? "Amount ($)" : "Quantity";

	[ObservableProperty]
	public partial string SizeText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string LimitPriceText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial Choice<TimeInForce>? SelectedTimeInForce { get; set; }

	[ObservableProperty]
	public partial string QuoteText { get; set; } = string.Empty;

	/// <summary>Why the ticket is pre-filled (e.g. closing a position); null for a plain ticket.</summary>
	[ObservableProperty]
	public partial string? DraftText { get; set; }

	[ObservableProperty]
	public partial string? EstimateText { get; set; }

	[ObservableProperty]
	public partial bool IsReviewing { get; set; }

	[ObservableProperty]
	public partial string? ReviewText { get; set; }

	[ObservableProperty]
	public partial string? EndpointText { get; set; }

	[ObservableProperty]
	public partial bool IsSending { get; set; }

	partial void OnIsBuyChanged(bool value) => OnTicketChanged();

	partial void OnSelectedOrderTypeChanged(Choice<OrderType> value)
	{
		if (value.Value != OrderType.Market)
			IsAmount = false;
		UpdateTimeInForces();
		OnTicketChanged();
	}

	partial void OnIsAmountChanged(bool value) => OnTicketChanged();

	partial void OnSizeTextChanged(string value) => OnTicketChanged();

	partial void OnLimitPriceTextChanged(string value) => OnTicketChanged();

	partial void OnSelectedTimeInForceChanged(Choice<TimeInForce>? value) => OnTicketChanged();

	partial void OnSelectedAccountChanged(string? value) => OnTicketChanged();

	/// <summary>Validates the ticket and shows what would be sent; nothing goes to IB yet.</summary>
	[RelayCommand]
	private void Review()
	{
		var request = BuildRequest();
		try
		{
			_orderManager.ValidateOrder(request);
		}
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			return;
		}
		ErrorMessage = null;
		_reviewed = request;
		ReviewText = OrderHelpers.Describe(request);
		EndpointText = $"Sends to {_connectionManager.Status.Endpoint}";
		IsReviewing = true;
	}

	[RelayCommand]
	private void Back() => StopReview();

	/// <summary>
	/// Fills the ticket from <paramref name="draft"/> and pins it to that contract: it leaves its
	/// link group, so picking another contract elsewhere can't swap what it would close. Nothing
	/// is reviewed or sent.
	/// </summary>
	public void Prefill(OrderDraftDto draft)
	{
		Link.Group = LinkGroup.None;
		_pinned = true;
		ApplyContract(draft.Contract);
		SelectedAccount = draft.Account;
		IsBuy = draft.Action == OrderAction.Buy;
		SelectedOrderType = OrderTypeChoices[0];
		IsAmount = false;
		SizeText = OrderHelpers.FormatInput(draft.Quantity);
		LimitPriceText = string.Empty;
		DraftText = draft.Note;
	}

	/// <summary>Sends the reviewed order to IB.</summary>
	[RelayCommand]
	private async Task SendAsync()
	{
		if (_reviewed is not { } request || IsSending)
			return;
		IsSending = true;
		var description = OrderHelpers.Describe(request);
		try
		{
			var placed = await _orderManager.PlaceOrderAsync(request, _session);
			var order = new TicketOrderViewModel(placed.OrderId, description);
			Orders.Insert(0, order);
			OnPropertyChanged(nameof(HasOrders));
			StopReview();
			SizeText = string.Empty;
			Log(MessageSeverity.Info, $"Order {placed.OrderId} sent: {description}");
			_ = FollowAsync(order, placed.Statuses, _session);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
			Log(MessageSeverity.Error, $"Order not sent ({description}): {ErrorMessage}");
		}
		finally
		{
			IsSending = false;
		}
	}

	[RelayCommand]
	private async Task CancelOrderAsync(TicketOrderViewModel order)
	{
		try
		{
			await _orderManager.CancelOrderAsync(order.OrderId, _session);
			Log(MessageSeverity.Info, $"Cancel requested for order {order.OrderId}");
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			ErrorMessage = ErrorMessageHelpers.ToUserMessage(ex);
		}
	}

	protected override void OnConnected(CancellationToken session)
	{
		_session = session;
		if (SelectedAccount == null || !Accounts.Contains(SelectedAccount))
			SelectedAccount = _accountStore.SelectedAccount;
		StartQuotes();
	}

	protected override void OnDisconnected()
	{
		StopQuotes();
		StopReview();
	}

	public override void Dispose()
	{
		_flushTimer.Stop();
		Link.Dispose();
		StopQuotes();
		base.Dispose();
	}

	private void SetContract(ContractDto? contract)
	{
		if (!_pinned)
			ApplyContract(contract);
	}

	private void ApplyContract(ContractDto? contract)
	{
		if (contract != null && ContractHelpers.IsSameListing(contract, _contract ?? new()))
			return;
		_contract = contract;
		_quote = null;
		QuoteText = string.Empty;
		StopReview();
		ContractText =
			contract == null
				? null
				: $"{ContractHelpers.GetDisplayName(contract)} · {contract.SecurityType} · {ContractHelpers.GetListingExchange(contract)} · {contract.Currency}";
		Title =
			contract == null
				? "Order ticket"
				: $"Order ticket · {ContractHelpers.GetDisplayName(contract)}";
		OnPropertyChanged(nameof(HasContract));
		UpdateTimeInForces();
		if (IsConnected)
			StartQuotes();
	}

	private void UpdateTimeInForces()
	{
		var allowed = OrderHelpers.GetTimeInForces(
			_contract?.SecurityType ?? string.Empty,
			SelectedOrderType.Value
		);
		var keep = SelectedTimeInForce?.Value;
		TimeInForces.Clear();
		foreach (var tif in allowed)
			TimeInForces.Add(new Choice<TimeInForce>(tif, tif.ToString().ToUpperInvariant()));
		SelectedTimeInForce =
			TimeInForces.FirstOrDefault(x => x.Value == keep) ?? TimeInForces.FirstOrDefault();
	}

	private PlaceOrderRequest BuildRequest()
	{
		var size = OrderHelpers.ParseNumber(SizeText);
		return new PlaceOrderRequest
		{
			Contract = _contract,
			Account = SelectedAccount ?? string.Empty,
			Action = IsBuy ? OrderAction.Buy : OrderAction.Sell,
			OrderType = SelectedOrderType.Value,
			Quantity = IsAmount ? null : size,
			CashAmount = IsAmount ? size : null,
			LimitPrice = IsLimit ? OrderHelpers.ParseNumber(LimitPriceText) : null,
			TimeInForce = SelectedTimeInForce?.Value ?? TimeInForce.Day,
		};
	}

	// Any edit drops a pending review, so what is confirmed is always what is shown
	private void OnTicketChanged()
	{
		StopReview();
		UpdateEstimate();
	}

	private void StopReview()
	{
		IsReviewing = false;
		_reviewed = null;
		ReviewText = null;
	}

	private void UpdateEstimate()
	{
		var request = BuildRequest();
		var price = IsLimit
			? (double?)request.LimitPrice
			: OrderHelpers.GetReferencePrice(
				request.Action,
				_quote?.Bid,
				_quote?.Ask,
				_quote?.Last
			);
		EstimateText = OrderHelpers.Estimate(request.Quantity, request.CashAmount, price);
	}

	private async Task FollowAsync(
		TicketOrderViewModel order,
		IAsyncEnumerable<OrderStatusDto> statuses,
		CancellationToken session
	)
	{
		try
		{
			await foreach (var status in statuses.WithCancellation(session).ConfigureAwait(false))
			{
				Dispatcher.UIThread.Post(() => order.Apply(status));
				if (status.IsDone)
					Log(
						MessageSeverity.Info,
						$"Order {order.OrderId} {status.Status}: {FormatHelpers.Quantity(status.Filled)} filled @ {FormatHelpers.Price(status.AverageFillPrice)}"
					);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			var message = ErrorMessageHelpers.ToUserMessage(ex);
			Dispatcher.UIThread.Post(() => order.Fail(message));
			Log(MessageSeverity.Error, $"Order {order.OrderId} rejected: {message}");
		}
	}

	private void StartQuotes()
	{
		StopQuotes();
		if (_contract is not { } contract || !IsConnected)
			return;
		_quotes = CancellationTokenSource.CreateLinkedTokenSource(_session);
		var token = _quotes.Token;
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
								owner: MarketDataStreamOwners.OrderTicket
							)
							.ConfigureAwait(false)
					)
						_pendingQuote.Set(quote);
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					var message = ErrorMessageHelpers.ToUserMessage(ex);
					Dispatcher.UIThread.Post(() => QuoteText = $"No quote: {message}");
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

	private void FlushQuote()
	{
		if (_pendingQuote.Take() is not { } quote)
			return;
		_quote = quote;
		QuoteText =
			$"Bid {FormatHelpers.Price(quote.Bid)} · Ask {FormatHelpers.Price(quote.Ask)} · Last {FormatHelpers.Price(quote.Last)}";
		UpdateEstimate();
	}

	private void Log(MessageSeverity severity, string text) =>
		_messageLogManager.Add(severity, MessageLogConstants.SourceOrders, text);
}
