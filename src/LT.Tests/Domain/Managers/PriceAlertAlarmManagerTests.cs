using LT.Contracts.Constants;
using LT.Contracts.Dtos.Contracts;
using LT.Contracts.Dtos.PriceAlerts;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.PriceAlerts;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.Domain.Managers;

/// <summary>
/// The alert popup's state and alarm: a fired alert waits to be dismissed and the alarm repeats
/// every <see cref="PriceAlertConstants.AlarmRepeatInterval"/> until the last one is.
/// </summary>
public class PriceAlertAlarmManagerTests
{
	private static readonly ContractDto Aapl = new()
	{
		ConId = 265598,
		Symbol = "AAPL",
		SecurityType = "STK",
		Exchange = "SMART",
		Currency = "USD",
	};

	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly FakeSoundPlayer _sound = new();
	private readonly PriceAlertAlarmManager _manager;

	public PriceAlertAlarmManagerTests() => _manager = new PriceAlertAlarmManager(_sound, _time);

	[Fact]
	public void Nothing_rings_before_an_alert_fires()
	{
		_time.Advance(PriceAlertConstants.AlarmRepeatInterval * 3);

		Assert.False(_manager.IsRinging);
		Assert.Empty(_manager.GetPending());
		Assert.Equal(0, _sound.AlarmsPlayed);
	}

	[Fact]
	public void A_fired_alert_rings_at_once_and_repeats_until_dismissed()
	{
		_manager.OnTriggered(Alert("a"));

		Assert.True(_manager.IsRinging);
		Assert.Equal(1, _sound.AlarmsPlayed);

		_time.Advance(PriceAlertConstants.AlarmRepeatInterval);
		_time.Advance(PriceAlertConstants.AlarmRepeatInterval);
		Assert.Equal(3, _sound.AlarmsPlayed);

		_manager.Dismiss("a");
		_time.Advance(PriceAlertConstants.AlarmRepeatInterval * 5);

		Assert.False(_manager.IsRinging);
		Assert.Empty(_manager.GetPending());
		Assert.Equal(3, _sound.AlarmsPlayed);
	}

	[Fact]
	public void The_alarm_stops_only_when_every_alert_is_dismissed()
	{
		_manager.OnTriggered(Alert("a"));
		_manager.OnTriggered(Alert("b"));

		// One alarm for both, not one each
		Assert.Equal(1, _sound.AlarmsPlayed);
		Assert.Equal(["a", "b"], _manager.GetPending().Select(x => x.Id));

		_manager.Dismiss("a");
		_time.Advance(PriceAlertConstants.AlarmRepeatInterval);
		Assert.True(_manager.IsRinging);
		Assert.Equal(2, _sound.AlarmsPlayed);

		_manager.Dismiss("b");
		_time.Advance(PriceAlertConstants.AlarmRepeatInterval);
		Assert.False(_manager.IsRinging);
		Assert.Equal(2, _sound.AlarmsPlayed);
	}

	[Fact]
	public void Dismiss_all_stops_the_alarm()
	{
		_manager.OnTriggered(Alert("a"));
		_manager.OnTriggered(Alert("b"));

		_manager.DismissAll();
		_time.Advance(PriceAlertConstants.AlarmRepeatInterval * 3);

		Assert.False(_manager.IsRinging);
		Assert.Empty(_manager.GetPending());
		Assert.Equal(1, _sound.AlarmsPlayed);
	}

	[Fact]
	public void An_alert_after_the_alarm_stopped_rings_again()
	{
		_manager.OnTriggered(Alert("a"));
		_manager.Dismiss("a");

		_manager.OnTriggered(Alert("b"));

		Assert.True(_manager.IsRinging);
		Assert.Equal(2, _sound.AlarmsPlayed);
	}

	[Fact]
	public void An_alert_that_fires_again_replaces_its_entry()
	{
		_manager.OnTriggered(Alert("a", 190));
		_manager.OnTriggered(Alert("b"));
		_manager.OnTriggered(Alert("a", 195));

		var pending = _manager.GetPending();
		Assert.Equal(["b", "a"], pending.Select(x => x.Id));
		Assert.Equal(195, pending[1].Price);
	}

	[Fact]
	public void Changed_is_raised_on_trigger_and_dismissal_only()
	{
		var changes = 0;
		_manager.Changed += () => changes++;

		_manager.OnTriggered(Alert("a"));
		_manager.Dismiss("missing");
		_manager.Dismiss("a");
		_manager.DismissAll();

		Assert.Equal(2, changes);
	}

	[Fact]
	public void A_failing_player_never_breaks_an_alert()
	{
		_sound.Error = new InvalidOperationException("no audio");

		_manager.OnTriggered(Alert("a"));
		_time.Advance(PriceAlertConstants.AlarmRepeatInterval);

		Assert.True(_manager.IsRinging);
		Assert.Single(_manager.GetPending());
	}

	[Fact]
	public void A_price_alert_that_fires_reaches_the_alarm_the_way_the_app_wires_it()
	{
		LSCoreContainer.EnsureInitialized();
		var alerts = new PriceAlertManager(
			new InMemoryPriceAlertRepository(),
			new MarketDataManager(new FakeIbkrApiClient { IsConnected = true }),
			new MessageLogManager(),
			_time
		);
		// As PriceAlertService does
		alerts.AlertTriggered += _manager.OnTriggered;
		var alert = alerts.Create(
			new CreatePriceAlertRequest
			{
				Contract = Aapl,
				Price = 190,
				CurrentPrice = 185,
			}
		);

		alerts.OnPrice(Aapl.ConId, 190.5);

		var pending = Assert.Single(_manager.GetPending());
		Assert.Equal(alert.Id, pending.Id);
		Assert.Equal(190.5, pending.TriggerPrice);
		Assert.Equal(1, _sound.AlarmsPlayed);
	}

	[Fact]
	public void The_alarm_is_a_mono_16_bit_wav_under_a_second()
	{
		var wav = SoundHelpers.CreateAlarm(22050);

		Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
		Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
		Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
		Assert.Equal(1, BitConverter.ToInt16(wav, 22));
		Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40));
		var seconds = (wav.Length - 44) / 2 / 22050.0;
		Assert.InRange(seconds, 0.5, 1.0);
		Assert.True(seconds < PriceAlertConstants.AlarmRepeatInterval.TotalSeconds);
	}

	private static PriceAlertDto Alert(string id, double price = 190) =>
		new()
		{
			Id = id,
			Contract = Aapl,
			Price = price,
			TriggeredAt = new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero),
			TriggerPrice = price + 0.1,
		};
}
