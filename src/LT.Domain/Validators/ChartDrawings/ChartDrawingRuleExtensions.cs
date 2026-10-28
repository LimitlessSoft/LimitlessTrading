using FluentValidation;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;

namespace LT.Domain.Validators.ChartDrawings;

/// <summary>The style and Fibonacci level rules shared by the create and properties requests.</summary>
public static class ChartDrawingRuleExtensions
{
	/// <summary>A style, when there is one: a hex colour, a known line style, a width in range.</summary>
	public static void AddStyleRules<T>(
		this AbstractValidator<T> validator,
		System.Linq.Expressions.Expression<Func<T, ChartDrawingStyleDto?>> style
	)
	{
		validator
			.RuleFor(style)
			.Must(x => x == null || ChartDrawingHelpers.IsValidColor(x.Color))
			.WithMessage(ChartDrawingValidationCodes.CDVC_010.GetValidationMessage());
		validator
			.RuleFor(style)
			.Must(x => x == null || Enum.IsDefined(x.LineStyle))
			.WithMessage(ChartDrawingValidationCodes.CDVC_012.GetValidationMessage());
		validator
			.RuleFor(style)
			.Must(x => x == null || ChartDrawingHelpers.IsValidLineWidth(x.LineWidth))
			.WithMessage(
				ChartDrawingValidationCodes.CDVC_011.GetValidationMessage(
					ChartDrawingConstants.MinLineWidth,
					ChartDrawingConstants.MaxLineWidth
				)
			);
	}

	/// <summary>
	/// Fibonacci levels, when given: 1 … <see cref="ChartDrawingConstants.MaxFibLevels"/> of them,
	/// each in range and there once, colours hex or none.
	/// </summary>
	public static void AddFibLevelRules<T>(
		this AbstractValidator<T> validator,
		System.Linq.Expressions.Expression<Func<T, List<ChartDrawingFibLevelDto>?>> levels
	)
	{
		validator
			.RuleFor(levels)
			.Must(x => x == null || x.Count is > 0 and <= ChartDrawingConstants.MaxFibLevels)
			.WithMessage(
				ChartDrawingValidationCodes.CDVC_013.GetValidationMessage(
					ChartDrawingConstants.MaxFibLevels
				)
			);
		validator
			.RuleFor(levels)
			.Must(x =>
				x == null || x.All(l => l != null && ChartDrawingHelpers.IsValidFibLevel(l.Value))
			)
			.WithMessage(
				ChartDrawingValidationCodes.CDVC_014.GetValidationMessage(
					ChartDrawingConstants.MinFibLevel,
					ChartDrawingConstants.MaxFibLevel
				)
			);
		validator
			.RuleFor(levels)
			.Must(x =>
				x == null || x.Any(l => l == null) || ChartDrawingHelpers.HasUniqueFibLevels(x)
			)
			.WithMessage(ChartDrawingValidationCodes.CDVC_015.GetValidationMessage());
		validator
			.RuleFor(levels)
			.Must(x =>
				x == null
				|| x.All(l => l?.Color == null || ChartDrawingHelpers.IsValidColor(l.Color))
			)
			.WithMessage(ChartDrawingValidationCodes.CDVC_010.GetValidationMessage());
	}
}
