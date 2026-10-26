using LT.Contracts.Constants;

namespace LT.App.Widgets.Settings;

/// <summary>
/// The IB Flex setup as a numbered checklist (the same as docs/ibkr-flex.md → Setup). IB has no
/// API to create Flex queries or tokens, so the user does these steps in Client Portal once; each
/// value has a Copy button and the Client Portal steps open its Flex Queries page.
/// </summary>
public static class FlexSetupGuide
{
	private const string OpenFlexQueries = "Open Flex Queries in Client Portal";

	public static GuideSettingViewModel Create() =>
		new(
			"Set up once in Client Portal",
			[
				new GuideStepViewModel(
					1,
					"Create an Activity Flex Query: Flex Queries page → Activity Flex Query → +. Click each of these sections; a field list opens where you pick the options shown and Select All, then Save it. Set the options below under Delivery and General Configuration, then Continue. Save, and copy the Query ID shown next to it in the list.",
					[
						new("Query name", "LimitlessTrading activity"),
						new("Section", "Account Information"),
						new("Section", "Net Asset Value (NAV) in Base"),
						new(
							"Section (option Deposits & Withdrawals, level Detail)",
							"Cash Transactions"
						),
						new("Section (option Execution)", "Trades"),
						new("Section (optional, transfers between accounts)", "Transfers"),
						new("Format", "XML"),
						new("Period", "Last 365 Calendar Days"),
						new("Date Format", "yyyyMMdd"),
						new("Time Format", "HHmmss"),
						new("Date/Time Separator", "; (semi-colon)"),
						new("Display Account Alias in Place of Account ID", "No"),
						new("Include Offsetting Trade/Cancel Pairs?", "No"),
						new("Breakout by Day?", "No"),
					],
					OpenFlexQueries,
					FlexConstants.ClientPortalFlexQueriesUrl
				),
				new GuideStepViewModel(
					2,
					"Optional, for today's trades: Trade Confirmation Flex Query → +, section Trade Confirmations (option Executions, Select All fields), with the same date / time options. Save and copy its Query ID.",
					[
						new("Query name", "LimitlessTrading today"),
						new("Section", "Trade Confirmations"),
						new("Format", "XML"),
						new("Period", "Today"),
					],
					OpenFlexQueries,
					FlexConstants.ClientPortalFlexQueriesUrl
				),
				new GuideStepViewModel(
					3,
					"Turn on the Flex Web Service: on the Flex Queries page click the gear next to Flex Web Service Configuration, switch it on, Generate a New Token with the longest expiry and no IP address, and copy the token. (IB has no direct link to this panel.)",
					[],
					OpenFlexQueries,
					FlexConstants.ClientPortalFlexQueriesUrl
				),
				new GuideStepViewModel(
					4,
					"Paste the token and the query IDs below (a pasted query URL or \"Query ID: 123456\" works too) and click Save & import now.",
					[]
				),
			],
			keywords: "flex history import ib client portal activity statement trade confirmation token setup checklist"
		);
}
