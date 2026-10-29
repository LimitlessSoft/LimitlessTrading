using System.Globalization;
using System.Text.RegularExpressions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.MarketData;
using LT.Contracts.Dtos.Options;
using LT.Contracts.Dtos.Orders;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Requests.Options;
using LT.Contracts.Requests.Orders;

namespace LT.Contracts.Helpers;

public static partial class OptionChainHelpers
{
	[GeneratedRegex(@"^\d{6}(\d{2})?$")]
	public static partial Regex ExpiryRegex();

	public static string GetExpirationsCacheKey(ContractDto underlying) =>
		$"expirations|{underlying.ConId}";

	public static string GetStrikesCacheKey(GetOptionChainRequest request) =>
		$"strikes|{request.Underlying.Symbol}|{request.Underlying.Currency}|{request.Expiry}|{request.TradingClass}";

	/// <summary>
	/// The option chain's tabs from reqSecDefOptParams: SMART entries of the symbol's own trading
	/// class and its weeklies (SPX + SPXW), skipping adjusted classes such as 2SPY; every SMART
	/// entry when none matches. A date both classes share keeps the symbol's own class.
	/// </summary>
	public static IReadOnlyList<OptionExpirationDto> SelectExpirations(
		string symbol,
		IEnumerable<OptionChainParametersDto> parameters
	)
	{
		var smart = parameters
			.Where(x => x.Exchange == MarketDataConstants.DefaultExchange)
			.ToList();
		var own = smart
			.Where(x =>
				x.TradingClass == symbol
				|| x.TradingClass == symbol + MarketDataConstants.WeeklyTradingClassSuffix
			)
			.ToList();
		return (own.Count > 0 ? own : smart)
			.OrderBy(x => x.TradingClass != symbol)
			.SelectMany(x =>
				x.Expirations.Select(e => new OptionExpirationDto
				{
					Expiration = e,
					TradingClass = x.TradingClass,
				})
			)
			.DistinctBy(x => x.Expiration)
			.OrderBy(x => x.Expiration)
			.ToList();
	}

	/// <summary>Today's date where options expire (New York), the day expirations are judged by.</summary>
	public static DateOnly GetExchangeDate(DateTimeOffset now) =>
		DateOnly.FromDateTime(
			TimeZoneInfo
				.ConvertTime(
					now,
					IbTimeHelpers.FindTimeZone(MarketDataConstants.OptionExpirationTimeZone)
				)
				.DateTime
		);

	/// <summary>
	/// Whether an expiry is over by <paramref name="today"/> (the exchange date): a yyyyMMdd date
	/// before today, a yyyyMM month before this month. An expiry in neither form is kept.
	/// </summary>
	public static bool IsExpired(string expiry, DateOnly today)
	{
		if (
			DateOnly.TryParseExact(
				expiry,
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var date
			)
		)
			return date < today;
		if (
			DateOnly.TryParseExact(
				expiry,
				"yyyyMM",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var month
			)
		)
			return month < new DateOnly(today.Year, today.Month, 1);
		return false;
	}

	/// <summary>
	/// The expirations not yet over at <paramref name="now"/> (see <see cref="IsExpired"/>): a list
	/// fetched or cached on an earlier day still holds the days since passed.
	/// </summary>
	public static IReadOnlyList<OptionExpirationDto> RemoveExpired(
		IEnumerable<OptionExpirationDto> expirations,
		DateTimeOffset now
	)
	{
		var today = GetExchangeDate(now);
		return expirations.Where(x => !IsExpired(x.Expiration, today)).ToList();
	}

	/// <summary>
	/// How old a cached expiration or strike list may be at <paramref name="now"/>: written since
	/// the last New York midnight (so an entry lasts until the next one, when an expiration may
	/// have passed), and never more than <see cref="CacheConstants.OptionChainTtl"/>.
	/// </summary>
	public static TimeSpan GetCacheTtl(DateTimeOffset now)
	{
		var zone = IbTimeHelpers.FindTimeZone(MarketDataConstants.OptionExpirationTimeZone);
		var midnight = TimeZoneInfo.ConvertTime(now, zone).Date;
		var sinceMidnight = now - new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
		return sinceMidnight < CacheConstants.OptionChainTtl
			? sinceMidnight
			: CacheConstants.OptionChainTtl;
	}

