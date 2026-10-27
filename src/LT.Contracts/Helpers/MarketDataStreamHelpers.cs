using LT.Contracts.Constants;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>Display text of the market data lines listed from the status bar.</summary>
public static class MarketDataStreamHelpers
{
	/// <summary>
	/// "Quote", "Quote + option IV" (generic tick 106), "Quote + ticks 233,…" or "News".
	/// </summary>
	public static string GetKindText(MarketDataStreamDto stream)
	{
		if (stream.Kind == MarketDataStreamKind.News)
			return "News";
		if (string.IsNullOrWhiteSpace(stream.GenericTicks))
			return "Quote";
		return stream.GenericTicks.Trim() == MarketDataConstants.OptionImpliedVolatilityGenericTick
			? "Quote + option IV"
			: $"Quote + ticks {stream.GenericTicks.Trim()}";
	}

	/// <summary>The contract as the rest of the app shows it, with its security type.</summary>
	public static string GetContractText(MarketDataStreamDto stream)
	{
		var name = ContractHelpers.GetDisplayName(stream.Contract);
		if (string.IsNullOrEmpty(name))
			name = stream.Contract.ConId > 0 ? $"#{stream.Contract.ConId}" : "?";
		return string.IsNullOrEmpty(stream.Contract.SecurityType)
			? name
			: $"{name} · {stream.Contract.SecurityType}";
	}

	/// <summary>
	/// Lines per owner, most first then by name: "Watchlist · US 40, Chart 2". Streams without
	/// an owner count as "Other".
	/// </summary>
	public static string GetOwnerSummary(IEnumerable<MarketDataStreamDto> streams) =>
		string.Join(
			", ",
			streams
				.GroupBy(x => string.IsNullOrEmpty(x.Owner) ? "Other" : x.Owner)
				.OrderByDescending(x => x.Count())
				.ThenBy(x => x.Key, StringComparer.Ordinal)
				.Select(x => $"{x.Key} {x.Count()}")
		);

	/// <summary>How long a stream has been open: "42s", "5m 03s", "2h 07m".</summary>
	public static string FormatAge(TimeSpan age)
	{
		if (age < TimeSpan.Zero)
			age = TimeSpan.Zero;
		if (age.TotalHours >= 1)
			return $"{(int)age.TotalHours}h {age.Minutes:00}m";
		if (age.TotalMinutes >= 1)
			return $"{(int)age.TotalMinutes}m {age.Seconds:00}s";
		return $"{(int)age.TotalSeconds}s";
	}
}
