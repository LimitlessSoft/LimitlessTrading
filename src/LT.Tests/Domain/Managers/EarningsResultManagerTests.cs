using LSCore.Exceptions;
using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Calendar;
using LT.Domain.Managers;
using LT.Tests.Fakes;

namespace LT.Tests.Domain.Managers;

public class EarningsResultManagerTests
{
	private static readonly DateOnly Thursday = new(2026, 10, 29);

	private readonly FakeEarningsResultSource _sec = new("SEC EDGAR");
	private readonly FakeEarningsResultSource _yahoo = new("Yahoo Finance");
	private readonly MessageLogManager _log = new();
	private readonly EarningsResultManager _manager;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public EarningsResultManagerTests()
	{
		LSCoreContainer.EnsureInitialized();
		_manager = new([_sec, _yahoo], _log);
	}

	private Task<IReadOnlyList<EarningsResultDto>> AskAsync(params string[] symbols) =>
		_manager.GetResultsAsync(
			new GetEarningsResultsRequest { Date = Thursday, Symbols = symbols },
			Ct
		);

	private static EarningsResultDto Result(string symbol, decimal? eps, string source) =>
		new()
		{
			Symbol = symbol,
			Date = Thursday,
			EpsActual = eps,
			Source = source,
			FilingUrl = eps is null ? "https://www.sec.gov/x-index.htm" : null,
		};

	[Fact]
	public async Task Keeps_the_first_result_per_symbol_and_only_the_symbols_asked_for()
	{
		_sec.Answer = _ => [Result("AMZN", null, "SEC EDGAR"), Result("INTC", null, "SEC EDGAR")];
		_yahoo.Answer = _ =>
			[Result("AMZN", 1.89m, "Yahoo Finance"), Result("LLY", 7.48m, "Yahoo Finance")];

		var results = await AskAsync("AMZN", "LLY");

		Assert.Equal(["AMZN", "LLY"], results.Select(x => x.Symbol));
		var amazon = results[0];
		Assert.Equal(1.89m, amazon.EpsActual);
		Assert.Equal("Yahoo Finance", amazon.Source);
		Assert.NotNull(amazon.FilingUrl);
	}

	[Fact]
	public async Task A_failing_source_is_logged_once_and_the_others_results_still_come()
	{
		_sec.Failure = new HttpRequestException("503 Service Unavailable");
		_yahoo.Answer = _ => [Result("LLY", 7.48m, "Yahoo Finance")];

		var first = await AskAsync("LLY");
		await AskAsync("LLY");

		Assert.Single(first);
		var message = Assert.Single(_log.GetAll());
		Assert.Equal(MessageSeverity.Warning, message.Severity);
		Assert.Contains("SEC EDGAR", message.Text);
		Assert.Contains("503", message.Text);

		// Answering again clears it: the next failure is logged anew
		_sec.Failure = null;
		await AskAsync("LLY");
		_sec.Failure = new HttpRequestException("503 Service Unavailable");
		await AskAsync("LLY");
		Assert.Equal(2, _log.GetAll().Count);
	}

	[Fact]
	public async Task A_source_timing_out_is_a_failure_not_a_cancellation()
	{
		_sec.Failure = new TaskCanceledException("The request was canceled due to the timeout.");
		_yahoo.Answer = _ => [Result("LLY", 7.48m, "Yahoo Finance")];

		var results = await AskAsync("LLY");

		Assert.Single(results);
		Assert.Contains("timeout", Assert.Single(_log.GetAll()).Text);
	}

	[Fact]
	public void Polls_at_the_shortest_of_the_sources_intervals()
	{
		_sec.Interval = TimeSpan.FromSeconds(30);
		_yahoo.Interval = TimeSpan.FromMinutes(5);

		Assert.Equal(TimeSpan.FromSeconds(30), _manager.GetPollInterval(DateTimeOffset.UtcNow));
	}

	[Fact]
	public async Task A_request_without_a_day_is_refused()
	{
		await Assert.ThrowsAsync<LSCoreBadRequestException>(() =>
			_manager.GetResultsAsync(new GetEarningsResultsRequest { Symbols = ["LLY"] }, Ct)
		);
	}
}
