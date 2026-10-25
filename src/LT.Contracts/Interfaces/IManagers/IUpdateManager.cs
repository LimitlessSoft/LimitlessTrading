using LT.Contracts.Dtos.Updates;

namespace LT.Contracts.Interfaces.IManagers;

/// <summary>The app's own updates: check GitHub, download, swap in and restart. A singleton.</summary>
public interface IUpdateManager
{
	UpdateStatusDto Status { get; }

	/// <summary>Raised on any thread whenever <see cref="Status"/> changes.</summary>
	event Action<UpdateStatusDto>? StatusChanged;

	/// <summary>Looks for a newer release; failures end in Failed, not exceptions.</summary>
	Task CheckAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Downloads the newer release and swaps it in; ends in Restarting (the app must now close
	/// itself) or Failed.
	/// </summary>
	Task InstallAsync(CancellationToken cancellationToken = default);
}
