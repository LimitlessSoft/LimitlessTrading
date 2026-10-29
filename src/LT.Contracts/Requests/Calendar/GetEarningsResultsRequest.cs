namespace LT.Contracts.Requests.Calendar;

/// <summary>
/// The earnings results of one day's symbols still without one. <see cref="Refresh"/> asks every
/// source again instead of answering from what each kept.
/// </summary>
public class GetEarningsResultsRequest
{
	public DateOnly Date { get; set; }
	public IReadOnlyList<string> Symbols { get; set; } = [];
	public bool Refresh { get; set; }
}
