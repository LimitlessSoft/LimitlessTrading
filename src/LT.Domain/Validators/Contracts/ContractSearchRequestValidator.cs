using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Contracts;

namespace LT.Domain.Validators.Contracts;

public class ContractSearchRequestValidator : LSCoreValidatorBase<ContractSearchRequest>
{
	public ContractSearchRequestValidator()
	{
		RuleFor(x => x.Symbol)
			.NotEmpty()
			.WithMessage(ContractValidationCodes.CTVC_001.GetValidationMessage())
			.MaximumLength(MarketDataConstants.SymbolMaxLength)
			.WithMessage(
				ContractValidationCodes.CTVC_002.GetValidationMessage(
					MarketDataConstants.SymbolMaxLength
				)
			);

		RuleFor(x => x.SecurityType)
			.Must(x => MarketDataConstants.SearchableSecurityTypes.Contains(x))
			.WithMessage(
				ContractValidationCodes.CTVC_003.GetValidationMessage(
					string.Join(", ", MarketDataConstants.SearchableSecurityTypes)
				)
			);
	}
}
