using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;

namespace LT.App.Helpers;

/// <summary>Market data errors widgets react to rather than show.</summary>
public static class MarketDataErrorHelpers
{
	/// <summary>IB's error when every market data line of the account is taken.</summary>
	public const int IbNoFreeLineCode = 101;

	/// <summary>
	/// No line was free for a quote stream: the app's own limit
	/// (<c>MarketDataConstants.MaxLines</c>) or IB's "Max number of tickers has been reached" (101).
	/// </summary>
	public static bool IsNoFreeLine(Exception exception) =>
		exception switch
		{
			IbkrRequestException ib => ib.Code == IbNoFreeLineCode,
			LSCoreBadRequestException => exception.Message
				== MarketDataValidationCodes.MDVC_001.GetValidationMessage(
					MarketDataConstants.MaxLines
				),
			_ => false,
		};

	/// <summary>Too many snapshots app-wide right now (<c>MarketDataConstants.MaxConcurrentSnapshots</c>).</summary>
	public static bool IsSnapshotLimit(Exception exception) =>
		exception is LSCoreBadRequestException
		&& exception.Message
			== MarketDataValidationCodes.MDVC_004.GetValidationMessage(
				MarketDataConstants.MaxConcurrentSnapshots
			);
}
