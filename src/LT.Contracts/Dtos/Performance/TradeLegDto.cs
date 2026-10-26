using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Performance;

/// <summary>
/// One FIFO match inside a trade: the part of an entry fill that a later exit fill closed. A
/// trade scaled in and out has several, one per (entry lot, exit fill) pair.
/// </summary>
public class TradeLegDto
{
	public string Account { get; set; } = string.Empty;
	public TradeSide Side { get; set; }
	public decimal Quantity { get; set; }
	public string EntryExecId { get; set; } = string.Empty;
	public DateTimeOffset EntryTime { get; set; }
	public double EntryPrice { get; set; }
	public string ExitExecId { get; set; } = string.Empty;
	public DateTimeOffset ExitTime { get; set; }
	public double ExitPrice { get; set; }

	/// <summary>P&amp;L of the matched quantity before commission.</summary>
	public double GrossPnl { get; set; }

	/// <summary>The entry's and the exit's commission for the matched quantity.</summary>
	public double Commission { get; set; }
	public double NetPnl => GrossPnl - Commission;
}