	/// <summary>Pairs calls and puts by expiration and strike.</summary>
	public static IReadOnlyList<OptionStrikeDto> BuildStrikes(IEnumerable<ContractDto> options) =>
		options
			.Where(x => x.SecurityType == MarketDataConstants.OptionType)
			.GroupBy(x => (x.LastTradeDateOrContractMonth, x.Strike))
			.Select(g => new OptionStrikeDto
			{
				Expiration = g.Key.LastTradeDateOrContractMonth,
				Strike = g.Key.Strike,
				Call = g.FirstOrDefault(x => x.Right == MarketDataConstants.CallRight),
				Put = g.FirstOrDefault(x => x.Right == MarketDataConstants.PutRight),
			})
			.OrderBy(x => x.Expiration)
			.ThenBy(x => x.Strike)
			.ToList();

	/// <summary>
	/// The <paramref name="count"/> strikes on each side of the price (the whole list centred on
	/// the middle strike when the price is unknown).
	/// </summary>
	public static IReadOnlyList<OptionStrikeDto> AroundPrice(
		IReadOnlyList<OptionStrikeDto> strikes,
		double? price,
		int count
	)
	{
		if (strikes.Count == 0)
			return [];

		var center = price is { } p
			? strikes
				.Select((x, i) => (Distance: Math.Abs(x.Strike - p), Index: i))
				.MinBy(x => x.Distance)
				.Index
			: strikes.Count / 2;
		var (from, to) = GetWindow(strikes.Count, center, count);
		return strikes.Skip(from).Take(to - from + 1).ToList();
	}

	/// <summary>
	/// Index range (inclusive) of the <paramref name="count"/> strikes on each side of
	/// <paramref name="center"/>, clamped to the list.
	/// </summary>
	public static (int From, int To) GetWindow(int strikeCount, int center, int count) =>
		(Math.Max(0, center - count), Math.Min(strikeCount - 1, center + count));

	/// <summary>
	/// Index of the strike nearest <paramref name="underlyingPrice"/> (the lower one on a tie);
	/// null when the price is unknown (null or not positive) or there are no strikes.
	/// </summary>
	public static int? GetNearestStrikeIndex(
		IReadOnlyList<OptionStrikeDto> strikes,
		double? underlyingPrice
	)
	{
		if (strikes.Count == 0 || underlyingPrice is not { } price || price <= 0)
			return null;
		return strikes
			.Select((x, i) => (Distance: Math.Abs(x.Strike - price), Index: i))
			.MinBy(x => x.Distance)
			.Index;
	}

	/// <summary>
	/// The centre a strike window around <paramref name="center"/> should move to for
	/// <paramref name="underlyingPrice"/>: the ATM index once it is more than
	/// <paramref name="threshold"/> strikes off centre and the move changes the window; null
	/// otherwise (also when the price is unknown).
	/// </summary>
	public static int? GetRecenterTarget(
		IReadOnlyList<OptionStrikeDto> strikes,
		int center,
		double? underlyingPrice,
		int count,
		int threshold
	)
	{
		if (GetNearestStrikeIndex(strikes, underlyingPrice) is not { } atm)
			return null;
		if (Math.Abs(atm - center) <= threshold)
			return null;
		return GetWindow(strikes.Count, atm, count) == GetWindow(strikes.Count, center, count)
			? null
			: atm;
	}

