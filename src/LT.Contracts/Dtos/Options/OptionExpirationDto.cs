namespace LT.Contracts.Dtos.Options;

/// <summary>One expiration of an underlying's options (a tab in the option chain).</summary>
public class OptionExpirationDto
{
	/// <summary>yyyyMMdd.</summary>
	public string Expiration { get; set; } = string.Empty;

	/// <summary>Which class expires that day, e.g. SPXW for SPX weeklies.</summary>
	public string TradingClass { get; set; } = string.Empty;
}
