using Avalonia;
using Avalonia.Controls;

namespace LT.App.Controls;

/// <summary>
/// Busy indicator for a widget body: an accent arc that sweeps around a faint track inside a small
/// card, with an optional pulsing <see cref="Text"/> under it. Bind <c>IsVisible</c> to the
/// view model's loading flag; the animations run only while the spinner is visible and in the
/// visual tree (the <c>:spinning</c> pseudo-class), so an idle one doesn't tick the clock.
/// </summary>
public partial class LoadingSpinner : UserControl
{
	public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<
		LoadingSpinner,
		string?
	>(nameof(Text));

	private bool _isAttached;

	public LoadingSpinner()
	{
		InitializeComponent();
	}

	public string? Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		_isAttached = true;
		UpdateSpinning();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);
		_isAttached = false;
		UpdateSpinning();
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsVisibleProperty)
			UpdateSpinning();
	}

	private void UpdateSpinning() => PseudoClasses.Set(":spinning", _isAttached && IsVisible);
}
