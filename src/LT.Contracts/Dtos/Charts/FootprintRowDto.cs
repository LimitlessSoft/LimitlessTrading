namespace LT.Contracts.Dtos.Charts;

/// <summary>
/// One row of a footprint bar as drawn: <c>FootprintHelpers.GetRows</c> groups the bar's levels
/// by a number of ticks per row (more when zoomed out), lowest row first.
/// </summary>
public class FootprintRowDto
{
	/// <summary>The row's lowest price level (tick index × tick size).</summary>
	public double Price { get; set; }

	/// <summary>The row's highest price level; equals <see cref="Price"/> for one tick per row.</summary>
	public double HighPrice { get; set; }
	public decimal BidVolume { get; set; }
	public decimal AskVolume { get; set; }
	public decimal Volume => BidVolume + AskVolume;

	/// <summary>Sellers dominate: the bid is the imbalance ratio times the ask one row up.</summary>
	public bool IsBidImbalance { get; set; }

	/// <summary>Buyers dominate: the ask is the imbalance ratio times the bid one row down.</summary>
	public bool IsAskImbalance { get; set; }

	/// <summary>The bar's point of control: the row with the most volume.</summary>
	public bool IsPoc { get; set; }
}
