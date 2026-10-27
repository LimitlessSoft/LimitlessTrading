using System.Text;
using LT.Contracts.Constants;

namespace LT.Contracts.Helpers;

/// <summary>
/// The app's sounds are generated, not shipped: no audio file to license, and a WAV every system
/// player reads.
/// </summary>
public static class SoundHelpers
{
	/// <summary>The fill sound: two short rising bell tones (C6, G6), about a quarter second.</summary>
	public static byte[] CreateFillChime(int sampleRate = SoundConstants.SampleRate)
	{
		var samples = new List<short>();
		AddTone(samples, sampleRate, 1046.5, 0.09);
		AddTone(samples, sampleRate, 1568.0, 0.17);
		return ToWav(samples, sampleRate);
	}

	/// <summary>
	/// The price alert alarm: two quick high-low pairs (A5, E5), about 0.7 s, made to be repeated
	/// and noticed.
	/// </summary>
	public static byte[] CreateAlarm(int sampleRate = SoundConstants.SampleRate)
	{
		var samples = new List<short>();
		for (var i = 0; i < 2; i++)
		{
			AddTone(samples, sampleRate, 880.0, 0.13);
			AddTone(samples, sampleRate, 659.3, 0.13);
			AddSilence(samples, sampleRate, 0.08);
		}
		return ToWav(samples, sampleRate);
	}

	/// <summary>Mono 16-bit PCM WAV (RIFF) of the samples.</summary>
	public static byte[] ToWav(IReadOnlyList<short> samples, int sampleRate)
	{
		const short channels = 1;
		const short bitsPerSample = 16;
		const short blockAlign = channels * bitsPerSample / 8;
		var dataLength = samples.Count * blockAlign;

		using var stream = new MemoryStream(44 + dataLength);
		using var writer = new BinaryWriter(stream, Encoding.ASCII);
		writer.Write("RIFF"u8);
		writer.Write(36 + dataLength);
		writer.Write("WAVE"u8);
		writer.Write("fmt "u8);
		writer.Write(16);
		writer.Write((short)1); // PCM
		writer.Write(channels);
		writer.Write(sampleRate);
		writer.Write(sampleRate * blockAlign);
		writer.Write(blockAlign);
		writer.Write(bitsPerSample);
		writer.Write("data"u8);
		writer.Write(dataLength);
		foreach (var sample in samples)
			writer.Write(sample);
		writer.Flush();
		return stream.ToArray();
	}

	private static void AddSilence(List<short> samples, int sampleRate, double seconds) =>
		samples.AddRange(new short[(int)(sampleRate * seconds)]);

	// A sine with a 5 ms attack and an exponential decay, so it neither clicks nor drones
	private static void AddTone(List<short> samples, int sampleRate, double hz, double seconds)
	{
		const double volume = 0.35;
		var count = (int)(sampleRate * seconds);
		var attack = sampleRate * 0.005;
		for (var i = 0; i < count; i++)
		{
			var envelope = Math.Min(1, i / attack) * Math.Exp(-4.0 * i / count);
			var value = Math.Sin(2 * Math.PI * hz * i / sampleRate) * envelope * volume;
			samples.Add((short)(value * short.MaxValue));
		}
	}
}
