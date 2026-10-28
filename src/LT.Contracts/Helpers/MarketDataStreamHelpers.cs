using LT.Contracts.Constants;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>Display text of the market data lines listed from the status bar.</summary>
public static class MarketDataStreamHelpers
{
	/// <summary>
	/// "Quote", "Quote + option IV" (generic tick 106), "Quote + avg volume" (165),
	/// "Quote + volume rate" (295), several joined ("Quote + avg volume + volume rate"),
	/// "Quote + ticks 233,…" when any of them is another, or "News".
	/// </summary>
	public static string GetKindText(MarketDataStreamDto stream)
	{
		if (stream.Kind == MarketDataStreamKind.News)
			return "News";
		if (string.IsNullOrWhiteSpace(stream.GenericTicks))
			return "Quote";
		var ticks = stream.GenericTicks.Trim();
		var names = ticks
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(GetGenericTickName)
			.ToList();
		return names.Count > 0 && names.All(x => x != null)
			? $"Quote + {string.Join(" + ", names)}"
			: $"Quote + ticks {ticks}";
	}

	private static string? GetGenericTickName(string tick) =>
		tick switch
		{
			MarketDataConstants.OptionImpliedVolatilityGenericTick => "option IV",
			MarketDataConstants.AverageVolumeGenericTick => "avg volume",
			MarketDataConstants.VolumeRateGenericTick => "volume rate",
			_ => null,
		};

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
	/// Lines per owner, most first then by name: "Watchlist · US 40, Chart 2". A shared line
	/// counts for each of its owners; streams without an owner count as "Other".
	/// </summary>
	public static string GetOwnerSummary(IEnumerable<MarketDataStreamDto> streams) =>
		string.Join(
			", ",
			streams
				.SelectMany(GetOwners)
				.GroupBy(x => x)
				.OrderByDescending(x => x.Count())
				.ThenBy(x => x.Key, StringComparer.Ordinal)
				.Select(x => $"{x.Key} {x.Count()}")
		);

	// A line made by hand (news) has only Owner
	private static IEnumerable<string> GetOwners(MarketDataStreamDto stream) =>
		stream.Owners.Count > 0 ? stream.Owners
		: string.IsNullOrEmpty(stream.Owner) ? ["Other"]
		: [stream.Owner];

	/// <summary>The owner column: every owner of the line, "—" for none.</summary>
	public static string GetOwnerText(MarketDataStreamDto stream) =>
		string.IsNullOrEmpty(stream.Owner) ? "—"
		: stream.Subscribers > 1 ? $"{stream.Owner} ({stream.Subscribers})"
		: stream.Owner;

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
