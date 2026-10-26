using LT.Contracts.Interfaces.IProcesses;

namespace LT.Tests.Fakes;

/// <summary>Counts the sounds asked for instead of playing them.</summary>
public class FakeSoundPlayer : ISoundPlayer
{
	public int FillsPlayed { get; private set; }
	public Exception? Error { get; set; }

	public void PlayFill()
	{
		if (Error != null)
			throw Error;
		FillsPlayed++;
	}
}
