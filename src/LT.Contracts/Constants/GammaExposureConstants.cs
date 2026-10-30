namespace LT.Contracts.Constants;

/// <summary>Limits of the gamma exposure widget (IGammaExposureManager).</summary>
public static class GammaExposureConstants
{
	/// <summary>Strikes on each side of the spot price the widget reads by default.</summary>
	public const int DefaultStrikesAroundSpot = 20;

	/// <summary>The most strikes on each side one request may read (two lines per strike, in turns).</summary>
	public const int MaxStrikesAroundSpot = 60;

	/// <summary>
	/// Option lines one reading holds at once at most; each is released as soon as its greeks and
	/// open interest came (IB sent both within 1.5 s when recorded).
	/// </summary>
	public const int MaxConcurrentLines = 20;

	/// <summary>Lines a reading leaves free for every other widget; it takes none below that.</summary>
	public const int ReservedLines = 10;

	/// <summary>An option still missing its greeks or open interest after this is read as it is.</summary>
	public static readonly TimeSpan OptionReadTimeout = TimeSpan.FromSeconds(6);

	/// <summary>How often a shown widget reads the exposure again.</summary>
	public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(2);

	/// <summary>
	/// Gamma exposure is in dollars per 1% move of the underlying: gamma × open interest ×
	/// multiplier × spot² × this.
	/// </summary>
	public const double OnePercentMove = 0.01;

	/// <summary>Multiplier of an option that doesn't report one (US equity options).</summary>
	public const double DefaultMultiplier = 100;

	/// <summary>
	/// The least time to expiration the gamma flip uses, in years (30 minutes), so an option
	/// expiring today still has a finite gamma profile.
	/// </summary>
	public const double MinYearsToExpiration = 0.5 / 24 / MarketDataConstants.DaysPerYear;

	/// <summary>Spot prices tried between the lowest and highest strike when looking for the flip.</summary>
	public const int FlipSearchSteps = 400;
}
