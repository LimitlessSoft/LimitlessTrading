using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Calendar;

namespace LT.Domain.Validators.Calendar;

public class GetEarningsResultsRequestValidator : LSCoreValidatorBase<GetEarningsResultsRequest>
{
	public GetEarningsResultsRequestValidator()
	{
		RuleFor(x => x.Date)
			.NotEqual(default(DateOnly))
			.WithMessage(CalendarValidationCodes.CLVC_013.GetValidationMessage());

		RuleFor(x => x.Symbols.Count)
			.LessThanOrEqualTo(EarningsResultConstants.MaxSymbolsPerRequest)
			.WithMessage(
				CalendarValidationCodes.CLVC_014.GetValidationMessage(
					EarningsResultConstants.MaxSymbolsPerRequest.ToString()
				)
			);
	}
}
