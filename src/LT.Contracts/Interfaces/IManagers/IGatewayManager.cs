using LT.Contracts.Dtos.Gateway;
using LT.Contracts.Requests.Gateway;

namespace LT.Contracts.Interfaces.IManagers;

public interface IGatewayManager
{
	GatewayStatusDto Status { get; }

	/// <summary>Raised on any thread whenever <see cref="Status"/> changes.</summary>
	event Action<GatewayStatusDto>? StatusChanged;

	/// <summary>Last used settings; <see cref="StartGatewayRequest.Password"/> is always empty.</summary>
	StartGatewayRequest GetLastStartRequest();

	/// <summary>
	/// Starts IB Gateway through IBC, waits for login (incl. 2FA) and connects the API to it.
	/// Failures end in <see cref="Enums.GatewayState.Failed"/>, not exceptions.
	/// </summary>
	Task StartAsync(StartGatewayRequest request, CancellationToken cancellationToken = default);

	Task StopAsync();

	/// <summary>
	/// Looks for IBC and Gateway in the usual places, then a bounded part of home / Downloads.
	/// Touches the disk — call off the UI thread.
	/// </summary>
	GatewayInstallDto LocateInstall();

	/// <summary>
	/// Downloads IBC's latest release for this OS from GitHub into the app-data folder and returns
	/// the IBC folder. Replaces an earlier app-installed copy.
	/// </summary>
	Task<string> InstallIbcAsync(CancellationToken cancellationToken = default);

	/// <summary>Folder the user picked → IBC folder; throws when there's no IBC start script.</summary>
	string ResolveIbcPath(string selected);

	/// <summary>Folder the user picked → Gateway root + versions; throws when none is installed.</summary>
	GatewayInstallDto ResolveGatewayPath(string selected);
}
