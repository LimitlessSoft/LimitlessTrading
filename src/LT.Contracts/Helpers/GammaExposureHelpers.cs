using System.Globalization;
using System.Text.RegularExpressions;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.GammaExposure;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Interfaces.IManagers;

namespace LT.Contracts.Helpers;

/// <summary>
/// Gamma exposure (GEX): per strike, gamma × open interest × multiplier × spot² × 1%, in dollars
/// per 1% move of the underlying, with dealers taken as long the calls (positive) and short the
/// puts (negative), the usual convention. The gamma flip is where that sum, worked out again at
/// other spot prices with Black-Scholes gamma (IB's implied volatility, no rates or dividends),
/// changes sign.
/// </summary>
public static partial class GammaExposureHelpers
{
	[GeneratedRegex(@"^\d{8}$")]
	public static partial Regex ExpirationRegex();

	/// <summary>
	/// Option lines a reading may hold at once with <paramref name="activeLines"/> in use: what is
	/// left above <see cref="GammaExposureConstants.ReservedLines"/>, at most
	/// <see cref="GammaExposureConstants.MaxConcurrentLines"/>; 0 when nothing is left.
	/// </summary>
	public static int GetLineBudget(int activeLines) =>
		Math.Clamp(
			MarketDataConstants.MaxLines - activeLines - GammaExposureConstants.ReservedLines,
			0,
			GammaExposureConstants.MaxConcurrentLines
		);

	/// <summary>Every call and put of the strikes, by strike, call first.</summary>
	public static IReadOnlyList<ContractDto> GetOptionContracts(
		IReadOnlyList<OptionStrikeDto> strikes
	) => [.. strikes.SelectMany(x => new[] { x.Call, x.Put }).OfType<ContractDto>()];

	/// <summary>The option's own open interest: tick 27 for a call, 28 for a put.</summary>
	public static decimal? GetOpenInterest(QuoteDto quote, string right) =>
		right == MarketDataConstants.CallRight ? quote.CallOpenInterest : quote.PutOpenInterest;

	/// <summary>The quote has IB's model gamma and the option's open interest.</summary>
	public static bool IsComplete(QuoteDto quote, string right) =>
		quote.ModelGreeks?.Gamma != null && GetOpenInterest(quote, right) != null;

	public static GammaExposureOptionDto ToOption(ContractDto option, QuoteDto? quote) =>
		new()
		{
			Strike = option.Strike,
			Right = option.Right,
			Gamma = quote?.ModelGreeks?.Gamma,
			ImpliedVolatility = quote?.ModelGreeks?.ImpliedVolatility,
			OpenInterest = quote == null ? null : GetOpenInterest(quote, option.Right),
		};

	/// <summary>
	/// Streams the option (with <see cref="MarketDataConstants.OpenInterestGenericTick"/>) until
	/// its gamma and open interest came, or for <paramref name="timeout"/> at most, then gives the
	/// line back. What came by then is returned; a refused line throws.
	/// </summary>
	public static async Task<GammaExposureOptionDto> ReadOptionAsync(
		IMarketDataManager marketDataManager,
		ContractDto option,
		TimeSpan timeout,
		TimeProvider timeProvider,
		CancellationToken cancellationToken
	)
	{
		QuoteDto? latest = null;
		using var deadline = new CancellationTokenSource(timeout, timeProvider);
		using var read = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			deadline.Token
		);
		try
		{
			await foreach (
				var quote in marketDataManager.SubscribeQuotesAsync(
					option,
					read.Token,
					MarketDataConstants.OpenInterestGenericTick,
					MarketDataStreamOwners.GammaExposure
				)
			)
			{
				latest = quote;
				if (IsComplete(quote, option.Right))
					break;
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// out of time: read it as it is
		}
		return ToOption(option, latest);
	}

	/// <summary>The contract's multiplier, or <see cref="GammaExposureConstants.DefaultMultiplier"/>.</summary>
	public static double GetMultiplier(ContractDto? option) =>
		double.TryParse(
			option?.Multiplier,
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var multiplier
		)
		&& multiplier > 0
			? multiplier
			: GammaExposureConstants.DefaultMultiplier;

	/// <summary>Dollars per 1% move: gamma × open interest × multiplier × spot² × 1%.</summary>
	public static double GetExposure(
		double gamma,
		decimal openInterest,
		double multiplier,
		double spot
	) =>
		gamma
		* (double)openInterest
		* multiplier
		* spot
		* spot
		* GammaExposureConstants.OnePercentMove;

	/// <summary>Options that count: both a gamma and an open interest.</summary>
	public static bool Counts(GammaExposureOptionDto option) =>
		option.Gamma != null && option.OpenInterest != null;

	/// <summary>
	/// Exposure per strike from the options that count, ascending by strike; calls positive, puts
	/// negative.
	/// </summary>
	public static IReadOnlyList<GammaExposureStrikeDto> GetStrikeExposures(
		IEnumerable<GammaExposureOptionDto> options,
		double spot,
		double multiplier
	) =>
		[
			.. options
				.Where(Counts)
				.GroupBy(x => x.Strike)
				.OrderBy(x => x.Key)
				.Select(strike =>
				{
					var result = new GammaExposureStrikeDto { Strike = strike.Key };
					foreach (var option in strike)
					{
						var exposure = GetExposure(
							option.Gamma!.Value,
							option.OpenInterest!.Value,
							multiplier,
							spot
						);
						if (option.Right == MarketDataConstants.CallRight)
						{
							result.CallOpenInterest += option.OpenInterest.Value;
							result.CallExposure += exposure;
						}
						else
						{
							result.PutOpenInterest += option.OpenInterest.Value;
							result.PutExposure -= exposure;
						}
					}
					return result;
				}),
		];

