using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Account;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Account;

public class AccountPnlDtoMapper : ILSCoreMapper<AccountPnl, AccountPnlDto>
{
	public AccountPnlDto ToMapped(AccountPnl source) =>
		new()
		{
			DailyPnl = source.DailyPnL,
			UnrealizedPnl = source.UnrealizedPnL,
			RealizedPnl = source.RealizedPnL,
		};
}
