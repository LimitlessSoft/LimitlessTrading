namespace LT.Contracts.Dtos.Charts;

public class ChartBarsDto
{
	public IReadOnlyList<BarDto> Bars { get; set; } = [];
	public DateTimeOffset FetchedAt { get; set; }

	/// <summary>True when IB could not be reached and these are the last cached bars.</summary>
	public bool IsStale { get; set; }
	public string? Error { get; set; }
}