	/// <summary>
	/// Whether the option is in the money at <paramref name="underlyingPrice"/>: a call when its
	/// strike is below the price, a put when above. False when the price is unknown (null or not
	/// positive), so nothing is shaded rather than shaded wrongly.
	/// </summary>
	public static bool IsInTheMoney(double strike, string right, double? underlyingPrice) =>
		underlyingPrice is { } price
		&& price > 0
		&& right switch
		{
			MarketDataConstants.CallRight => strike < price,
			MarketDataConstants.PutRight => strike > price,
			_ => false,
		};

	/// <summary>
	/// The at-the-money strike: the one nearest <paramref name="underlyingPrice"/> (the lower one
	/// on a tie); null when the price is unknown or there are no strikes.
	/// </summary>
	public static double? GetAtmStrike(
		IReadOnlyList<OptionStrikeDto> strikes,
		double? underlyingPrice
	) => GetNearestStrikeIndex(strikes, underlyingPrice) is { } i ? strikes[i].Strike : null;

	/// <summary>
	/// One-click trading from the chain: the ask buys, the bid sells (calls and puts alike); other
	/// columns don't trade.
	/// </summary>
	public static OrderAction? GetQuickOrderAction(OptionQuoteColumn column) =>
		column switch
		{
			OptionQuoteColumn.Ask => OrderAction.Buy,
			OptionQuoteColumn.Bid => OrderAction.Sell,
			_ => null,
		};

	/// <summary>
	/// Whether a cell can be clicked to trade: a bid / ask column whose side has a contract and a
	/// price in that column (nothing to hit otherwise).
	/// </summary>
	public static bool CanQuickTrade(
		OptionStrikeDto strike,
		string right,
		OptionQuoteColumn column,
		QuoteDto? quote
	) =>
		GetOption(strike, right) != null
		&& column switch
		{
			OptionQuoteColumn.Ask => quote?.Ask != null,
			OptionQuoteColumn.Bid => quote?.Bid != null,
			_ => false,
		};

	/// <summary>
	/// Whether <paramref name="quantity"/> may be sent with one click: a whole number of contracts
	/// from 1 to <see cref="OrderConstants.QuickOrderQuantityMax"/> (empty input never trades).
	/// </summary>
	public static bool IsValidQuickOrderQuantity(decimal? quantity) =>
		quantity is { } q
		&& q >= 1
		&& q <= OrderConstants.QuickOrderQuantityMax
		&& q == decimal.Truncate(q);

	/// <summary>
	/// The quote price a one-click order on <paramref name="column"/> would hit: the ask for a buy,
	/// the bid for a sell; null for other columns or while the quote has none.
	/// </summary>
	public static double? GetQuickOrderPrice(QuoteDto? quote, OptionQuoteColumn column) =>
		column switch
		{
			OptionQuoteColumn.Ask => quote?.Ask,
			OptionQuoteColumn.Bid => quote?.Bid,
			_ => null,
		};

	/// <summary>
	/// What a one-click order would cost (or bring in): quantity × price × contract multiplier.
	/// An estimate, since the order goes at market. Null when the quantity isn't a valid one-click
	/// quantity, or the price or multiplier is unknown.
	/// </summary>
	public static double? GetQuickOrderCost(decimal? quantity, double? price, double? multiplier) =>
		IsValidQuickOrderQuantity(quantity) && price is { } p && multiplier is { } m
			? (double)quantity!.Value * p * m
			: null;

	/// <summary>Why a one-click quantity can't be sent; null when it can.</summary>
	public static string? GetQuickOrderQuantityError(decimal? quantity) =>
		IsValidQuickOrderQuantity(quantity)
			? null
			: OrderValidationCodes.OVC_012.GetValidationMessage(
				1,
				OrderConstants.QuickOrderQuantityMax
			);

	/// <summary>
	/// A saved one-click quantity, or the default when there is none or it is out of bounds (an
	/// edited layout file).
	/// </summary>
	public static int GetQuickOrderQuantityOrDefault(int? saved) =>
		saved is { } q && IsValidQuickOrderQuantity(q)
			? q
			: OrderConstants.QuickOrderQuantityDefault;

