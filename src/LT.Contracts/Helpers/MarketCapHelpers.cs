using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Fundamentals;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Entities;

namespace LT.Contracts.Helpers;

/// <summary>Market cap = SEC shares outstanding × IB's last price.</summary>
public static class MarketCapHelpers
{
	/// <summary>Only US stocks (STK in USD) are looked up at the SEC.</summary>
	public static bool IsEligible(ContractDto contract) =>
		contract.SecurityType == MarketDataConstants.StockType
		&& contract.Currency == MarketDataConstants.PreferredCurrency
		&& !string.IsNullOrWhiteSpace(contract.Symbol);

	/// <summary>The SEC's spelling of an IB symbol: share classes take a dash (BRK B → BRK-B).</summary>
	public static string GetSecTicker(string symbol) =>
		symbol.Trim().ToUpperInvariant().Replace(' ', '-').Replace('.', '-');

	/// <summary>
	/// The newest count as of a date (no weighted averages) from a US filer's report
	/// (<see cref="SecConstants.SharesOutstandingForms"/>) no older than
	/// <see cref="SecConstants.MaxFactAge"/>; the latest filed wins on the same date.
	/// </summary>
	public static SharesOutstandingDto? PickSharesOutstanding(
		IEnumerable<SecFactDto> facts,
		DateOnly today
	) =>
		ToDto(
			Usable(facts, today)
				.Where(x => x.Start == null)
				.OrderByDescending(x => x.End)
				.ThenByDescending(x => x.Filed)
				.FirstOrDefault()
		);

	/// <summary>
	/// The fallback when no count as of a date is usable (multi-class filers, META): the weighted
	/// average of the newest period, usable like <see cref="PickSharesOutstanding"/>. Basic before
	/// diluted, the quarter before the year to date, the latest filed last.
	/// </summary>
	public static SharesOutstandingDto? PickWeightedAverageShares(
		IEnumerable<SecFactDto> facts,
		DateOnly today
	) =>
		ToDto(
			Usable(facts, today)
				.Where(x =>
					x.Start != null
					&& x.Concept
						is SecConstants.WeightedAverageBasicConcept
							or SecConstants.WeightedAverageDilutedConcept
				)
				.OrderByDescending(x => x.End)
				.ThenBy(x => x.Concept == SecConstants.WeightedAverageBasicConcept ? 0 : 1)
				.ThenByDescending(x => x.Start)
				.ThenByDescending(x => x.Filed)
				.FirstOrDefault()
		);

	/// <summary>
	/// The total from a filing's cover page (<see cref="SecFactDto.ShareClass"/> counts, usable
	/// like <see cref="PickSharesOutstanding"/>), in shares of <paramref name="secTicker"/>: the
	/// newest date's undivided count if there is one, otherwise its classes added up, each times
	/// its <see cref="SecConstants.ShareClassRatios"/> (BRK-B: Class A × 1,500 + Class B).
	/// </summary>
	public static SharesOutstandingDto? PickFilingShares(
		IEnumerable<SecFactDto> facts,
		string secTicker,
		DateOnly today
	)
	{
		var usable = Usable(facts, today).Where(x => x.Start == null).ToList();
		if (usable.Count == 0)
			return null;
		var end = usable.Max(x => x.End);
		var filed = usable.Where(x => x.End == end).Max(x => x.Filed);
		var latest = usable.Where(x => x.End == end && x.Filed == filed).ToList();
		if (latest.FirstOrDefault(x => x.ShareClass == null) is { } total)
			return ToDto(total);

		var ratios = SecConstants.ShareClassRatios.GetValueOrDefault(secTicker);
		var shares = latest
			.GroupBy(x => x.ShareClass!)
			.Sum(x =>
				x.Max(f => f.Value)
				* (ratios != null && ratios.TryGetValue(x.Key, out var ratio) ? ratio : 1m)
			);
		return new SharesOutstandingDto { Shares = (long)Math.Round(shares), AsOf = end };
	}

	/// <summary>
	/// A found count is fresh for a week, "nothing usable" for a few days unless an older lookup
	/// (<see cref="SecConstants.SharesLookupVersion"/>) found it.
	/// </summary>
	public static bool IsFresh(SharesOutstandingEntity entry, DateTimeOffset now) =>
		entry.Shares != null
			? now - entry.FetchedAt < SecConstants.SharesOutstandingTtl
			: entry.LookupVersion >= SecConstants.SharesLookupVersion
				&& now - entry.FetchedAt < SecConstants.SharesNotFoundTtl;

	/// <summary>Shares × the last trade, or × the previous close before the first trade.</summary>
	public static double? Compute(long? shares, QuoteDto? quote) =>
		shares is { } s
		&& quote != null
		&& TickTypeHelpers.GetDisplayPrice(quote) is double price and > 0
			? s * price
			: null;

	// Positive values from a US filer's report no older than MaxFactAge
	private static IEnumerable<SecFactDto> Usable(IEnumerable<SecFactDto> facts, DateOnly today)
	{
		var oldest = today.AddDays(-(int)SecConstants.MaxFactAge.TotalDays);
		return facts.Where(x =>
			x.Value >= 1 && x.End >= oldest && SecConstants.SharesOutstandingForms.Contains(x.Form)
		);
	}

	private static SharesOutstandingDto? ToDto(SecFactDto? fact) =>
		fact == null
			? null
			: new SharesOutstandingDto { Shares = (long)fact.Value, AsOf = fact.End };
}
