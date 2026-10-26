using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.BuiltIns;

/// <summary>The indicators that ship with the app, by key (their id is "builtin:&lt;key&gt;").</summary>
public static class BuiltInIndicators
{
	public static IReadOnlyList<(string Key, Func<Indicator> Create)> All { get; } =
	[
		("sma", () => new SmaIndicator()),
		("ema", () => new EmaIndicator()),
		("vwap", () => new VwapIndicator()),
		("bb", () => new BollingerBandsIndicator()),
		("rsi", () => new RsiIndicator()),
		("macd", () => new MacdIndicator()),
		("atr", () => new AtrIndicator()),
		("volume-ma", () => new VolumeMaIndicator()),
	];
}
