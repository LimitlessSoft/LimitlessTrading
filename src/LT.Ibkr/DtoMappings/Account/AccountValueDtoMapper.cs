using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Account;
using TwsApi;

namespace LT.Ibkr.DtoMappings.Account;

public class AccountValueDtoMapper : ILSCoreMapper<AccountValue, AccountValueDto>
{
	public AccountValueDto ToMapped(AccountValue source) =>
		new()
		{
			Account = source.Account,
			Tag = source.Tag,
			Value = source.Value,
			Currency = source.Currency,
		};
}
