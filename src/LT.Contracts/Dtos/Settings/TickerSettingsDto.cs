using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

/// <summary>
/// Settings of one contract (Settings → General → Ticker settings), keyed by
/// <see cref="ContractDto.ConId"/>: an option is its own entry, apart from its underlying.
/// </summary>
public class TickerSettingsDto
{
	public ContractDto Contract { get; set; } = new();

	/// <summary>Charts of this contract use it instead of the app-wide chart live update mode.</summary>
	public ChartLiveUpdateMode ChartLiveUpdateMode { get; set; }
}
