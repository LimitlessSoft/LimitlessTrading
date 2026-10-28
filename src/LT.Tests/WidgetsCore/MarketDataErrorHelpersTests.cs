using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Exceptions;

namespace LT.Tests.WidgetsCore;

public class MarketDataErrorHelpersTests
{
	[Fact]
	public void No_free_line_is_the_apps_line_limit_or_ibs_101()
	{
		Assert.True(
			MarketDataErrorHelpers.IsNoFreeLine(
				new LSCoreBadRequestException(
					MarketDataValidationCodes.MDVC_001.GetValidationMessage(
						MarketDataConstants.MaxLines
					)
				)
			)
		);
		Assert.True(
			MarketDataErrorHelpers.IsNoFreeLine(
				new IbkrRequestException(101, "Max number of tickers has been reached")
			)
		);
		Assert.False(
			MarketDataErrorHelpers.IsNoFreeLine(new IbkrRequestException(354, "Not subscribed."))
		);
		Assert.False(
			MarketDataErrorHelpers.IsNoFreeLine(
				new LSCoreBadRequestException(
					MarketDataValidationCodes.MDVC_002.GetValidationMessage()
				)
			)
		);
	}

	[Fact]
	public void The_snapshot_limit_is_recognised()
	{
		Assert.True(
			MarketDataErrorHelpers.IsSnapshotLimit(
				new LSCoreBadRequestException(
					MarketDataValidationCodes.MDVC_004.GetValidationMessage(
						MarketDataConstants.MaxConcurrentSnapshots
					)
				)
			)
		);
		Assert.False(MarketDataErrorHelpers.IsSnapshotLimit(new TimeoutException()));
	}
}
