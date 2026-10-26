namespace LT.Contracts.Dtos.AccountPerformance;

/// <summary>Largest peak-to-trough fall; both values are zero or negative.</summary>
public class DrawdownDto
{
	/// <summary>Of the summed daily P&amp;L curve, in the account currency.</summary>
	public double? Money { get; set; }

	/// <summary>Of the chained daily returns, in percent (-3.2 = -3.2 %).</summary>
	public double? Percent { get; set; }
}
