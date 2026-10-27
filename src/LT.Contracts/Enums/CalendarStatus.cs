namespace LT.Contracts.Enums;

/// <summary>How a calendar load, or one source's part of it, ended.</summary>
public enum CalendarStatus
{
	/// <summary>Events loaded (possibly none in the range).</summary>
	Loaded = 0,

	/// <summary>IB has no Wall Street Horizon subscription for the account (error 10276).</summary>
	NotSubscribed = 1,

	/// <summary>The source answered with data the app could not read; nothing of it is shown.</summary>
	Unreadable = 2,

	/// <summary>The source could not be asked or refused (not connected, HTTP error, IB refusal).</summary>
	Failed = 3,

	/// <summary>Every calendar source is switched off in Settings.</summary>
	NoSources = 4,
}
