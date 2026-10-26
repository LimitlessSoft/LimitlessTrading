using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.MarketDepth;

namespace LT.Domain.Validators.MarketDepth;

public class SubscribeOrderBookRequestValidator : LSCoreValidatorBase<SubscribeOrderBookRequest>
{
	public SubscribeOrderBookRequestValidator()
	{
		RuleFor(x => x.Contract.ConId)
			.GreaterThan(0)
			.WithMessage(ContractValidationCodes.CTVC_004.GetValidationMessage());

		RuleFor(x => x.Rows)
			.InclusiveBetween(1, MarketDepthConstants.MaxRows)
			.WithMessage(
				MarketDepthValidationCodes.MDPVC_001.GetValidationMessage(
					MarketDepthConstants.MaxRows
				)
			);
	}
}
