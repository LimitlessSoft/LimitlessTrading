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
	/// The newest count from a US filer's report (<see cref="SecConstants.SharesOutstandingForms"/>)
	/// no older than <see cref="SecConstants.MaxFactAge"/>; the latest filed wins on the same date.
	/// </summary>
	public static SharesOutstandingDto? PickSharesOutstanding(
		IEnumerable<SecFactDto> facts,
		DateOnly today
	)
	{
		var oldest = today.AddDays(-(int)SecConstants.MaxFactAge.TotalDays);
		var latest = facts
			.Where(x =>
				x.Value >= 1
				&& x.End >= oldest
				&& SecConstants.SharesOutstandingForms.Contains(x.Form)
			)
			.OrderByDescending(x => x.End)
			.ThenByDescending(x => x.Filed)
			.FirstOrDefault();
		return latest == null
			? null
			: new SharesOutstandingDto { Shares = (long)latest.Value, AsOf = latest.End };
	}

	/// <summary>A found count is fresh for a week, "nothing usable" for a few days.</summary>
	public static bool IsFresh(SharesOutstandingEntity entry, DateTimeOffset now) =>
		now - entry.FetchedAt
		< (
			entry.Shares != null
				? SecConstants.SharesOutstandingTtl
				: SecConstants.SharesNotFoundTtl
		);

	/// <summary>Shares × the last trade, or × the previous close before the first trade.</summary>
	public static double? Compute(long? shares, QuoteDto? quote) =>
		shares is { } s
		&& quote != null
		&& TickTypeHelpers.GetDisplayPrice(quote) is double price and > 0
			? s * price
			: null;
}
