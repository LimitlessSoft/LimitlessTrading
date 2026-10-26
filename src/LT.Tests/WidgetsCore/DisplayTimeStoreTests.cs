using Avalonia.Threading;
using LT.App.Stores;
using LT.Contracts.Enums;
using LT.Contracts.Requests.Settings;
using LT.Domain.Managers;
using LT.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace LT.Tests.WidgetsCore;

public class DisplayTimeStoreTests
{
	private readonly FakeTimeProvider _time = new(
		new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero)
	);
	private readonly SettingsManager _settings = new(
		new InMemorySettingsRepository(),
		new FakeIbkrApiClient()
	);

	public DisplayTimeStoreTests() => LSCoreContainer.EnsureInitialized();

	[Fact]
	public void Without_a_saved_zone_times_are_in_the_system_local_zone()
	{
		var store = new DisplayTimeStore(_settings, _time);

		Assert.Equal(TimeZoneInfo.Local, store.Zone);
		Assert.Equal(_time.GetUtcNow().ToLocalTime(), store.Now);
	}

	[Fact]
	public void A_saved_zone_converts_times_and_now()
	{
		Save("Asia/Tokyo");

		var store = new DisplayTimeStore(_settings, _time);

		Assert.Equal(new DateTime(2026, 10, 26, 23, 0, 0), store.Now.DateTime);
		Assert.Equal("23:00:00", store.Format(_time.GetUtcNow()));
		Assert.Equal("23:30", store.FormatDayTime(_time.GetUtcNow().AddMinutes(30)));
		Assert.Equal(
			"10-27 00:30",
			store.FormatDayTime(_time.GetUtcNow().AddMinutes(90), otherDayFormat: "MM-dd HH:mm")
		);
	}

	[Fact]
	public void Saving_another_zone_switches_and_tells_the_widgets()
	{
		var store = new DisplayTimeStore(_settings, _time);
		var changes = 0;
		store.ZoneChanged += () => changes++;

		Save("America/New_York");
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());

		Assert.Equal(1, changes);
		Assert.Equal("10:00:00", store.Format(_time.GetUtcNow()));
	}

	[Fact]
	public void Saving_the_same_zone_again_changes_nothing()
	{
		Save("America/New_York");
		var store = new DisplayTimeStore(_settings, _time);
		var changes = 0;
		store.ZoneChanged += () => changes++;

		Save("America/New_York");
		TestUiThread.Invoke(() => Dispatcher.UIThread.RunJobs());

		Assert.Equal(0, changes);
	}

	private void Save(string? zoneId) =>
		_settings.SaveAppSettings(
			new SaveAppSettingsRequest
			{
				MarketDataType = MarketDataType.Delayed,
				HeartbeatSeconds = 10,
				DisplayTimeZoneId = zoneId,
			}
		);
}
