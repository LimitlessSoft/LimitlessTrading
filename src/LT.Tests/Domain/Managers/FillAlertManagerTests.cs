using LT.Contracts.Dtos.Executions;
using LT.Contracts.Helpers;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// The fill sound, fed the way the app does it: every executions answer of the trade journal goes
/// to the fill alert manager (FillAlertService), over the fake IB client.
/// </summary>
public class FillAlertManagerTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);

	private readonly FakeIbkrApiClient _ibkr = new()
	{
		IsConnected = true,
		ManagedAccounts = "DU1",
	};
	private readonly TestCache _cache = new();
	private readonly FakeSoundPlayer _sound = new();
	private readonly SettingsManager _settings;
	private readonly TradeJournalManager _journal;
	private readonly FillAlertManager _manager;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public FillAlertManagerTests()
	{
		_settings = new SettingsManager(_cache.Settings, _ibkr);
		_journal = new TradeJournalManager(
			_ibkr,
			new AccountManager(_ibkr, _cache.Manager),
			new InMemoryTradeJournalRepository(),
			new FakeTimeProvider(Now)
		);
		_manager = new FillAlertManager(_settings, _sound);
		_journal.ExecutionsReceived += x => _manager.OnExecutions(x);
	}

	[Fact]
	public async Task The_fills_replayed_on_connect_are_silent()
	{
		_ibkr.Executions = [Execution("e1"), Execution("e2")];
		_manager.BeginSession();

		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(0, _sound.FillsPlayed);
	}

	[Fact]
	public async Task A_new_fill_plays_the_sound_once()
	{
		_ibkr.Executions = [Execution("e1")];
		_manager.BeginSession();
		await _journal.RecordExecutionsAsync(Ct);

		_ibkr.Executions = [Execution("e1"), Execution("e2")];
		await _journal.RecordExecutionsAsync(Ct);
		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(1, _sound.FillsPlayed);
	}

	[Fact]
	public async Task Each_partial_fill_plays_the_sound()
	{
		_manager.BeginSession();
		await _journal.RecordExecutionsAsync(Ct);

		_ibkr.Executions = [Execution("e1.01")];
		await _journal.RecordExecutionsAsync(Ct);
		_ibkr.Executions = [Execution("e1.01"), Execution("e1.02")];
		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(2, _sound.FillsPlayed);
	}

	[Fact]
	public async Task Fills_arriving_together_play_one_sound()
	{
		_manager.BeginSession();
		await _journal.RecordExecutionsAsync(Ct);

		_ibkr.Executions = [Execution("e1"), Execution("e2"), Execution("e3")];
		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(1, _sound.FillsPlayed);
	}

	[Fact]
	public async Task A_reconnect_replays_silently_and_known_fills_never_sound_again()
	{
		_manager.BeginSession();
		_ibkr.Executions = [Execution("e1")];
		await _journal.RecordExecutionsAsync(Ct);

		// Disconnected, then connected again: e2 filled meanwhile and comes with the replay
		_manager.BeginSession();
		_manager.BeginSession();
		_ibkr.Executions = [Execution("e1"), Execution("e2")];
		await _journal.RecordExecutionsAsync(Ct);
		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(0, _sound.FillsPlayed);
	}

	[Fact]
	public async Task A_commission_reported_later_is_not_a_new_fill()
	{
		_manager.BeginSession();
		await _journal.RecordExecutionsAsync(Ct);
		_ibkr.Executions = [Execution("e1")];
		await _journal.RecordExecutionsAsync(Ct);

		_ibkr.Executions = [Execution("e1", commission: 1.5)];
		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(1, _sound.FillsPlayed);
	}

	[Fact]
	public async Task A_failed_request_does_not_end_the_replay()
	{
		_ibkr.ExecutionsException = new InvalidOperationException("IB said no");
		_manager.BeginSession();
		await _journal.RecordExecutionsAsync(Ct);

		_ibkr.ExecutionsException = null;
		_ibkr.Executions = [Execution("e1")];
		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(0, _sound.FillsPlayed);
	}

	[Fact]
	public async Task The_setting_turns_the_sound_off()
	{
		_cache.Settings.Settings.General.PlayFillSound = false;
		_manager.BeginSession();
		await _journal.RecordExecutionsAsync(Ct);

		_ibkr.Executions = [Execution("e1")];
		await _journal.RecordExecutionsAsync(Ct);

		Assert.Equal(0, _sound.FillsPlayed);
	}

	[Fact]
	public async Task A_failing_player_never_breaks_recording_fills()
	{
		_sound.Error = new InvalidOperationException("no audio device");
		_manager.BeginSession();
		await _journal.RecordExecutionsAsync(Ct);

		_ibkr.Executions = [Execution("e1")];

		Assert.Equal(1, await _journal.RecordExecutionsAsync(Ct));
	}

	[Fact]
	public void The_sound_is_on_by_default() =>
		Assert.True(_settings.GetAppSettings().PlayFillSound);

	[Fact]
	public void The_fill_chime_is_a_mono_16_bit_wav()
	{
		var wav = SoundHelpers.CreateFillChime(22050);

		Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
		Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
		Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
		Assert.Equal(1, BitConverter.ToInt16(wav, 22));
		Assert.Equal(22050, BitConverter.ToInt32(wav, 24));
		Assert.Equal(16, BitConverter.ToInt16(wav, 34));
		Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40));
		// About a quarter second
		Assert.InRange((wav.Length - 44) / 2 / 22050.0, 0.2, 0.3);
	}

	private static ExecutionDto Execution(string execId, double? commission = null) =>
		new()
		{
			ExecId = execId,
			Time = Now,
			Account = "DU1",
			ConId = 1,
			Symbol = "AAPL",
			SecurityType = "STK",
			Side = "BOT",
			Shares = 100,
			Price = 10,
			Commission = commission,
		};
}
