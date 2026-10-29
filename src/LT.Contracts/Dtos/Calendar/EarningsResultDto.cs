namespace LT.Contracts.Dtos.Calendar;

/// <summary>
/// A company's earnings result for one day, as one source reported it: the EPS and surprise when
/// the source gives figures, or only that the report is out (SEC EDGAR: the results 8-K was
/// filed). Matched to the calendar's earnings rows by <see cref="Symbol"/> and <see cref="Date"/>.
/// </summary>
public class EarningsResultDto
{
	/// <summary>Upper case, as the calendar lists it.</summary>
	public string Symbol { get; set; } = string.Empty;

	/// <summary>The calendar day (New York) the result belongs to.</summary>
	public DateOnly Date { get; set; }

	/// <summary>The reported EPS; null when the source only knows the report is out.</summary>
	public decimal? EpsActual { get; set; }

	/// <summary>Percent the EPS beat (positive) or missed (negative) the estimate; null when not given.</summary>
	public decimal? EpsSurprisePercent { get; set; }

	/// <summary>When the result came out, when the source says (an 8-K's acceptance time).</summary>
	public DateTimeOffset? ReportedAt { get; set; }

	/// <summary>The source's name for the tooltip ("Yahoo Finance", "SEC EDGAR").</summary>
	public string Source { get; set; } = string.Empty;

	/// <summary>The company's own release on EDGAR (the 8-K's index page); null when none is known.</summary>
	public string? FilingUrl { get; set; }

	/// <summary>The figure came from this source; when <see cref="Source"/> only had the filing, null.</summary>
	public bool HasFigures => EpsActual is not null;
}
