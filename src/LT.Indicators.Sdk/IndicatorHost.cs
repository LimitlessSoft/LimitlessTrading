namespace LT.Indicators.Sdk;

/// <summary>How the app runs an indicator. Exceptions from the indicator are not caught here.</summary>
public static class IndicatorHost
{
	/// <summary>
	/// Creates an indicator with <paramref name="create"/> and computes it over
	/// <paramref name="bars"/>, both within <paramref name="budget"/> (a script's constructor is
	/// its code too).
	/// </summary>
	public static IndicatorOutput Run(
		Func<Indicator> create,
		BarData bars,
		IReadOnlyDictionary<string, double> inputs,
		TimeSpan budget
	)
	{
		ArgumentNullException.ThrowIfNull(create);
		using var scope = IndicatorRuntime.Start(budget);
		return create().Run(bars, inputs);
	}

	/// <summary>Runs on zero bars with the defaults: the indicator's name, inputs and plots.</summary>
	public static IndicatorOutput Describe(Func<Indicator> create, TimeSpan budget) =>
		Run(create, BarData.Empty, new Dictionary<string, double>(), budget);
}
