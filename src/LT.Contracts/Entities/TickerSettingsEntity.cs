using LT.Contracts.Enums;

namespace LT.Contracts.Entities;

/// <summary>
/// Settings of one contract (Settings → General → Ticker settings), keyed by its conId: an
/// option is its own entry, apart from its underlying.
/// </summary>
public class TickerSettingsEntity
{
	public ContractEntity Contract { get; set; } = new();

	/// <summary>Charts of this contract use it instead of the app-wide chart live update mode.</summary>
	public ChartLiveUpdateMode ChartLiveUpdateMode { get; set; } = ChartLiveUpdateMode.RealtimeBars;
}
