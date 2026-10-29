namespace LT.Contracts.Dtos.Calendar;

/// <summary>One entry of EDGAR's latest filings feed: who filed what, when, with which 8-K items.</summary>
public class SecCurrentFilingDto
{
	public int Cik { get; set; }
	public string Company { get; set; } = string.Empty;
	public string Form { get; set; } = string.Empty;
	public string AccessionNumber { get; set; } = string.Empty;

	/// <summary>EDGAR's acceptance time (the feed's <c>updated</c>).</summary>
	public DateTimeOffset AcceptedAt { get; set; }

	/// <summary>The 8-K items reported, as numbers: "2.02", "9.01".</summary>
	public IReadOnlyList<string> Items { get; set; } = [];

	/// <summary>The filing's index page.</summary>
	public string Url { get; set; } = string.Empty;
}