	/// <summary>
	/// Black-Scholes gamma (no rates, no dividends) per 1.00 move; 0 for an input it can't use.
	/// </summary>
	public static double BlackScholesGamma(
		double spot,
		double strike,
		double volatility,
		double years
	)
	{
		if (spot <= 0 || strike <= 0 || volatility <= 0 || years <= 0)
			return 0;
		var deviation = volatility * Math.Sqrt(years);
		var d1 = (Math.Log(spot / strike) + 0.5 * volatility * volatility * years) / deviation;
		return Math.Exp(-0.5 * d1 * d1) / Math.Sqrt(2 * Math.PI) / (spot * deviation);
	}

	/// <summary>Net exposure if the underlying stood at <paramref name="spot"/>, by Black-Scholes gamma.</summary>
	public static double GetNetExposureAt(
		IEnumerable<GammaExposureOptionDto> options,
		double spot,
		double multiplier,
		double years
	)
	{
		var total = 0.0;
		foreach (var option in options)
		{
			if (option is not { OpenInterest: { } openInterest, ImpliedVolatility: { } volatility })
				continue;
			var exposure = GetExposure(
				BlackScholesGamma(spot, option.Strike, volatility, years),
				openInterest,
				multiplier,
				spot
			);
			total += option.Right == MarketDataConstants.CallRight ? exposure : -exposure;
		}
		return total;
	}

	/// <summary>
	/// The gamma flip: of the spot prices between the lowest and highest strike where the net
	/// exposure (<see cref="GetNetExposureAt"/>) changes sign, the one nearest
	/// <paramref name="spot"/>, interpolated between the two prices tried around it. Null when it
	/// doesn't change sign there, or no option has a volatility and open interest.
	/// </summary>
	public static double? FindGammaFlip(
		IReadOnlyList<GammaExposureOptionDto> options,
		double spot,
		double multiplier,
		double years
	)
	{
		var usable = options
			.Where(x => x is { OpenInterest: > 0, ImpliedVolatility: > 0 })
			.ToList();
		if (usable.Count == 0)
			return null;
		years = Math.Max(years, GammaExposureConstants.MinYearsToExpiration);
		var low = usable.Min(x => x.Strike);
		var high = usable.Max(x => x.Strike);
		if (high <= low)
			return null;

		var step = (high - low) / GammaExposureConstants.FlipSearchSteps;
		double? best = null;
		var previousPrice = low;
		var previous = GetNetExposureAt(usable, low, multiplier, years);
		for (var i = 1; i <= GammaExposureConstants.FlipSearchSteps; i++)
		{
			var price = low + i * step;
			var value = GetNetExposureAt(usable, price, multiplier, years);
			if (previous != 0 && Math.Sign(value) != Math.Sign(previous))
			{
				var flip = previousPrice + (price - previousPrice) * previous / (previous - value);
				if (best is not { } b || Math.Abs(flip - spot) < Math.Abs(b - spot))
					best = flip;
			}
			previous = value;
			previousPrice = price;
		}
		return best;
	}

	/// <summary>The reading's result from what its options brought.</summary>
	public static GammaExposureDto Build(
		ContractDto underlying,
		string expiration,
		double spot,
		IReadOnlyList<GammaExposureOptionDto> options,
		double multiplier,
		DateTimeOffset now
	)
	{
		var strikes = GetStrikeExposures(options, spot, multiplier);
		var years = OptionChainHelpers.GetExpirationTime(expiration) is { } close
			? OptionChainHelpers.GetYearsToExpiration(close, now)
			: 0;
		return new GammaExposureDto
		{
			Underlying = underlying,
			Expiration = expiration,
			SpotPrice = spot,
			Strikes = strikes,
			CallExposure = strikes.Sum(x => x.CallExposure),
			PutExposure = strikes.Sum(x => x.PutExposure),
			GammaFlip = FindGammaFlip([.. options.Where(Counts)], spot, multiplier, years),
			OptionsRequested = options.Count,
			OptionsReceived = options.Count(Counts),
			ReadAt = now,
		};
	}

	/// <summary>Exposure label: $1.25B, $340M, -$12.5K.</summary>
	public static string FormatExposure(double value)
	{
		var abs = Math.Abs(value);
		var sign = value < 0 ? "-" : string.Empty;
		var text = abs switch
		{
			>= 1e9 => (abs / 1e9).ToString("0.##", CultureInfo.CurrentCulture) + "B",
			>= 1e6 => (abs / 1e6).ToString("0.##", CultureInfo.CurrentCulture) + "M",
			>= 1e3 => (abs / 1e3).ToString("0.#", CultureInfo.CurrentCulture) + "K",
			_ => abs.ToString("0", CultureInfo.CurrentCulture),
		};
		return $"{sign}${text}";
	}
}
