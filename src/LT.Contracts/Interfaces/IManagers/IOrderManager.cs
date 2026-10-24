using LT.Contracts.Dtos.Orders;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>Read-only view of orders; this build places no orders.</summary>
public interface IOrderManager
{
	Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
		string? account,
		CancellationToken cancellationToken = default
	);
}
