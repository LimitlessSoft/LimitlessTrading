namespace LT.Contracts.Constants;

/// <summary>
/// IB's Flex Web Service (version 3): account history the socket API doesn't have. See
/// docs/ibkr-flex.md.
/// </summary>
public static class FlexConstants
{
	/// <summary>Both endpoints live here; <c>GetStatement</c>'s URL also comes back from SendRequest.</summary>
	public const string BaseUrl =
		"https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/";
	public const string SendRequestPath = "SendRequest";
	public const string GetStatementPath = "GetStatement";

	/// <summary>Without <c>v=3</c> IB answers in version 2.</summary>
	public const int Version = 3;

	/// <summary>IB requires a User-Agent ("Technology/Version") for programmatic access.</summary>
	public const string UserAgentProduct = "LimitlessTrading";

	/// <summary>A statement URL IB returns is followed only on this host suffix (the token goes with it).</summary>
	public const string AllowedHostSuffix = ".interactivebrokers.com";

	/// <summary>The <c>fd</c> / <c>td</c> override (yyyyMMdd) spans at most this many days.</summary>
	public const int MaxDaysPerRequest = 365;

	/// <summary>IB: one request per second per token; kept a little apart.</summary>
	public static readonly TimeSpan RequestSpacing = TimeSpan.FromMilliseconds(1200);

	/// <summary>IB: ten requests per minute per token; one kept spare.</summary>
	public const int MaxRequestsPerMinute = 9;

	/// <summary>First wait for a statement IB is still generating, then grows by <see cref="PollBackoffFactor"/>.</summary>
	public static readonly TimeSpan PollInitialDelay = TimeSpan.FromSeconds(3);
	public static readonly TimeSpan PollMaxDelay = TimeSpan.FromSeconds(30);
	public const double PollBackoffFactor = 1.6;

	/// <summary>Wait after IB answers "too many requests" (1018).</summary>
	public static readonly TimeSpan TooManyRequestsDelay = TimeSpan.FromSeconds(20);

	/// <summary>Gives up on one statement (request + polling) after this long.</summary>
	public static readonly TimeSpan StatementTimeout = TimeSpan.FromMinutes(5);

	/// <summary>One HTTP call; a few years of trades are a few MB.</summary>
	public static readonly TimeSpan HttpTimeout = TimeSpan.FromMinutes(2);

	/// <summary>Trade Confirmation statement while the app runs (today's fills, minutes after the fill).</summary>
	public static readonly TimeSpan TradeConfirmInterval = TimeSpan.FromMinutes(5);

	/// <summary>After a failed Activity import (statement not ready yet, network) it is tried again this much later.</summary>
	public static readonly TimeSpan ActivityRetryInterval = TimeSpan.FromHours(1);

	/// <summary>The daily Activity import starts this many days before the last covered day (late corrections).</summary>
	public const int IncrementalOverlapDays = 7;

	/// <summary>Back-fill stops after this many yearly chunks even if IB keeps answering.</summary>
	public const int MaxBackfillChunks = 30;

	/// <summary>Flex report dates and times are US Eastern (IB's statement time zone).</summary>
	public const string StatementTimeZoneId = "America/New_York";

	/// <summary>Journal key of a Flex trade without an IB execution id: this prefix + tradeID.</summary>
	public const string TradeIdExecIdPrefix = "flex:";

	public const int TokenMinLength = 10;
	public const int TokenMaxLength = 64;
	public const int QueryIdMaxLength = 12;

	public const string ImportStateFileName = "flex-import.json";
}
