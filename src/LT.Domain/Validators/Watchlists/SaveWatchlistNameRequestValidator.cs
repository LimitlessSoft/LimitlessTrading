using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Watchlists;

namespace LT.Domain.Validators.Watchlists;

public class SaveWatchlistNameRequestValidator : LSCoreValidatorBase<SaveWatchlistNameRequest>
{
	public SaveWatchlistNameRequestValidator()
	{
		RuleFor(x => x.Name)
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.WithMessage(WatchlistValidationCodes.WVC_003.GetValidationMessage());

		RuleFor(x => x.Name.Trim())
			.MaximumLength(MarketDataConstants.WatchlistNameMaxLength)
			.WithMessage(
				WatchlistValidationCodes.WVC_004.GetValidationMessage(
					MarketDataConstants.WatchlistNameMaxLength
				)
			);
	}
}
