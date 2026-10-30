using LT.Contracts.Dtos.Contracts;

namespace LT.Contracts.Dtos.GammaExposure;

/// <summary>Gamma exposure by strike of one expiration, as read at <see cref="ReadAt"/>.</summary>
public class GammaExposureDto
{
	public ContractDto Underlying { get; set; } = new();

	/// <summary>yyyyMMdd.</summary>
	public string Expiration { get; set; } = string.Empty;
	public double SpotPrice { get; set; }

	/// <summary>Ascending by strike.</summary>
	public IReadOnlyList<GammaExposureStrikeDto> Strikes { get; set; } = [];
	public double CallExposure { get; set; }
	public double PutExposure { get; set; }
	public double NetExposure => CallExposure + PutExposure;

	/// <summary>
	/// The spot price, nearest the current one, where the net exposure changes sign when every
	/// option's gamma is worked out again there (Black-Scholes with IB's implied volatility); null
	/// when it doesn't within the strikes read.
	/// </summary>
	public double? GammaFlip { get; set; }

	/// <summary>Options asked for (a call and a put per strike).</summary>
	public int OptionsRequested { get; set; }

	/// <summary>Options that brought both a gamma and an open interest; only those count.</summary>
	public int OptionsReceived { get; set; }
	public DateTimeOffset ReadAt { get; set; }
}
