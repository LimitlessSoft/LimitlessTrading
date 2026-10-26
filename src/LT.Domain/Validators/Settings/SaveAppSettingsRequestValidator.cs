using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Settings;

namespace LT.Domain.Validators.Settings;

public class SaveAppSettingsRequestValidator : LSCoreValidatorBase<SaveAppSettingsRequest>
{
	public SaveAppSettingsRequestValidator()
	{
		RuleFor(x => x.HeartbeatSeconds)
			.InclusiveBetween(
				ConnectionConstants.HeartbeatSecondsMin,
				ConnectionConstants.HeartbeatSecondsMax
			)
			.WithMessage(
				SettingsValidationCodes.SVC_001.GetValidationMessage(
					ConnectionConstants.HeartbeatSecondsMin,
					ConnectionConstants.HeartbeatSecondsMax
				)
			);

		RuleFor(x => x.MarketDataType)
			.IsInEnum()
			.WithMessage(SettingsValidationCodes.SVC_002.GetValidationMessage());

		RuleFor(x => x.LayoutStyle)
			.IsInEnum()
			.WithMessage(SettingsValidationCodes.SVC_003.GetValidationMessage());

		RuleFor(x => x.CacheMaxSizeMb)
			.InclusiveBetween(CacheConstants.MaxSizeMbMin, CacheConstants.MaxSizeMbMax)
			.WithMessage(
				SettingsValidationCodes.SVC_004.GetValidationMessage(
					CacheConstants.MaxSizeMbMin,
					CacheConstants.MaxSizeMbMax
				)
			);

		RuleFor(x => x.ChartLiveUpdateMode)
			.IsInEnum()
			.WithMessage(SettingsValidationCodes.SVC_006.GetValidationMessage());

		RuleFor(x => x.ChartRedrawIntervalMs)
			.InclusiveBetween(
				ChartConstants.RedrawIntervalMsMin,
				ChartConstants.RedrawIntervalMsMax
			)
			.WithMessage(
				SettingsValidationCodes.SVC_007.GetValidationMessage(
					ChartConstants.RedrawIntervalMsMin,
					ChartConstants.RedrawIntervalMsMax
				)
			);

		RuleFor(x => x.DisplayTimeZoneId)
			.Must(DisplayTimeHelpers.IsValidZoneId)
			.WithMessage(SettingsValidationCodes.SVC_005.GetValidationMessage());
	}
}
