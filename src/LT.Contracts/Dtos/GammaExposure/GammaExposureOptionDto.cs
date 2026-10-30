namespace LT.Contracts.Dtos.GammaExposure;

/// <summary>What one option of a gamma exposure reading brought: IB's model gamma and its open interest.</summary>
public class GammaExposureOptionDto
{
	public double Strike { get; set; }

	/// <summary>C or P.</summary>
	public string Right { get; set; } = string.Empty;

	/// <summary>Per 1.00 move of the underlying, per share; null when IB sent none in time.</summary>
	public double? Gamma { get; set; }

	/// <summary>IB's model implied volatility as a fraction; null when IB sent none.</summary>
	public double? ImpliedVolatility { get; set; }

	/// <summary>Contracts open; null when IB sent none in time.</summary>
	public decimal? OpenInterest { get; set; }
}
