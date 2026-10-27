using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Calendar;

namespace LT.Domain.Validators.Calendar;

public class GetCalendarEventsRequestValidator : LSCoreValidatorBase<GetCalendarEventsRequest>
{
	public GetCalendarEventsRequestValidator()
	{
		RuleFor(x => x.Range)
			.IsInEnum()
			.WithMessage(CalendarValidationCodes.CLVC_001.GetValidationMessage());

		RuleFor(x => x.Filter)
			.IsInEnum()
			.WithMessage(CalendarValidationCodes.CLVC_002.GetValidationMessage());
	}
}
