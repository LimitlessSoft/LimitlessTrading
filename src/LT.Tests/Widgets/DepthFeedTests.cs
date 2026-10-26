using LT.App.Widgets.Depth;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Requests.MarketDepth;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

public class DepthFeedTests
{
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
	};

	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true, KeepDepthOpen = true };
	private readonly MarketDepthManager _manager;

	public DepthFeedTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new MarketDepthManager(_ibkr);
	}

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task WaitForSubscriptionsAsync(int count)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(5));
		while (_manager.ActiveSubscriptions != count)
			await Task.Delay(10, timeout.Token);
	}

	[Fact]
	public async Task Closing_the_widget_frees_its_depth_slot_and_reopening_takes_it_again()
	{
		using var feed = new DepthFeed(_manager, (_, _) => { });
		feed.Subscribe(new SubscribeOrderBookRequest { Contract = Aapl }, Ct);
		await WaitForSubscriptionsAsync(1);

		feed.SetOpen(false);
		await WaitForSubscriptionsAsync(0);
		Assert.False(feed.IsSubscribed);

		// A contract picked while closed waits for the widget to open
		feed.Subscribe(
			new SubscribeOrderBookRequest
			{
				Contract = new ContractDto { ConId = 272093, Symbol = "MSFT" },
			},
			Ct
		);
		await Task.Delay(50, Ct);
		Assert.Equal(0, _manager.ActiveSubscriptions);
		Assert.Single(_ibkr.DepthRequests);

		feed.SetOpen(true);
		await WaitForSubscriptionsAsync(1);
		Assert.Equal(272093, _ibkr.DepthRequests[^1].Contract.ConId);
	}

	[Fact]
	public async Task A_new_request_replaces_the_subscription_and_a_dead_session_starts_none()
	{
		using var feed = new DepthFeed(_manager, (_, _) => { });
		feed.Subscribe(new SubscribeOrderBookRequest { Contract = Aapl }, Ct);
		await WaitForSubscriptionsAsync(1);

		feed.Subscribe(
			new SubscribeOrderBookRequest { Contract = Aapl, PreferSmartDepth = false },
			Ct
		);
		while (_ibkr.DepthRequests.Count < 2)
			await Task.Delay(10, Ct);
		await WaitForSubscriptionsAsync(1);

		feed.Subscribe(
			new SubscribeOrderBookRequest { Contract = Aapl },
			new CancellationToken(true)
		);
		await WaitForSubscriptionsAsync(0);
		Assert.False(feed.IsSubscribed);
	}
}
