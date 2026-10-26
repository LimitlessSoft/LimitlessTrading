using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.PriceAlerts;

public class PriceAlertDtoMapper : ILSCoreMapper<PriceAlertEntity, PriceAlertDto>
{
	public PriceAlertDto ToMapped(PriceAlertEntity source) =>
		new()
		{
			Id = source.Id,
			Contract = new ContractDtoMapper().ToMapped(source.Contract),
			Price = source.Price,
			Condition = source.Condition,
			Note = source.Note,
			CreatedAt = source.CreatedAt,
			TriggeredAt = source.TriggeredAt,
			TriggerPrice = source.TriggerPrice,
		};
}
