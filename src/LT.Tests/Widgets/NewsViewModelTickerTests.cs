using Avalonia.Threading;
using LT.App.Stores;
using LT.App.Widgets.News;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// Typing a ticker into the News widget's header title: it becomes the linked contract (and moves
/// the link group along), switching All news back to Linked; cancel and blank input change nothing.
/// Connected with one provider, so the switch shows up as a headline request for the new contract.
/// </summary>
public class NewsViewModelTickerTests : IDisposable
{
	private static readonly ContractDto Aapl = Stock(265598, "AAPL");
	private static readonly ContractDto Msft = Stock(272093, "MSFT");

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new() { IsConnected = true };
	private readonly FakeConnectionManager _connection = new();
	private readonly FakeNewsSource _source = new("ibkr");
	private readonly LinkGroupStore _links = new();
	private readonly ContractManager _contracts;
	private readonly NewsViewModel _news;

	public NewsViewModelTickerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_source.Providers = [_source.Provider("BRFG")];
		_contracts = new ContractManager(_ibkr, _cache.Settings, _cache.Manager);
		_links.Set(LinkGroup.Red, Aapl);
		_news = TestUiThread.Invoke(CreateNews);
	}

	[Fact]
	public async Task A_typed_ticker_is_upper_cased_resolved_and_its_news_is_asked_for()
	{
		await Connect();
		_ibkr.SearchResults = [new ContractDetailsDto { Contract = Msft }];
		_news.TickerEditor.Begin();

		await OnUi(() => _news.TickerEditor.CommitAsync(" msft"));

		Assert.Equal("MSFT", _ibkr.LastSearch!.Symbol);
		Assert.Equal("News · MSFT", _news.Title);
		Assert.False(_news.TickerEditor.IsEditing);
		Assert.Equal(Msft.ConId, _links.Get(LinkGroup.Red)?.ConId);
		Assert.Equal(Msft.ConId, _contracts.GetRecentContracts()[0].ConId);
		Assert.Equal(Msft.ConId, _source.HeadlineRequests[^1].Contract.ConId);
	}

	[Fact]
	public async Task A_ticker_typed_in_All_news_switches_back_to_Linked()
	{
		await Connect();
		TestUiThread.Invoke(() => _news.Mode = NewsMode.All);

		TestUiThread.Invoke(() => _news.TickerEditor.Pick(Msft));

		Assert.Equal(NewsMode.Linked, _news.Mode);
		Assert.Equal("News · MSFT", _news.Title);
		Assert.Equal(Msft.ConId, _source.HeadlineRequests[^1].Contract.ConId);
	}

	[Fact]
	public async Task Blank_input_keeps_the_contract_and_asks_nothing()
	{
		await Connect();
		var requests = _source.HeadlineRequests.Count;
		_news.TickerEditor.Begin();

		await OnUi(() => _news.TickerEditor.CommitAsync("  "));

		Assert.Equal(0, _ibkr.SearchCalls);
		Assert.Equal("News · AAPL", _news.Title);
		Assert.False(_news.TickerEditor.IsEditing);
		Assert.Equal(requests, _source.HeadlineRequests.Count);
	}

	[Fact]
	public async Task Cancel_keeps_the_contract_and_asks_nothing()
	{
		await Connect();
		var requests = _source.HeadlineRequests.Count;
		_news.TickerEditor.Begin();

		_news.TickerEditor.Cancel();

		Assert.Equal(0, _ibkr.SearchCalls);
		Assert.Equal("News · AAPL", _news.Title);
		Assert.False(_news.TickerEditor.IsEditing);
		Assert.Equal(requests, _source.HeadlineRequests.Count);
	}

	[Fact]
	public async Task An_unknown_ticker_shows_an_error_and_keeps_the_contract()
	{
		_ibkr.SearchResults = [];
		_news.TickerEditor.Begin();

		await OnUi(() => _news.TickerEditor.CommitAsync("zzzz"));

		Assert.Equal("News · AAPL", _news.Title);
		Assert.False(string.IsNullOrEmpty(_news.ErrorMessage));
		Assert.Equal(Aapl.ConId, _links.Get(LinkGroup.Red)?.ConId);
	}

	public void Dispose()
	{
		TestUiThread.Invoke(_news.Dispose);
		GC.SuppressFinalize(this);
	}

	// Connecting loads the providers and starts the linked feed for the group's contract
	private async Task Connect()
	{
		_connection.SetStatus(ConnectionState.Connected, "U1");
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());
		await WaitFor(() => _source.HeadlineRequests.Count > 0);
		Assert.Equal(Aapl.ConId, _source.HeadlineRequests[^1].Contract.ConId);
	}

	// Started where the view model lives; the test awaits the returned task
	private static Task OnUi(Func<Task> action) => TestUiThread.Invoke(action);

	private static async Task WaitFor(Func<bool> condition)
	{
		for (var i = 0; i < 200 && !condition(); i++)
			await Task.Delay(10, TestContext.Current.CancellationToken);
		Assert.True(condition());
	}

	private NewsViewModel CreateNews()
	{
		var settings = new SettingsManager(_cache.Settings, _ibkr);
		return new NewsViewModel(
			new NewsManager([_source], _cache.Time),
			_contracts,
			_connection,
			_links,
			new DisplayTimeStore(settings, _cache.Time)
		);
	}

	private static ContractDto Stock(int conId, string symbol) =>
		new()
		{
			ConId = conId,
			Symbol = symbol,
			SecurityType = "STK",
			Exchange = "SMART",
			Currency = "USD",
		};
}
