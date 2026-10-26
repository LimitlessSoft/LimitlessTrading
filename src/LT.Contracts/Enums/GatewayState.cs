namespace LT.Contracts.Enums;

public enum GatewayState
{
	Stopped = 0,
	Starting = 1,

	/// <summary>Gateway's window is up; the user logs in there (credentials, mode, 2FA).</summary>
	AwaitingLogin = 2,
	Running = 3,
	Stopping = 4,
	Failed = 5,
}
