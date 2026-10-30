namespace LT.App.Helpers;

/// <summary>
/// The app-wide UI scale (zoom) as a factor (1 = normal), the view side of
/// <see cref="Stores.UiScaleStore"/>: every window's <see cref="Controls.UiScaleHost"/> and
/// <see cref="Controls.AppWindow"/>'s size follow it. Set on the UI thread.
/// </summary>
public static class UiScale
{
	private static double _factor = 1;

	/// <summary>Raised on the UI thread after <see cref="Factor"/> changed.</summary>
	public static event Action? Changed;

	public static double Factor
	{
		get => _factor;
		set
		{
			if (_factor.Equals(value))
				return;
			_factor = value;
			Changed?.Invoke();
		}
	}
}
