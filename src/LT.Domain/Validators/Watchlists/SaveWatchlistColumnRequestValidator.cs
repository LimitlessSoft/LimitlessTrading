using FluentValidation;
using LSCore.Validation.Domain;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Watchlists;

namespace LT.Domain.Validators.Watchlists;

public class SaveWatchlistColumnRequestValidator : LSCoreValidatorBase<SaveWatchlistColumnRequest>
{
	public SaveWatchlistColumnRequestValidator()
	{
		RuleFor(x => x.Name)
			.Custom(
				(name, context) =>
				{
					if (WatchlistHelpers.GetColumnNameError(name) is { } error)
						context.AddFailure(nameof(SaveWatchlistColumnRequest.Name), error);
				}
			);
	}
}
