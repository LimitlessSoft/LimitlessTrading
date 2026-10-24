using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Requests.Options;

public class GetOptionChainRequest
{
	public ContractDto Underlying { get; set; } = new();

	/// <summary>yyyyMM (whole month) or yyyyMMdd (one expiration).</summary>
	public string Expiry { get; set; } = string.Empty;

	/// <summary>Optional; picks one class when several expire the same day (SPX vs SPXW).</summary>
	public string TradingClass { get; set; } = string.Empty;
}
