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

	/// <summary>Which widget or feature opened it (<c>MarketDataStreamOwners</c>); empty when unknown.</summary>
	public string Owner { get; init; } = string.Empty;

	public DateTimeOffset OpenedAt { get; init; }
}
