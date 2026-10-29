namespace LT.Contracts.Enums;

/// <summary>What a financials lookup found; add new ones at the end.</summary>
public enum FinancialsStatus
{
	/// <summary>Statements were found (<see cref="Dtos.Fundamentals.FinancialsDto"/> has periods).</summary>
	Available = 0,

	/// <summary>Not a US stock: only STK in USD is looked up at the SEC.</summary>
	NotEligible = 1,

	/// <summary>
	/// The SEC has no 10-K / 10-Q statements for the ticker: ETFs and funds, foreign issuers
	/// (20-F / 40-F, IFRS) and tickers it doesn't list.
	/// </summary>
	NotFiled = 2,

	/// <summary>The SEC couldn't be reached or answered with an error.</summary>
	Unavailable = 3,
}
