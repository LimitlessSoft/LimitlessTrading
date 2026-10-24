using LSCore.Validation.Contracts;

namespace LT.Contracts.Enums.ValidationCodes;

public enum WatchlistValidationCodes
{
	[LSCoreValidationMessage("{0} is already in the watchlist.")]
	WVC_001,

	[LSCoreValidationMessage("Watchlist can hold at most {0} contracts.")]
	WVC_002,
}
