using LT.Contracts.Dtos.PatchNotes;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>
/// What changed in each released version, from GitHub Releases, cached on disk so it shows
/// offline. A singleton.
/// </summary>
public interface IPatchNotesManager
{
	/// <summary>Starts with the cached notes (Idle).</summary>
	PatchNotesStatusDto Status { get; }

	/// <summary>Raised on any thread whenever <see cref="Status"/> changes.</summary>
	event Action<PatchNotesStatusDto>? StatusChanged;

	/// <summary>Fetches the releases again; failures end in Failed (cached notes kept), not exceptions.</summary>
	Task RefreshAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// True once, on the first call after the app started on a version newer than the last one
	/// it ran; records the running version either way. False on the very first start, from source
	/// builds and on a downgrade.
	/// </summary>
	bool TakeIsNewVersion();
}
