namespace LT.Contracts.Dtos.GammaExposure;

/// <summary>
/// One strike's gamma exposure, in dollars per 1% move of the underlying, taken as dealers long
/// the calls and short the puts: calls count positive, puts negative.
/// </summary>
public class GammaExposureStrikeDto
{
	public double Strike { get; set; }
	public decimal CallOpenInterest { get; set; }
	public decimal PutOpenInterest { get; set; }
	public double CallExposure { get; set; }

	/// <summary>Zero or negative.</summary>
	public double PutExposure { get; set; }
	public double NetExposure => CallExposure + PutExposure;
}
