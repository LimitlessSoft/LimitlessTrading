using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Flex;

namespace LT.Domain.Validators.Flex;

/// <summary>All empty is valid (Flex off); otherwise a token and an Activity query id, both well formed.</summary>
public class SaveFlexSettingsRequestValidator : LSCoreValidatorBase<SaveFlexSettingsRequest>
{
	public SaveFlexSettingsRequestValidator()
	{
		RuleFor(x => x.Token)
			.NotEmpty()
			.When(x => x.ActivityQueryId.Length > 0 || x.TradeConfirmQueryId.Length > 0)
			.WithMessage(FlexValidationCodes.FVC_001.GetValidationMessage());

		RuleFor(x => x.Token)
			.Length(FlexConstants.TokenMinLength, FlexConstants.TokenMaxLength)
			.Must(x => x.All(char.IsAsciiLetterOrDigit))
			.When(x => x.Token.Length > 0)
			.WithMessage(
				FlexValidationCodes.FVC_002.GetValidationMessage(
					FlexConstants.TokenMinLength,
					FlexConstants.TokenMaxLength
				)
			);

		RuleFor(x => x.ActivityQueryId)
			.NotEmpty()
			.When(x => x.Token.Length > 0)
			.WithMessage(FlexValidationCodes.FVC_003.GetValidationMessage());

		RuleFor(x => x.ActivityQueryId)
			.Must(BeAQueryId)
			.When(x => x.ActivityQueryId.Length > 0)
			.WithMessage(
				FlexValidationCodes.FVC_004.GetValidationMessage(FlexConstants.QueryIdMaxLength)
			);

		RuleFor(x => x.TradeConfirmQueryId)
			.Must(BeAQueryId)
			.When(x => x.TradeConfirmQueryId.Length > 0)
			.WithMessage(
				FlexValidationCodes.FVC_004.GetValidationMessage(FlexConstants.QueryIdMaxLength)
			);
	}

	private static bool BeAQueryId(string value) =>
		value.Length <= FlexConstants.QueryIdMaxLength && value.All(char.IsAsciiDigit);
}
