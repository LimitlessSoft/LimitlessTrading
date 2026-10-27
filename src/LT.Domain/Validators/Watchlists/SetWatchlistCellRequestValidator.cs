using FluentValidation;
using LSCore.Validation.Domain;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Watchlists;

namespace LT.Domain.Validators.Watchlists;

public class SetWatchlistCellRequestValidator : LSCoreValidatorBase<SetWatchlistCellRequest>
{
	public SetWatchlistCellRequestValidator()
	{
		RuleFor(x => x.Value)
			.Custom(
				(value, context) =>
				{
					if (WatchlistHelpers.GetCellValueError(value) is { } error)
						context.AddFailure(nameof(SetWatchlistCellRequest.Value), error);
				}
			);
	}
}