	/// <summary>
	/// The one-click order for a chain cell: <paramref name="quantity"/> contracts at market, DAY,
	/// on <paramref name="account"/>; null when the column doesn't trade, the side has no contract
	/// or the quantity isn't a valid one-click quantity (<see cref="IsValidQuickOrderQuantity"/>).
	/// The manager still validates it (account, contract id).
	/// </summary>
	public static PlaceOrderRequest? BuildQuickOrder(
		OptionStrikeDto strike,
		string right,
		OptionQuoteColumn column,
		string? account,
		decimal? quantity
	)
	{
		if (
			GetQuickOrderAction(column) is not { } action
			|| GetOption(strike, right) is not { } option
			|| !IsValidQuickOrderQuantity(quantity)
		)
			return null;
		return new PlaceOrderRequest
		{
			Contract = option,
			Account = account ?? string.Empty,
			Action = action,
			OrderType = OrderType.Market,
			Quantity = quantity,
			TimeInForce = TimeInForce.Day,
		};
	}

	/// <summary>Identifies a chain cell for the repeat-click guard: the contract and the side.</summary>
	public static string GetQuickOrderKey(PlaceOrderRequest request) =>
		$"{request.Contract?.ConId}|{request.Contract?.LocalSymbol}|{request.Action}";

	/// <summary>Whether a click lands within the repeat window of the last one on the same cell.</summary>
	public static bool IsRepeatClick(DateTimeOffset? lastClick, DateTimeOffset now) =>
		lastClick is { } last && now - last < OrderConstants.QuickOrderRepeatWindow;

	/// <summary>The strike's call or put, by right ("C" / "P").</summary>
	public static ContractDto? GetOption(OptionStrikeDto strike, string right) =>
		right switch
		{
			MarketDataConstants.CallRight => strike.Call,
			MarketDataConstants.PutRight => strike.Put,
			_ => null,
		};

	/// <summary>
	/// When an expiry date (yyyyMMdd) expires: 16:00 New York that day. Null for another format
	/// (a yyyyMM month has no day) or when the system lacks the New York time zone.
	/// </summary>
	public static DateTimeOffset? GetExpirationTime(string expiry)
	{
		if (
			!DateTime.TryParseExact(
				expiry,
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var date
			)
		)
			return null;
		if (
			!TimeZoneInfo.TryFindSystemTimeZoneById(
				MarketDataConstants.OptionExpirationTimeZone,
				out var zone
			)
		)
			return null;
		var close = date.Add(MarketDataConstants.OptionExpirationClose);
		return new DateTimeOffset(close, zone.GetUtcOffset(close));
	}

	/// <summary>Calendar time from <paramref name="now"/> to the expiration, in years (0 once past).</summary>
	public static double GetYearsToExpiration(DateTimeOffset expiration, DateTimeOffset now) =>
		Math.Max(0, (expiration - now).TotalDays / MarketDataConstants.DaysPerYear);

	/// <summary>
	/// One standard deviation of the expected move to expiration, in price:
	/// price × IV × √T. Null when the price or IV is unknown, or no time is left (the bands would
	/// collapse onto the price).
	/// </summary>
	public static double? GetExpectedMove(
		double? underlyingPrice,
		double? impliedVolatility,
		double yearsToExpiration
	) =>
		underlyingPrice is { } price
		&& price > 0
		&& impliedVolatility is { } iv
		&& iv > 0
		&& yearsToExpiration > 0
			? price * iv * Math.Sqrt(yearsToExpiration)
			: null;

	/// <summary>Signed distance of the strike from the price in standard deviations; null when unknown.</summary>
	public static double? GetSigmaDistance(double strike, double? underlyingPrice, double? sigma) =>
		underlyingPrice is { } price && price > 0 && sigma is { } s && s > 0
			? (strike - price) / s
			: null;

