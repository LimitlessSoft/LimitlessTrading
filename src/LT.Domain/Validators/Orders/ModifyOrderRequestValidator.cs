using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Orders;

namespace LT.Domain.Validators.Orders;

/// <summary>
/// A new limit price for an order; whether this app may change the order at all is
/// <c>OrderHelpers.GetModifyBlock</c>, checked by the manager against the connection.
/// </summary>
public class ModifyOrderRequestValidator : LSCoreValidatorBase<ModifyOrderRequest>
{
	public ModifyOrderRequestValidator()
	{
		RuleFor(x => x.Order)
			.NotNull()
			.WithMessage(OrderValidationCodes.OVC_013.GetValidationMessage());

		RuleFor(x => x.LimitPrice)
			.Must(x => x > 0)
			.WithMessage(OrderValidationCodes.OVC_007.GetValidationMessage());

		RuleFor(x => x.LimitPrice)
			.Must((request, price) => (double)price!.Value != request.Order!.LimitPrice)
			.When(x => x.Order != null && x.LimitPrice > 0)
			.WithMessage(OrderValidationCodes.OVC_017.GetValidationMessage());
	}
}
