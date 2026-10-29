namespace LT.Contracts.Dtos.Calendar;

/// <summary>
/// One page of Yahoo Finance's earnings calendar: its rows (with figures or without) and where
/// the page sits in the day's list ("1-25 of 298": <see cref="First"/> 1, <see cref="Total"/> 298).
/// </summary>
public class YahooEarningsPageDto
{
	public IReadOnlyList<EarningsResultDto> Rows { get; set; } = [];

	/// <summary>1-based position of the page's first row; 0 when the page didn't say.</summary>
	public int First { get; set; }

	/// <summary>Rows the day has in all; 0 when the page didn't say.</summary>
	public int Total { get; set; }
}
