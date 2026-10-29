namespace LT.Contracts.Constants;

public static class SoundConstants
{
	/// <summary>Folder under the app data directory the generated sounds are written to.</summary>
	public const string DirectoryName = "sounds";

	public const string FillFileName = "fill.wav";

	public const string AlarmFileName = "alarm.wav";

	public const string NewsFileName = "news.wav";

	public const int SampleRate = 22050;

	/// <summary>A player still running after this is stopped.</summary>
	public static readonly TimeSpan PlayerTimeout = TimeSpan.FromSeconds(5);
}
