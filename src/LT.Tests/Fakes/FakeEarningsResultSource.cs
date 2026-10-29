using LT.Contracts.Dtos.Calendar;
using LT.Contracts.Interfaces.IEarningsResultSources;

namespace LT.Tests.Fakes;

/// <summary>Answers every query from <see cref="Answer"/> (or throws <see cref="Failure"/>); counts calls.</summary>
public class FakeEarningsResultSource(string name) : IEarningsResultSource
{
	public string Name { get; } = name;

	public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

	public Func<EarningsResultQueryDto, IReadOnlyList<EarningsResultDto>> Answer { get; set; } =
		_ => [];

	public Exception? Failure { get; set; }

	public int Calls { get; private set; }

	public TimeSpan GetRefreshInterval(DateTimeOffset now) => Interval;

	public Task<IReadOnlyList<EarningsResultDto>> GetResultsAsync(
		EarningsResultQueryDto query,
		CancellationToken cancellationToken = default
	)
	{
		Calls++;
		return Failure != null
			? Task.FromException<IReadOnlyList<EarningsResultDto>>(Failure)
			: Task.FromResult(Answer(query));
	}
}
