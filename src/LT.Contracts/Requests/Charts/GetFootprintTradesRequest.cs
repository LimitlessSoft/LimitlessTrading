using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Requests.Charts;

/// <summary>A page of a contract's past trades for a footprint, ending before <see cref="Before"/>.</summary>
public class GetFootprintTradesRequest
{
	public ContractDto Contract { get; set; } = new();
	public bool UseRegularTradingHours { get; set; }

	/// <summary>The page holds trades strictly before this (the oldest trade of the page after it).</summary>
	public DateTimeOffset Before { get; set; }
}
