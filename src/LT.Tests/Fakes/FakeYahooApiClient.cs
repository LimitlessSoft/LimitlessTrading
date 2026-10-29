using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Interfaces.IApiClients;

namespace LT.Tests.Fakes;

/// <summary>Answers each Yahoo page from <see cref="Answer"/>; records what was asked.</summary>
public class FakeYahooApiClient : IYahooApiClient
{
	/// <summary>The page (or a thrown exception) for a day and offset; an empty page by default.</summary>
	public Func<DateOnly, int, YahooEarningsPageDto> Answer { get; set; } = (_, _) => new();

	public List<(DateOnly Day, int Offset)> Requests { get; } = [];

	public Task<YahooEarningsPageDto> GetEarningsPageAsync(
		DateOnly day,
		int offset,
		CancellationToken cancellationToken = default
	)
	{
		lock (Requests)
			Requests.Add((day, offset));
		try
		{
			return Task.FromResult(Answer(day, offset));
		}
		catch (Exception ex)
		{
			return Task.FromException<YahooEarningsPageDto>(ex);
		}
	}

	/// <summary>A page of one day: rows with an EPS ("LLY", 7.48m) or without (null).</summary>
	public static YahooEarningsPageDto Page(
		DateOnly day,
		int first,
		int total,
		params (string Symbol, decimal? Eps, decimal? Surprise)[] rows
	) =>
		new()
		{
			First = first,
			Total = total,
			Rows =
			[
				.. rows.Select(x => new EarningsResultDto
				{
					Symbol = x.Symbol,
					Date = day,
					EpsActual = x.Eps,
					EpsSurprisePercent = x.Surprise,
					Source = "Yahoo Finance",
				}),
			],
		};
}
