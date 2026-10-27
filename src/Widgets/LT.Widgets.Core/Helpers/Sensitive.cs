using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using LT.App.Controls;

namespace LT.App.Helpers;

/// <summary>
/// Privacy mode: while <see cref="IsHidden"/> is on, sensitive values (account ids, balances,
/// P&amp;L, quantities) show as <see cref="Mask"/>. Views mark them with
/// <see cref="SensitiveTextBlock"/> (a TextBlock), <see cref="SensitiveTextColumn"/> (a DataGrid
/// text column) or <c>ComboBox.sensitive</c> (account pickers); a control that draws values itself
/// (a chart) passes them through <see cref="Apply"/> and sets <c>Redraws</c> to be drawn again on
/// every switch. Only what is drawn changes — bound values and view models are never touched, and
/// everything on screen follows the switch at once. <see cref="Stores.PrivacyStore"/> owns the
/// switch; this is its view side, reachable from every window, popup and grid cell.
/// </summary>
public static class Sensitive
{
	/// <summary>Always the same length, so the mask gives away nothing about the value.</summary>
	public const string Mask = "••••••";

	public static readonly AttachedProperty<bool> RedrawsProperty =
		AvaloniaProperty.RegisterAttached<Control, bool>("Redraws", typeof(Sensitive));

	// Controls that follow the switch; weak, so a closed widget's cells are not kept alive
	private static readonly List<WeakReference<Control>> Tracked = [];
	private static readonly ConditionalWeakTable<Control, object> TrackedSet = new();
	private static int _pruneAt = 256;
	private static bool _isHidden;

	static Sensitive() =>
		RedrawsProperty.Changed.AddClassHandler<Control>(
			(control, e) =>
			{
				if (e.NewValue is true)
					Track(control);
			}
		);

	/// <summary>Set by <see cref="Stores.PrivacyStore"/> on the UI thread.</summary>
	public static bool IsHidden
	{
		get => _isHidden;
		set
		{
			if (_isHidden == value)
				return;
			_isHidden = value;
			Prune();
			foreach (var reference in Tracked.ToList())
				if (reference.TryGetTarget(out var control))
					Refresh(control);
		}
	}

	public static bool GetRedraws(Control control) => control.GetValue(RedrawsProperty);

	public static void SetRedraws(Control control, bool value) =>
		control.SetValue(RedrawsProperty, value);

	/// <summary>
	/// <paramref name="text"/>, or <see cref="Mask"/> in privacy mode. Nothing stays nothing: an
	/// empty value hides nothing, and masking it would suggest there is one.
	/// </summary>
	[return: NotNullIfNotNull(nameof(text))]
	public static string? Apply(string? text) =>
		_isHidden && !string.IsNullOrEmpty(text) ? Mask : text;

	/// <summary>Makes <paramref name="control"/> follow the switch (once is enough).</summary>
	public static void Track(Control control)
	{
		if (!TrackedSet.TryAdd(control, Tracked))
			return;
		Tracked.Add(new WeakReference<Control>(control));
		if (Tracked.Count < _pruneAt)
			return;
		Prune();
		_pruneAt = Math.Max(256, Tracked.Count * 2);
	}

	private static void Refresh(Control control)
	{
		if (control is SensitiveTextBlock text)
			text.Refresh();
		else
			control.InvalidateVisual();
	}

	private static void Prune() => Tracked.RemoveAll(x => !x.TryGetTarget(out _));
}
