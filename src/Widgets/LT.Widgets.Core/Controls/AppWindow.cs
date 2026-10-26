using Avalonia;
using Avalonia.Controls;

namespace LT.App.Controls;

/// <summary>
/// Every window the app opens: the main and workspace windows, settings, dialogs, floating
/// widgets. It has no OS title bar; its template (the <c>AppWindow</c> ControlTheme in
/// App.axaml) turns on Avalonia's client-side decorations without frame or shadow and puts a
/// <see cref="WindowTitleBar"/> above the content. The bar shows <see cref="Window.Title"/> and
/// <see cref="TitleBarLeftContent"/> / <see cref="TitleBarRightContent"/> (the window's own
/// buttons); its minimize / maximize buttons follow <c>CanMinimize</c>, <c>ShowInTaskbar</c>,
/// <c>CanMaximize</c> and <c>CanResize</c>. <see cref="Window.Content"/> stays the window's
/// content and <see cref="TemplatedControl.Padding"/> pads only it, not the bar.
/// </summary>
public class AppWindow : Window
{
	public static readonly StyledProperty<object?> TitleBarLeftContentProperty =
		AvaloniaProperty.Register<AppWindow, object?>(nameof(TitleBarLeftContent));

	public static readonly StyledProperty<object?> TitleBarRightContentProperty =
		AvaloniaProperty.Register<AppWindow, object?>(nameof(TitleBarRightContent));

	/// <summary>Left of the title (e.g. the main window's menu).</summary>
	public object? TitleBarLeftContent
	{
		get => GetValue(TitleBarLeftContentProperty);
		set => SetValue(TitleBarLeftContentProperty, value);
	}

	/// <summary>Right of the title, before the window buttons (e.g. the update pill).</summary>
	public object? TitleBarRightContent
	{
		get => GetValue(TitleBarRightContentProperty);
		set => SetValue(TitleBarRightContentProperty, value);
	}

	// Subclasses (MainWindow, SettingsWindow…) all take the AppWindow theme
	protected override Type StyleKeyOverride => typeof(AppWindow);
}
