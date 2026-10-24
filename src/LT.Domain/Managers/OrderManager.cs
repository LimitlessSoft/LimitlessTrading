using LT.Contracts.Dtos.Orders;
using LT.Contracts.Interfaces.IApiClients;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Domain.Managers;

public class OrderManager(IIbkrApiClient ibkrApiClient) : IOrderManager
{
	public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		string? account,
		CancellationToken cancellationToken = default
	)
	{
		if (!ibkrApiClient.IsConnected)
			return [];

		var orders = await ibkrApiClient.GetOpenOrdersAsync(cancellationToken);
		return orders
			.Where(x => string.IsNullOrEmpty(account) || x.Account == account)
			.OrderBy(x => x.OrderId)
			.ToList();
	}
}
