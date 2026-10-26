using IBKR.Net;
using LSCore.Mapper.Contracts;
using LSCore.Mapper.Domain;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Helpers;
using Contract = IBKR.Net.Contract;

namespace LT.Ibkr.DtoMappings.Orders;

public class OpenOrderDtoMapper : ILSCoreMapper<OpenOrder, OpenOrderDto>
{
	public OpenOrderDto ToMapped(OpenOrder source)
	{
		var contract = source.Contract.ToMapped<Contract, ContractDto>();
		return new OpenOrderDto
		{
			OrderId = source.OrderId,
			ClientId = source.ClientId,
			PermId = source.PermId,
			Account = source.Order.Account,
			Symbol = ContractHelpers.GetDisplayName(contract),
			SecurityType = contract.SecurityType,
			Action = source.Order.Action,
			Quantity = source.Order.Quantity,
			Filled = source.Filled,
			OrderType = source.Order.OrderType,
			LimitPrice = source.Order.LimitPrice,
			AuxPrice = source.Order.AuxPrice,
			TimeInForce = source.Order.TimeInForce,
			Status = source.Status,
			Contract = contract,
		};
	}
}
