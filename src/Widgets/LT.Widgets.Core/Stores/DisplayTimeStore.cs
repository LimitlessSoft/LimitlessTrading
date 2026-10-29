using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LT.App.Helpers;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Stores;

/// <summary>
/// The zone every shown time is in (Settings → General → Time zone; the system's local zone by
/// default). Follows saved settings while it runs: <see cref="ZoneChanged"/> fires on the UI
/// thread, and widgets format their times again. Display only — trading days keep their own
/// zones.
/// </summary>
public partial class DisplayTimeStore : ObservableObject
{
	private readonly TimeProvider _timeProvider;
	private string? _zoneId;

	public DisplayTimeStore(ISettingsManager settingsManager, TimeProvider timeProvider)
	{
		_timeProvider = timeProvider;
		_zoneId = settingsManager.GetAppSettings().DisplayTimeZoneId;
		Zone = DisplayTimeHelpers.ResolveZone(_zoneId);
		settingsManager.AppSettingsChanged += settings =>
			Dispatcher.UIThread.Post(() => Apply(settings.DisplayTimeZoneId));
	}

	/// <summary>Raised on the UI thread after <see cref="Zone"/> changed.</summary>
	public event Action? ZoneChanged;

	/// <summary>Bound by the charts, which draw their time axes in it.</summary>
	[ObservableProperty]
	public partial TimeZoneInfo Zone { get; private set; }

	/// <summary>The current time in the display zone.</summary>
	public DateTimeOffset Now => ToDisplay(_timeProvider.GetUtcNow());

	public DateTimeOffset ToDisplay(DateTimeOffset time) => DisplayTimeHelpers.ToZone(time, Zone);

	/// <summary><paramref name="time"/> in the display zone, e.g. "14:05:03".</summary>
	public string Format(DateTimeOffset time, string format = "HH:mm:ss") =>
		ToDisplay(time).ToString(format, CultureInfo.CurrentCulture);

	/// <summary>"14:05" today, "26 Oct 14:05" another day (in the display zone).</summary>
	public string FormatDayTime(
		DateTimeOffset time,
		string todayFormat = "HH:mm",
		string otherDayFormat = "d MMM HH:mm",
		string? otherYearFormat = null
	) => FormatHelpers.DayTime(time, Now, Zone, todayFormat, otherDayFormat, otherYearFormat);

	private void Apply(string? zoneId)
	{
		if (zoneId == _zoneId)
			return;
		_zoneId = zoneId;
		Zone = DisplayTimeHelpers.ResolveZone(zoneId);
		ZoneChanged?.Invoke();
	}
}
