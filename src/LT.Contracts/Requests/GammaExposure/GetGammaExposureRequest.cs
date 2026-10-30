using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Requests.GammaExposure;

public class GetGammaExposureRequest
{
	public ContractDto Underlying { get; set; } = new();

	/// <summary>One expiration, yyyyMMdd.</summary>
	public string Expiration { get; set; } = string.Empty;

	/// <summary>Optional; picks one class when several expire the same day (SPX vs SPXW).</summary>
	public string TradingClass { get; set; } = string.Empty;

	/// <summary>Strikes read on each side of the spot price.</summary>
	public int StrikesAroundSpot { get; set; } = GammaExposureConstants.DefaultStrikesAroundSpot;
}
