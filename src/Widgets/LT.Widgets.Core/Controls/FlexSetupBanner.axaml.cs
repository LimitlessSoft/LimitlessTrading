using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LT.App.Controls;

/// <summary>
/// "History needs IB Flex" banner with a primary Set up… button, shared by Account performance and
/// Day trade performance. Hidden while <see cref="Text"/> is empty; <see cref="IsWarning"/> draws
/// it for an error the user must fix. The view model decides the text
/// (FlexStatusHelpers.GetBanner) and opens Settings through <see cref="Command"/>.
/// </summary>
public partial class FlexSetupBanner : UserControl
{
	public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<
		FlexSetupBanner,
		string?
	>(nameof(Text));

	public static readonly StyledProperty<bool> IsWarningProperty = AvaloniaProperty.Register<
		FlexSetupBanner,
		bool
	>(nameof(IsWarning));

	public static readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<
		FlexSetupBanner,
		ICommand?
	>(nameof(Command));

	public FlexSetupBanner()
	{
		InitializeComponent();
		IsVisible = false;
	}

	public string? Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public bool IsWarning
	{
		get => GetValue(IsWarningProperty);
		set => SetValue(IsWarningProperty, value);
	}

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == TextProperty)
		{
			Message.Text = Text;
			IsVisible = !string.IsNullOrEmpty(Text);
		}
		else if (change.Property == IsWarningProperty)
			Frame.Classes.Set("warning", IsWarning);
		else if (change.Property == CommandProperty)
			SetUpButton.Command = Command;
	}
}
