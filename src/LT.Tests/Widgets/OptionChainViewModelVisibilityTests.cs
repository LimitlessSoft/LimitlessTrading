using Avalonia.Threading;
using LT.App.Helpers;
using LT.App.Stores;
using LT.App.Widgets.OptionChain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Enums;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Widgets;

/// <summary>
/// A connected option chain streams the underlying and every row on screen only while the widget
/// is shown: hidden (inactive view, collapsed panel, minimized window) it gives every line back
/// and keeps its rows and last values; shown again, the same rows subscribe again without loading
/// the chain again. The fake client keeps streams open, so lines stay counted until cancelled.
/// </summary>
public class OptionChainViewModelVisibilityTests : IDisposable
{
	private const string Expiry = "20261120";
	private static readonly double[] Strikes = [96, 98, 100, 102, 104];

	// The underlying plus a call and a put per strike
	private static readonly int ChainLines = 1 + 2 * Strikes.Length;

	private static readonly ContractDto Spy = new()
	{
		ConId = 756733,
		Symbol = "SPY",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly TestCache _cache = new();
	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		KeepMarketDataOpen = true,
		Ticks = [new MarketDataTickDto { Field = TickTypeHelpers.Last, Price = 100 }],
		OptionChainParameters =
		[
			new OptionChainParametersDto
			{
				Exchange = MarketDataConstants.DefaultExchange,
				TradingClass = "SPY",
				Expirations = [Expiry],
				Strikes = Strikes,
			},
		],
		SearchResults =
		[
			.. Strikes.SelectMany(
				(strike, i) =>
					new[]
					{
						Option(1000 + i, strike, MarketDataConstants.CallRight),
						Option(2000 + i, strike, MarketDataConstants.PutRight),
					}
			),
		],
	};
	private readonly FakeConnectionManager _connection = new();
	private readonly LinkGroupStore _links = new();
	private readonly MarketDataManager _marketData;
	private OptionChainViewModel? _chain;

	public OptionChainViewModelVisibilityTests()
	{
		_marketData = new MarketDataManager(_ibkr, TimeProvider.System);
		_links.Set(LinkGroup.Red, Spy);
	}

	[Fact]
	public void Hiding_the_chain_releases_every_line_and_keeps_its_rows_and_values()
	{
		var chain = CreateLoaded();
		var rows = chain.Rows.ToList();
		WaitFor(() =>
		{
			foreach (var row in chain.Rows)
				row.Flush();
			return rows[0].CallLast != FormatHelpers.Empty;
		});
		var last = rows[0].CallLast;

		Ui(() => chain.SetShown(false));

		WaitFor(() => _marketData.ActiveLines == 0 && _ibkr.OpenMarketDataStreams == 0);
		Assert.Equal(rows, chain.Rows);
		Assert.Equal(last, rows[0].CallLast);
		Assert.False(chain.IsShown);
	}

	[Fact]
	public void Showing_it_again_subscribes_the_same_rows_without_loading_the_chain_again()
	{
		var chain = CreateLoaded();
		var rows = chain.Rows.ToList();
		var searches = _ibkr.SearchCalls;
		var requested = Requested();
		Ui(() => chain.SetShown(false));
		WaitFor(() => _marketData.ActiveLines == 0);

		Ui(() => chain.SetShown(true));

		WaitFor(() => _marketData.ActiveLines == ChainLines);
		Assert.Equal(rows, chain.Rows);
		Assert.Equal(searches, _ibkr.SearchCalls);
		// The same contracts once more: the underlying and each row's call and put
		var again = Requested().Skip(requested.Count).ToList();
		Assert.Equal(requested.TakeLast(ChainLines).Order(), again.Order());
	}

	[Fact]
	public void A_chain_loaded_while_hidden_streams_nothing_until_shown()
	{
		var chain = Create();
		Ui(() => chain.SetShown(false));
		_connection.SetStatus(ConnectionState.Connected, "U1");

		WaitFor(() => chain.Rows.Count == Strikes.Length && !chain.IsLoading);
		// Only the one-off price of the underlying for centring the strikes, then nothing
		WaitFor(() => _marketData.ActiveLines == 0);
		var requested = Requested().Count;

		Ui(() => chain.SetShown(true));

		WaitFor(() => _marketData.ActiveLines == ChainLines);
		Assert.Equal(requested + ChainLines, Requested().Count);
	}

	[Fact]
	public void Hiding_and_showing_while_disconnected_opens_nothing()
	{
		var chain = Create();

		Ui(() => chain.SetShown(false));
		Ui(() => chain.SetShown(true));

		Assert.Empty(Requested());
		Assert.Equal(0, _marketData.ActiveLines);
	}

	public void Dispose()
	{
		if (_chain != null)
			TestUiThread.Invoke(_chain.Dispose);
		GC.SuppressFinalize(this);
	}

	// Connected, with the chain of the one expiration on screen and streaming
	private OptionChainViewModel CreateLoaded()
	{
		var chain = Create();
		_connection.SetStatus(ConnectionState.Connected, "U1");
		WaitFor(() =>
			chain.Rows.Count == Strikes.Length
			&& _marketData.ActiveLines == ChainLines
			&& _ibkr.OpenMarketDataStreams == ChainLines
		);
		return chain;
	}

	private OptionChainViewModel Create()
	{
		_chain = TestUiThread.Invoke(() =>
		{
			var settings = new SettingsManager(_cache.Settings, _ibkr);
			return new OptionChainViewModel(
				new OptionChainManager(_ibkr, _marketData, _cache.Manager, _cache.Time),
				new ContractManager(_ibkr, _cache.Settings, _cache.Manager),
				_marketData,
				_connection,
				new MessageLogManager(),
				new OrderManager(_ibkr, _cache.Manager),
				_links,
				new AccountStore(_connection, settings),
				_cache.Time,
				new DisplayTimeStore(settings, _cache.Time),
				new OrderTicketNavigator()
			);
		});
		// The base's posted connection check
		Ui(() => { });
		return _chain;
	}

	private List<int> Requested()
	{
		lock (_ibkr.MarketDataRequested)
			return [.. _ibkr.MarketDataRequested.Select(x => x.ConId)];
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

	private static ContractDetailsDto Option(int conId, double strike, string right) =>
		new()
		{
			Contract = new ContractDto
			{
				ConId = conId,
				Symbol = "SPY",
				SecurityType = MarketDataConstants.OptionType,
				Exchange = MarketDataConstants.DefaultExchange,
				Currency = "USD",
				LastTradeDateOrContractMonth = Expiry,
				Strike = strike,
				Right = right,
				TradingClass = "SPY",
			},
		};
}
