namespace LT.Contracts.Enums;

/// <summary>Where the app's own update stands.</summary>
public enum UpdateState
{
	Idle = 0,
	Checking = 1,
	UpToDate = 2,
	Available = 3,
	Downloading = 4,
	Restarting = 5,
	Failed = 6,
}
