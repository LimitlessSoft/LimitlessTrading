namespace LT.Contracts.Interfaces.IProcesses;

/// <summary>
/// Plays the app's notification sounds through the system's own player. Implemented in LT.App.
/// </summary>
public interface ISoundPlayer
{
	/// <summary>
	/// Starts the fill sound and returns at once; never throws (no audio device or player: no
	/// sound).
	/// </summary>
	void PlayFill();
}
