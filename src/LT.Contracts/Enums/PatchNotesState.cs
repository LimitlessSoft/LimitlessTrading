namespace LT.Contracts.Enums;

/// <summary>Where the patch notes' refresh from GitHub stands. Append only.</summary>
public enum PatchNotesState
{
	/// <summary>Not fetched in this session yet (cached notes, if any, are shown).</summary>
	Idle,
	Loading,
	Loaded,

	/// <summary>The last refresh failed; cached notes, if any, are still shown.</summary>
	Failed,
}
