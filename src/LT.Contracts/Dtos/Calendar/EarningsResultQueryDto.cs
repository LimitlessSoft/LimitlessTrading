namespace LT.Contracts.Dtos.Calendar;

/// <summary>What an earnings result source is asked: the symbols of one day still without a result.</summary>
public class EarningsResultQueryDto
{
	public DateOnly Date { get; set; }

	/// <summary>Upper case; a source may answer for others too, they are ignored.</summary>
	public IReadOnlyList<string> Symbols { get; set; } = [];

	/// <summary>Ask again now instead of answering from what the source has kept (Refresh).</summary>
	public bool Refresh { get; set; }
}
