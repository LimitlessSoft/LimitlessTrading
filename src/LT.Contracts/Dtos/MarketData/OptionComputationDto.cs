namespace LT.Contracts.Dtos.MarketData;

/// <summary>
/// IB's computation for an option from one of its prices (tickOptionComputation): implied
/// volatility, greeks and the underlying price it used; null where IB computed none. Gamma is per
/// 1.00 move of the underlying, per option share.
/// </summary>
public class OptionComputationDto
{
	public double? ImpliedVolatility { get; set; }
	public double? Delta { get; set; }
	public double? OptionPrice { get; set; }
	public double? PresentValueDividend { get; set; }
	public double? Gamma { get; set; }
	public double? Vega { get; set; }
	public double? Theta { get; set; }
	public double? UnderlyingPrice { get; set; }
}
