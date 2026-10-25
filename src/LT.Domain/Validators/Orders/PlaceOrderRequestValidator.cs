using FluentValidation;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.Orders;

namespace LT.Domain.Validators.Orders;

public class PlaceOrderRequestValidator : LSCoreValidatorBase<PlaceOrderRequest>
{
	public PlaceOrderRequestValidator()
	{
		RuleFor(x => x.Contract)
			.Must(x => x is { ConId: > 0 })
			.WithMessage(OrderValidationCodes.OVC_001.GetValidationMessage());

		RuleFor(x => x.Account)
			.Must(x => !string.IsNullOrWhiteSpace(x))
			.WithMessage(OrderValidationCodes.OVC_002.GetValidationMessage());

		RuleFor(x => x)
			.Must(x => x.Quantity != null || x.CashAmount != null)
			.WithMessage(OrderValidationCodes.OVC_003.GetValidationMessage())
			.Must(x => x.Quantity == null || x.CashAmount == null)
			.WithMessage(OrderValidationCodes.OVC_004.GetValidationMessage());

		RuleFor(x => x.Quantity)
			.GreaterThan(0)
			.When(x => x.Quantity != null)
			.WithMessage(OrderValidationCodes.OVC_005.GetValidationMessage());

		RuleFor(x => x.CashAmount)
			.Must(_ => false)
			.When(x => x.CashAmount != null && x.OrderType != OrderType.Market)
			.WithMessage(OrderValidationCodes.OVC_006.GetValidationMessage());

		RuleFor(x => x.CashAmount)
			.InclusiveBetween(OrderConstants.CashAmountMin, OrderConstants.CashAmountMax)
			.When(x => x.CashAmount != null)
			.WithMessage(
				OrderValidationCodes.OVC_009.GetValidationMessage(
					OrderHelpers.FormatMoney(OrderConstants.CashAmountMin),
					OrderHelpers.FormatMoney(OrderConstants.CashAmountMax)
				)
			);

		RuleFor(x => x.LimitPrice)
			.Must(x => x > 0)
			.When(x => x.OrderType == OrderType.Limit)
			.WithMessage(OrderValidationCodes.OVC_007.GetValidationMessage());

		RuleFor(x => x.TimeInForce)
			.Must(
				(request, tif) =>
					OrderHelpers
						.GetTimeInForces(request.Contract!.SecurityType, request.OrderType)
						.Contains(tif)
			)
			.When(x => x.Contract != null)
			.WithMessage(request =>
				OrderValidationCodes.OVC_008.GetValidationMessage(
					$"{request.Contract!.SecurityType} {request.OrderType.ToString().ToLowerInvariant()}",
					string.Join(
						", ",
						OrderHelpers
							.GetTimeInForces(request.Contract.SecurityType, request.OrderType)
							.Select(x => x.ToString().ToUpperInvariant())
					)
				)
			);
	}
}
