using IBKR.Net;
using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Account;

namespace LT.Ibkr.DtoMappings.Account;

public class AccountPnlDtoMapper : ILSCoreMapper<AccountPnl, AccountPnlDto>
{
	public AccountPnlDto ToMapped(AccountPnl source) =>
		new()
		{
			DailyPnl = source.DailyPnl,
			UnrealizedPnl = source.UnrealizedPnl,
			RealizedPnl = source.RealizedPnl,
		};
}
