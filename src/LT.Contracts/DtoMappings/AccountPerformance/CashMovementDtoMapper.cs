using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.AccountPerformance;

public class CashMovementDtoMapper : ILSCoreMapper<CashMovementEntity, CashMovementDto>
{
	public CashMovementDto ToMapped(CashMovementEntity source) =>
		new()
		{
			Date = source.Date,
			Kind = source.Kind,
			Amount = source.Amount,
			Currency = source.Currency,
			AmountInCurrency = source.AmountInCurrency,
			Description = source.Description,
		};
}
