namespace LT.Contracts.Enums;

public enum GatewayState
{
	Stopped = 0,
	Starting = 1,
	AwaitingSecondFactor = 2,
	Running = 3,
	Stopping = 4,
	Failed = 5,
}
