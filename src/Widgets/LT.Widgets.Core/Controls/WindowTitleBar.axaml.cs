using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace LT.App.Controls;

/// <summary>
/// Title bar of an <see cref="AppWindow"/>, part of its template: title, optional content left and
/// right, and minimize / maximize / close acting on the window it sits in. Which window buttons
/// show follows the window: minimize needs <c>CanMinimize</c> and a taskbar entry to come back
/// from, maximize needs <c>CanMaximize</c> and <c>CanResize</c>.
/// </summary>
public partial class WindowTitleBar : UserControl
{
	public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<
		WindowTitleBar,
		string?
	>(nameof(Title));

	public static readonly StyledProperty<object?> LeftContentProperty = AvaloniaProperty.Register<
		WindowTitleBar,
		object?
	>(nameof(LeftContent));

	public static readonly StyledProperty<object?> RightContentProperty = AvaloniaProperty.Register<
		WindowTitleBar,
		object?
	>(nameof(RightContent));

	private Window? _window;
	private DateTime _lastPress = DateTime.MinValue;
	private Point _lastPressPosition;

	public WindowTitleBar()
	{
		InitializeComponent();
		Bar.PointerPressed += OnBarPressed;
	}

	public string? Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public object? LeftContent
	{
		get => GetValue(LeftContentProperty);
		set => SetValue(LeftContentProperty, value);
	}

	public object? RightContent
	{
		get => GetValue(RightContentProperty);
		set => SetValue(RightContentProperty, value);
	}

	private bool CanMaximize => _window is { CanMaximize: true, CanResize: true };

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == TitleProperty)
			TitleText.Text = Title;
		else if (change.Property == LeftContentProperty)
			LeftHost.Content = LeftContent;
		else if (change.Property == RightContentProperty)
			RightHost.Content = RightContent;
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		_window = TopLevel.GetTopLevel(this) as Window;
		if (_window is null)
			return;
		_window.PropertyChanged += OnWindowPropertyChanged;
		UpdateButtons();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_window?.PropertyChanged -= OnWindowPropertyChanged;
		_window = null;
		base.OnDetachedFromVisualTree(e);
	}

	private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (
			e.Property == Window.WindowStateProperty
			|| e.Property == Window.CanMinimizeProperty
			|| e.Property == Window.CanMaximizeProperty
			|| e.Property == Window.CanResizeProperty
			|| e.Property == Window.ShowInTaskbarProperty
		)
			UpdateButtons();
	}

	// The maximize button shows restore while maximized
	private void UpdateButtons()
	{
		MinimizeButton.IsVisible = _window is { CanMinimize: true, ShowInTaskbar: true };
		MaximizeButton.IsVisible = CanMaximize;
		var maximized = _window?.WindowState == WindowState.Maximized;
		MaximizeIcon.Data = (Geometry)
			this.FindResource(maximized ? "WindowRestoreIcon" : "WindowMaximizeIcon")!;
		ToolTip.SetTip(MaximizeButton, maximized ? "Restore" : "Maximize");
	}

	// Title bar buttons act themselves: the decoration button roles do nothing on Linux
	private void OnMinimizeClick(object? sender, RoutedEventArgs e) =>
		_window?.WindowState = WindowState.Minimized;

	private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximized();

	private void OnCloseClick(object? sender, RoutedEventArgs e) => _window?.Close();

	// The bar moves the window itself (a decoration drag role grabs the mouse and swallows double-
	// clicks): a press drags, a second press within the double-click time maximizes / restores.
	// Presses on its buttons are handled by them and never get here.
	private void OnBarPressed(object? sender, PointerPressedEventArgs e)
	{
		if (_window is null || !e.GetCurrentPoint(Bar).Properties.IsLeftButtonPressed)
			return;
		var now = DateTime.UtcNow;
		var position = e.GetPosition(_window);
		var settings = Application.Current?.PlatformSettings;
		var doubleClickTime =
			settings?.GetDoubleTapTime(e.Pointer.Type) ?? TimeSpan.FromMilliseconds(500);
		var doubleClickSize = settings?.GetDoubleTapSize(e.Pointer.Type) ?? new Size(4, 4);
		var isDoubleClick =
			now - _lastPress <= doubleClickTime
			&& Math.Abs(position.X - _lastPressPosition.X) <= doubleClickSize.Width
			&& Math.Abs(position.Y - _lastPressPosition.Y) <= doubleClickSize.Height;
		_lastPress = isDoubleClick ? DateTime.MinValue : now;
		_lastPressPosition = position;
		e.Handled = true;
		if (isDoubleClick && CanMaximize)
			ToggleMaximized();
		else if (!isDoubleClick)
			_window.BeginMoveDrag(e);
	}

	private void ToggleMaximized() =>
		_window?.WindowState =
			_window.WindowState == WindowState.Maximized
				? WindowState.Normal
				: WindowState.Maximized;
}
