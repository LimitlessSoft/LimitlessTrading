using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Requests.Gateway;

namespace LT.Contracts.Interfaces.IManagers;

public interface IGatewayManager
{
	GatewayStatusDto Status { get; }

	/// <summary>Raised on any thread whenever <see cref="Status"/> changes.</summary>
	event Action<GatewayStatusDto>? StatusChanged;

	/// <summary>Last used Gateway install.</summary>
	StartGatewayRequest GetLastStartRequest();

	/// <summary>
	/// Starts IB Gateway, waits while the user logs in in its own window (live or paper, incl.
	/// 2FA) and connects the API to whichever port opens. Failures end in
	/// <see cref="Enums.GatewayState.Failed"/>, not exceptions; the Gateway closed before the login
	/// finished ends in <see cref="Enums.GatewayState.Stopped"/>.
	/// </summary>
	Task StartAsync(StartGatewayRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Uses a Gateway that is already running on the live or paper API port (one left running by
	/// an earlier session; the last used mode is tried first): marks it running (external) and
	/// connects to it. False when nothing listens there.
	/// </summary>
	Task<bool> AttachRunningAsync(CancellationToken cancellationToken = default);

	/// <summary>Disconnects; also closes the Gateway when this app run started it.</summary>
	Task StopAsync();

	/// <summary>
	/// Looks for IB Gateway in the usual places. Touches the disk — call off the UI thread.
	/// </summary>
	GatewayInstallDto LocateInstall();

	/// <summary>Folder the user picked → Gateway root + versions; throws when none is installed.</summary>
	GatewayInstallDto ResolveGatewayPath(string selected);
}
