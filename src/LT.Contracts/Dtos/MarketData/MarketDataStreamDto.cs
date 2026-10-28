using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.MarketData;

/// <summary>One market data line in use by the app, as listed from the status bar.</summary>
public class MarketDataStreamDto
{
	/// <summary>Unique for the app's run; what <c>IMarketDataManager.ReleaseLine</c> takes.</summary>
	public long Id { get; init; }

	public MarketDataStreamKind Kind { get; init; }

	/// <summary>The streamed contract: the quoted one, or the linked contract / broad tape of news.</summary>
	public ContractDto Contract { get; init; } = new();

	/// <summary>IB generic tick list asked for on the same line (e.g. "106"); empty when none.</summary>
	public string GenericTicks { get; init; } = string.Empty;

	/// <summary>
	/// Who uses it (<c>MarketDataStreamOwners</c>), joined with ", " when a quote line is shared;
	/// empty when unknown.
	/// </summary>
	public string Owner { get; init; } = string.Empty;

	/// <summary>
	/// Every owner of the line, each once, in the order they joined; a quote line is shared by
	/// every subscriber of the same contract and generic ticks.
	/// </summary>
	public IReadOnlyList<string> Owners { get; init; } = [];

	/// <summary>How many subscriptions read the line (1 unless shared).</summary>
	public int Subscribers { get; init; } = 1;

	public DateTimeOffset OpenedAt { get; init; }
}
