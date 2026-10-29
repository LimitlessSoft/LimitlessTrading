using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// One trade of the footprint trade cache (<c>IFootprintTapeRepository</c>): a struct, as a
/// heavy symbol's day is a million of them. <see cref="QuoteSide"/> is the side by the quote rule
/// when the trade came live with a bid / ask to tell it; otherwise
/// <see cref="FootprintSide.Unknown"/>, and the reader tells it by the tick rule from the trades
/// before it (as for the past trades IB pages).
/// </summary>
public readonly record struct FootprintTapeTradeDto(
	long UnixSeconds,
	double Price,
	double Size,
	FootprintSide QuoteSide
)
{
	/// <summary>IB's time, whole seconds.</summary>
	public DateTimeOffset Time => DateTimeOffset.FromUnixTimeSeconds(UnixSeconds);
}
