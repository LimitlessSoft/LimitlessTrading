using System.Globalization;
using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Indicators;

namespace LT.Domain.Validators.Indicators;

/// <summary>Every value must be an input of the definition and fit its type and range.</summary>
public class UpdateIndicatorInputsRequestValidator
	: LSCoreValidatorBase<UpdateIndicatorInputsRequest>
{
	public UpdateIndicatorInputsRequestValidator()
	{
		RuleFor(x => x)
			.Custom(
				(request, context) =>
				{
					foreach (var (name, value) in request.Inputs)
					{
						var input = request.Definition.Inputs.FirstOrDefault(x => x.Name == name);
						var message = input switch
						{
							null => IndicatorValidationCodes.IVC_002.GetValidationMessage(name),
							_ when !double.IsFinite(value) =>
								IndicatorValidationCodes.IVC_003.GetValidationMessage(name),
							{ Type: IndicatorInputType.Bool } when value is not (0 or 1) =>
								IndicatorValidationCodes.IVC_007.GetValidationMessage(name),
							{ Type: IndicatorInputType.Int } when value != Math.Round(value) =>
								IndicatorValidationCodes.IVC_004.GetValidationMessage(name),
							{ Min: { } min } when value < min =>
								IndicatorValidationCodes.IVC_005.GetValidationMessage(
									name,
									Format(min)
								),
							{ Max: { } max } when value > max =>
								IndicatorValidationCodes.IVC_006.GetValidationMessage(
									name,
									Format(max)
								),
							_ => null,
						};
						if (message != null)
							context.AddFailure(name, message);
					}
				}
			);
	}

	private static string Format(double value) =>
		value.ToString("0.####", CultureInfo.CurrentCulture);
}
