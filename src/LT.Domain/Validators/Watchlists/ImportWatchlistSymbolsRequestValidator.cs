using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Watchlists;

namespace LT.Domain.Validators.Watchlists;

/// <summary>
/// A plain list must hold between one and the most symbols a watchlist can, each looking like a
/// ticker, before anything is asked of IB.
/// </summary>
public class ImportWatchlistSymbolsRequestValidator
	: LSCoreValidatorBase<ImportWatchlistSymbolsRequest>
{
	public ImportWatchlistSymbolsRequestValidator()
	{
		RuleFor(x => x.Symbols)
			.Custom(
				(symbols, context) =>
				{
					var error =
						symbols.Count == 0 ? WatchlistValidationCodes.WVC_016.GetValidationMessage()
						: symbols.Count > MarketDataConstants.WatchlistMaxItems
							? WatchlistValidationCodes.WVC_002.GetValidationMessage(
								MarketDataConstants.WatchlistMaxItems
							)
						: symbols.FirstOrDefault(x => !WatchlistFileHelpers.IsValidSymbol(x))
							is { } invalid
							? WatchlistValidationCodes.WVC_018.GetValidationMessage(
								invalid.Length > MarketDataConstants.SymbolMaxLength
									? invalid[..MarketDataConstants.SymbolMaxLength] + "…"
									: invalid
							)
						: null;
					if (error != null)
						context.AddFailure(nameof(ImportWatchlistSymbolsRequest.Symbols), error);
				}
			);
	}
}
