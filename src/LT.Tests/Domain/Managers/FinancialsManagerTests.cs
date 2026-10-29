using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Enums;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// Statements from SEC companyfacts: US stocks only, kept in memory per ticker, a clear status
/// for ETFs and foreign filers, and a failure logged once without throwing.
/// </summary>
public class FinancialsManagerTests
{
	private const int AppleCik = 320193;
	private const int SpyCik = 884394;
	private const int TsmCik = 1046179;

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 29, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeSecApiClient _sec = new();
	private readonly MessageLogManager _log = new();
	private readonly FinancialsManager _manager;

	public FinancialsManagerTests()
	{
		_manager = new FinancialsManager(_sec, _log, _time);
		_sec.Ciks["AAPL"] = AppleCik;
		_sec.Ciks["SPY"] = SpyCik;
		_sec.Ciks["TSM"] = TsmCik;
		_sec.FinancialFacts[AppleCik] = new SecCompanyFactsDto
		{
			EntityName = "Apple Inc.",
			Facts =
			[
				Fact("NetIncomeLoss", "2024-09-29", "2025-09-27", 112_010, "10-K"),
				Fact("NetIncomeLoss", "2026-03-29", "2026-06-27", 29_789, "10-Q"),
			],
		};
		// Facts, but only from 20-F annual reports
		_sec.FinancialFacts[TsmCik] = new SecCompanyFactsDto
		{
			EntityName = "TSMC",
			Facts = [Fact("NetIncomeLoss", "2025-01-01", "2025-12-31", 1, "20-F")],
		};
	}

	[Fact]
	public async Task Reads_a_us_stock_once_then_serves_it_from_memory()
	{
		var first = await GetAsync("AAPL");
		_time.Advance(FinancialsConstants.FoundTtl - TimeSpan.FromMinutes(1));
		var second = await GetAsync("AAPL");

		Assert.Equal(FinancialsStatus.Available, first.Status);
		Assert.Equal("Apple Inc.", first.EntityName);
		Assert.Equal(2025, Assert.Single(first.Annual).FiscalYear);
		Assert.Equal(29_789m, Assert.Single(first.Quarterly).Get(FinancialItem.NetIncome));
		Assert.Same(first, second);
		Assert.Equal(1, _sec.FinancialCalls);
		Assert.Equal(1, _sec.TickerCalls);
	}

	[Fact]
	public async Task Reads_again_once_the_statements_are_old()
	{
		await GetAsync("AAPL");
		_time.Advance(FinancialsConstants.FoundTtl);
		await GetAsync("AAPL");

		Assert.Equal(2, _sec.FinancialCalls);
		Assert.Equal(1, _sec.TickerCalls);
	}

	[Fact]
	public async Task A_stock_outside_the_us_is_not_looked_up()
	{
		var financials = await GetAsync("SAP", "EUR");

		Assert.Equal(FinancialsStatus.NotEligible, financials.Status);
		Assert.Equal(0, _sec.TickerCalls);
	}

	[Theory]
	[InlineData("SPY")] // an ETF: the SEC has no companyfacts (404)
	[InlineData("TSM")] // a foreign issuer: 20-F only
	[InlineData("ZZZZ")] // not in the SEC's ticker file
	public async Task Without_10k_or_10q_statements_it_says_so(string symbol)
	{
		var financials = await GetAsync(symbol);

		Assert.Equal(FinancialsStatus.NotFiled, financials.Status);
		Assert.Empty(financials.Annual);
		Assert.Empty(_log.GetAll());
	}

	[Fact]
	public async Task A_failure_is_logged_once_and_not_retried_during_the_backoff()
	{
		_sec.Failure = new HttpRequestException("503");

		var first = await GetAsync("AAPL");
		var second = await GetAsync("AAPL");

		Assert.Equal(FinancialsStatus.Unavailable, first.Status);
		Assert.Equal(FinancialsStatus.Unavailable, second.Status);
		Assert.Single(_log.GetAll(), x => x.Source == MessageLogConstants.SourceSec);

		_sec.Failure = null;
		_time.Advance(SecConstants.FailureBackoff);
		Assert.Equal(FinancialsStatus.Available, (await GetAsync("AAPL")).Status);
	}

	[Fact]
	public async Task Concurrent_lookups_of_one_ticker_share_one_request()
	{
		_sec.Gate = new TaskCompletionSource();

		var first = GetAsync("AAPL");
		var second = GetAsync("AAPL");
		_sec.Gate.SetResult();

		Assert.Same(await first, await second);
		Assert.Equal(1, _sec.FinancialCalls);
	}

	private Task<FinancialsDto> GetAsync(string symbol, string currency = "USD") =>
		_manager.GetFinancialsAsync(
			new ContractDto
			{
				Symbol = symbol,
				SecurityType = MarketDataConstants.StockType,
				Currency = currency,
			},
			TestContext.Current.CancellationToken
		);

	private static SecFactDto Fact(
		string concept,
		string start,
		string end,
		decimal value,
		string form
	) =>
		new()
		{
			Concept = concept,
			Start = DateOnly.Parse(start),
			End = DateOnly.Parse(end),
			Filed = DateOnly.Parse(end).AddDays(35),
			Value = value,
			Form = form,
		};
}
