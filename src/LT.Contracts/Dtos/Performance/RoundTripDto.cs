using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Performance;

/// <summary>
/// One trade: from flat to flat in one contract of one account. Scaling in and out stays in the
/// same trade; a fill that goes through zero closes it and opens the next one.
/// </summary>
public class RoundTripDto
{
	public string Account { get; set; } = string.Empty;
	public string Symbol { get; set; } = string.Empty;
	public string SecurityType { get; set; } = string.Empty;
	public TradeSide Side { get; set; }

	/// <summary>Everything bought (long) or sold short (short) while the trade was open.</summary>
	public decimal Quantity { get; set; }
	public double Multiplier { get; set; } = 1;
	public double EntryPrice { get; set; }

	/// <summary>Average price of the fills that reduced the position; null before the first.</summary>
	public double? ExitPrice { get; set; }
	public DateTimeOffset OpenTime { get; set; }
	public DateTimeOffset? CloseTime { get; set; }
	public bool IsClosed { get; set; }

	/// <summary>Realized P&amp;L before commission (FIFO lots; so far, while still open).</summary>
	public double GrossPnl { get; set; }
	public double Commission { get; set; }
	public double NetPnl => GrossPnl - Commission;
	public TimeSpan? HoldTime => CloseTime - OpenTime;
	public int FillCount { get; set; }
}
