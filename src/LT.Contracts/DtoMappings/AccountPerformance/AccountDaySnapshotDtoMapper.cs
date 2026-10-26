using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.AccountPerformance;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.AccountPerformance;

public class AccountDaySnapshotDtoMapper
	: ILSCoreMapper<AccountDaySnapshotEntity, AccountDaySnapshotDto>
{
	public AccountDaySnapshotDto ToMapped(AccountDaySnapshotEntity source) =>
		new()
		{
			Date = source.Date,
			Currency = source.Currency,
			NetLiquidation = source.NetLiquidation,
			DailyPnl = source.DailyPnl,
			RealizedPnl = source.RealizedPnl,
			UnrealizedPnl = source.UnrealizedPnl,
			FirstUpdate = source.FirstUpdate,
			LastUpdate = source.LastUpdate,
		};
}
