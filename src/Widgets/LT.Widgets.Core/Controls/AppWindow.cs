using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LT.App.Helpers;

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
/// <para>
/// The UI scale (<see cref="UiScale"/>): the template scales the bar and content through a
/// <see cref="UiScaleHost"/>. A dialog's size (Width / Height and their limits, as set in XAML or
/// its constructor) is in unscaled units and grows with the scale, so its content still fits;
/// windows whose size the user owns (main, workspace, floating widget) set
/// <see cref="ScalesSizeWithUi"/> off.
/// </para>
/// </summary>
public class AppWindow : Window
{
	public static readonly StyledProperty<object?> TitleBarLeftContentProperty =
		AvaloniaProperty.Register<AppWindow, object?>(nameof(TitleBarLeftContent));

	public static readonly StyledProperty<object?> TitleBarRightContentProperty =
		AvaloniaProperty.Register<AppWindow, object?>(nameof(TitleBarRightContent));

	public static readonly StyledProperty<bool> ScalesSizeWithUiProperty =
		AvaloniaProperty.Register<AppWindow, bool>(nameof(ScalesSizeWithUi), true);

	// The UI scale the size properties carry now, and the unscaled limits
	private double _sizeScale = 1;
	private Size _baseMin;
	private Size _baseMax;

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

	/// <summary>
	/// The window's size grows with the UI scale (dialogs; the default). Off for windows whose size
	/// the user sets and the layout saves.
	/// </summary>
	public bool ScalesSizeWithUi
	{
		get => GetValue(ScalesSizeWithUiProperty);
		set => SetValue(ScalesSizeWithUiProperty, value);
	}

	public AppWindow() => AddHandler(WindowOpenedEvent, OnShowing);

	// Raised by Show / ShowDialog before the window's size is worked out: the XAML and the
	// subclass's constructor have set the unscaled sizes by now (OnInitialized runs too early, in
	// the base constructor, before the XAML is loaded)
	private void OnShowing(object? sender, RoutedEventArgs e)
	{
		_baseMin = new Size(MinWidth, MinHeight);
		_baseMax = new Size(MaxWidth, MaxHeight);
		ScaleSize();
	}

	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		UiScale.Changed += ScaleSize;
	}

	protected override void OnClosed(EventArgs e)
	{
		UiScale.Changed -= ScaleSize;
		base.OnClosed(e);
	}

	private void ScaleSize()
	{
		var factor = UiScale.Factor;
		if (!ScalesSizeWithUi || factor.Equals(_sizeScale))
			return;
		var ratio = factor / _sizeScale;
		_sizeScale = factor;
		// Never past the screen's working area (a large dialog at 200 %)
		var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
		var limit = screen is null
			? new Size(double.PositiveInfinity, double.PositiveInfinity)
			: screen.WorkingArea.Size.ToSize(screen.Scaling);
		MinWidth = Math.Min(_baseMin.Width * factor, limit.Width);
		MinHeight = Math.Min(_baseMin.Height * factor, limit.Height);
		MaxWidth = _baseMax.Width * factor;
		MaxHeight = _baseMax.Height * factor;
		// Width / Height scale from what they are now, so a size the user dragged to is kept
		if (!double.IsNaN(Width) && !SizeToContent.HasFlag(SizeToContent.Width))
			Width = Math.Min(Width * ratio, limit.Width);
		if (!double.IsNaN(Height) && !SizeToContent.HasFlag(SizeToContent.Height))
			Height = Math.Min(Height * ratio, limit.Height);
	}

	// Subclasses (MainWindow, SettingsWindow…) all take the AppWindow theme
	protected override Type StyleKeyOverride => typeof(AppWindow);
}
