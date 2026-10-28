using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.Depth;
using LT.App.Widgets.OrderTicket;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// The order ticket's quote line and the Depth widget's depth subscription run only while the
/// widget is shown: hidden (a collapsed side panel, an inactive view, a minimized window) they are
/// given back, shown again they come back. A view model the workspace never told anything (the
/// order ticket in its Close-position dialog) counts as shown.
/// </summary>
public class HiddenWidgetStreamsTests : IDisposable
{
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		KeepMarketDataOpen = true,
		KeepDepthOpen = true,
		Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 10 }],
	};
	private readonly FakeConnectionManager _connection = new();
	private readonly LinkGroupStore _links = new();
	private readonly MarketDataManager _marketData;
	private readonly MarketDepthManager _depth;
	private IDisposable? _widget;

	public HiddenWidgetStreamsTests()
	{
		_marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		_depth = new MarketDepthManager(_ibkr);
		_links.Set(LinkGroup.Red, Aapl);
	}

	[Fact]
	public void The_order_ticket_gives_its_quote_line_back_while_hidden()
	{
		var ticket = CreateTicket();
		_connection.SetStatus(ConnectionState.Connected, "U1");
		WaitFor(() => _marketData.ActiveLines == 1);

		Ui(() => ticket.SetShown(false));
		WaitFor(() => _marketData.ActiveLines == 0 && _ibkr.OpenMarketDataStreams == 0);

		Ui(() => ticket.SetShown(true));
		WaitFor(() => _marketData.ActiveLines == 1);
		Assert.Equal(2, _ibkr.MarketDataRequested.Count);
	}

	[Fact]
	public void A_hidden_order_ticket_streams_nothing_when_connected_or_given_a_contract()
	{
		var ticket = CreateTicket();
		Ui(() => ticket.SetShown(false));
		_connection.SetStatus(ConnectionState.Connected, "U1");
		Ui(() => _links.Set(LinkGroup.Red, new ContractDto { ConId = 272093, Symbol = "MSFT" }));
		Thread.Sleep(50);
		WaitFor(() => true);

		Assert.Empty(_ibkr.MarketDataRequested);

		Ui(() => ticket.SetShown(true));
		WaitFor(() => _marketData.ActiveLines == 1);
		Assert.Equal(272093, Assert.Single(_ibkr.MarketDataRequested).ConId);
	}

	[Fact]
	public void The_depth_widget_gives_its_subscription_back_while_hidden()
	{
		var depth = CreateDepth();
		_connection.SetStatus(ConnectionState.Connected, "U1");
		WaitFor(() => _depth.ActiveSubscriptions == 1);

		Ui(() => depth.SetShown(false));
		WaitFor(() => _depth.ActiveSubscriptions == 0);

		Ui(() => depth.SetShown(true));
		WaitFor(() => _depth.ActiveSubscriptions == 1);
		Assert.Equal(2, _ibkr.DepthRequests.Count);
	}

	[Fact]
	public void A_depth_widget_hidden_before_connecting_subscribes_only_once_shown()
	{
		var depth = CreateDepth();
		Ui(() => depth.SetShown(false));
		_connection.SetStatus(ConnectionState.Connected, "U1");
		Thread.Sleep(50);
		WaitFor(() => true);

		Assert.Empty(_ibkr.DepthRequests);

		Ui(() => depth.SetShown(true));
		WaitFor(() => _depth.ActiveSubscriptions == 1);
	}

	[Fact]
	public void A_closed_depth_widget_stays_unsubscribed_when_shown()
	{
		var depth = CreateDepth();
		_connection.SetStatus(ConnectionState.Connected, "U1");
		WaitFor(() => _depth.ActiveSubscriptions == 1);

		Ui(() =>
		{
			depth.SetOpen(false);
			depth.SetShown(false);
			depth.SetShown(true);
		});

		WaitFor(() => _depth.ActiveSubscriptions == 0);
		Assert.Single(_ibkr.DepthRequests);
	}

	public void Dispose()
	{
		if (_widget != null)
			TestUiThread.Invoke(_widget.Dispose);
		GC.SuppressFinalize(this);
	}

	private OrderTicketViewModel CreateTicket()
	{
		var ticket = TestUiThread.Invoke(() =>
		{
			var settings = new SettingsManager(_cache.Settings, _ibkr);
			return new OrderTicketViewModel(
				new OrderManager(_ibkr, _cache.Manager),
				_marketData,
				_connection,
				new MessageLogManager(),
				new AccountStore(_connection, settings),
				_links
			);
		});
		_widget = ticket;
		Ui(() => { });
		return ticket;
	}

	private DepthViewModel CreateDepth()
	{
		var depth = TestUiThread.Invoke(() => new DepthViewModel(_depth, _connection, _links));
		_widget = depth;
		Ui(() => { });
		return depth;
	}

	private static void Ui(Action action) =>
		TestUiThread.Invoke(() =>
		{
			action();
			Dispatcher.UIThread.RunJobs();
		});

	// Runs posted UI work until the condition holds (checked on the UI thread)
	private static void WaitFor(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (
			!TestUiThread.Invoke(() =>
			{
				Dispatcher.UIThread.RunJobs();
				return condition();
			})
		)
		{
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException("condition not met");
			Thread.Sleep(10);
		}
	}
}