	/// <summary>The band of a σ distance: within 1, 2, 3 σ (inclusive), or beyond.</summary>
	public static SigmaBand GetSigmaBand(double? sigmaDistance) =>
		sigmaDistance is not { } d ? SigmaBand.None
		: Math.Abs(d) <= 1 ? SigmaBand.WithinOne
		: Math.Abs(d) <= 2 ? SigmaBand.WithinTwo
		: Math.Abs(d) <= 3 ? SigmaBand.WithinThree
		: SigmaBand.BeyondThree;

	/// <summary>"+1.4σ" / "−0.3σ"; null when unknown.</summary>
	public static string? FormatSigma(double? sigmaDistance) =>
		sigmaDistance is { } d
			? d.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "σ"
			: null;

	/// <summary>
	/// The order ticket a click on the strike opens for its call or put (<paramref name="right"/>
	/// "C" / "P"): buy <paramref name="quantity"/> contracts (the one-click quantity; 1 when that
	/// isn't valid) on <paramref name="account"/>, market, with the limit box set to
	/// <see cref="GetMidPrice"/>. Null when the strike has no such option. Nothing is sent: the
	/// user picks market or limit, reviews and sends from the ticket.
	/// </summary>
	public static OrderDraftDto? GetOrderDraft(
		OptionStrikeDto strike,
		string right,
		string? account,
		decimal? quantity,
		QuoteDto? quote
	)
	{
		if (GetOption(strike, right) is not { } option)
			return null;
		return new OrderDraftDto
		{
			Contract = option,
			Account = account ?? string.Empty,
			Action = OrderAction.Buy,
			Quantity = IsValidQuickOrderQuantity(quantity)
				? quantity!.Value
				: OrderConstants.QuickOrderQuantityDefault,
			LimitPrice = GetMidPrice(quote),
			Note =
				$"{ContractHelpers.GetDisplayName(option)} from the option chain. Pick market or limit, review, then send.",
		};
	}

	/// <summary>
	/// A starting limit price for an option: the bid / ask mid, else the last price, rounded to
	/// the cent; null with no positive price. Only a suggestion the user reviews (some options
	/// trade in 0.05 steps).
	/// </summary>
	public static decimal? GetMidPrice(QuoteDto? quote)
	{
		if (quote?.Bid is { } bid && quote.Ask is { } ask && bid > 0 && ask >= bid)
			return Math.Round(((decimal)bid + (decimal)ask) / 2, 2, MidpointRounding.AwayFromZero);
		return quote?.Last is { } last && last > 0
			? Math.Round((decimal)last, 2, MidpointRounding.AwayFromZero)
			: null;
	}

	/// <summary>
	/// The quote columns in the order the call side shows them (the put side mirrors it), as the
	/// column menu lists them.
	/// </summary>
	public static IReadOnlyList<OptionQuoteColumn> QuoteColumns { get; } =
	[
		OptionQuoteColumn.Volume,
		OptionQuoteColumn.Last,
		OptionQuoteColumn.Ask,
		OptionQuoteColumn.Bid,
	];

	/// <summary>A quote column's name in the column menu.</summary>
	public static string GetColumnName(OptionQuoteColumn column) =>
		column switch
		{
			OptionQuoteColumn.Volume => "Vol",
			OptionQuoteColumn.Last => "Last",
			OptionQuoteColumn.Ask => "Ask",
			OptionQuoteColumn.Bid => "Bid",
			_ => column.ToString(),
		};

	/// <summary>Saved hidden columns as a set, without unknown values (an edited layout file).</summary>
	public static HashSet<OptionQuoteColumn> ResolveHiddenColumns(
		IEnumerable<OptionQuoteColumn>? saved
	) => [.. (saved ?? []).Where(QuoteColumns.Contains)];

	/// <summary>Hidden columns as saved: in menu order, null when none is hidden.</summary>
	public static List<OptionQuoteColumn>? ToSavedHiddenColumns(
		IReadOnlySet<OptionQuoteColumn> hidden
	) => hidden.Count > 0 ? [.. QuoteColumns.Where(hidden.Contains)] : null;
}
