using IBApi;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Helpers;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Orders;

public class OpenOrderDtoMapper : ILSCoreMapper<OpenOrderInfo, OpenOrderDto>
{
	public OpenOrderDto ToMapped(OpenOrderInfo source)
	{
		var contract = source.Contract.ToMapped<Contract, ContractDto>();
		return new OpenOrderDto
		{
			OrderId = source.OrderId,
			PermId = source.Order.PermId,
			Account = source.Order.Account ?? string.Empty,
			Symbol = ContractHelpers.GetDisplayName(contract),
			SecurityType = contract.SecurityType,
			Action = source.Order.Action ?? string.Empty,
			Quantity = source.Order.TotalQuantity,
			Filled =
				source.Order.FilledQuantity == decimal.MaxValue ? 0 : source.Order.FilledQuantity,
			OrderType = source.Order.OrderType ?? string.Empty,
			LimitPrice = IbValueHelpers.Clean(source.Order.LmtPrice),
			AuxPrice = IbValueHelpers.Clean(source.Order.AuxPrice),
			TimeInForce = source.Order.Tif ?? string.Empty,
			Status = source.OrderState.Status ?? string.Empty,
		};
	}
}
