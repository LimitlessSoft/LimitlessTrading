using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LSCore.Exceptions;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;

namespace LT.App.Widgets.Chart;

/// <summary>
/// An indicator's settings, generated from the inputs it declares: a spin box per number (whole
/// or decimal, within its range), a check box per on / off input. A script also shows its file
/// (with a button that opens it in the system's editor) and its compile errors. OK applies
/// through <c>apply</c>, which validates; its error shows here and the dialog stays open.
/// </summary>
public sealed class IndicatorSettingsWindow : Window
{
	private readonly IndicatorDefinitionDto _definition;
	private readonly Action<Dictionary<string, double>> _apply;
	private readonly Dictionary<string, Control> _editors = [];
	private readonly TextBlock _error;

	public IndicatorSettingsWindow(
		IndicatorDefinitionDto definition,
		IndicatorInstanceDto instance,
		Action<Dictionary<string, double>> apply
	)
	{
		_definition = definition;
		_apply = apply;
		Title = definition.Name;
		Width = 380;
		SizeToContent = SizeToContent.Height;
		CanResize = false;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ShowInTaskbar = false;

		var panel = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
		panel.Children.Add(
			new TextBlock
			{
				Text = definition.Name,
				FontWeight = FontWeight.SemiBold,
				FontSize = 15,
			}
		);
		if (definition.Source == IndicatorSource.Script && definition.FilePath is { } path)
			panel.Children.Add(BuildFileRow(path));
		if (definition.Error != null)
			panel.Children.Add(BuildErrors(definition));

		if (definition.Inputs.Count == 0)
			panel.Children.Add(new TextBlock { Text = "No inputs.", Classes = { "secondary" } });
		else
			panel.Children.Add(BuildInputs(definition, instance));

		_error = new TextBlock
		{
			Classes = { "negative" },
			TextWrapping = TextWrapping.Wrap,
			IsVisible = false,
		};
		panel.Children.Add(_error);
		panel.Children.Add(BuildButtons());
		Content = panel;

		KeyDown += (_, e) =>
		{
			if (e.Key == Key.Escape)
				Close();
			else if (e.Key == Key.Enter)
				Apply();
		};
	}

	private Control BuildFileRow(string path)
	{
		var open = new Button { Content = "Edit script", Padding = new Thickness(10, 3) };
		open.Click += async (_, _) =>
		{
			if (Launcher is { } launcher)
				await launcher.LaunchFileInfoAsync(new FileInfo(path));
		};
		var grid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto"),
			ColumnSpacing = 8,
		};
		grid.Children.Add(
			new TextBlock
			{
				Text = path,
				Classes = { "secondary" },
				FontSize = 12,
				TextTrimming = TextTrimming.PrefixCharacterEllipsis,
				VerticalAlignment = VerticalAlignment.Center,
			}
		);
		Grid.SetColumn(open, 1);
		grid.Children.Add(open);
		ToolTip.SetTip(grid, path);
		return grid;
	}

	private static Control BuildErrors(IndicatorDefinitionDto definition)
	{
		var errors = definition
			.Diagnostics.Where(x => x.IsError)
			.Select(x => x.ToString())
			.ToList();
		var text = errors.Count > 0 ? string.Join("\n", errors) : definition.Error;
		return new ScrollViewer
		{
			MaxHeight = 160,
			Content = new SelectableTextBlock
			{
				Text = text,
				Classes = { "negative" },
				FontSize = 12,
				FontFamily = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace"),
				TextWrapping = TextWrapping.Wrap,
			},
		};
	}

	private Control BuildInputs(IndicatorDefinitionDto definition, IndicatorInstanceDto instance)
	{
		var grid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,160"),
			ColumnSpacing = 12,
			RowSpacing = 6,
		};
		for (var row = 0; row < definition.Inputs.Count; row++)
		{
			var input = definition.Inputs[row];
			grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			var value = instance.Inputs.TryGetValue(input.Name, out var saved)
				? saved
				: input.DefaultValue;
			var label = new TextBlock
			{
				Text = input.Name,
				VerticalAlignment = VerticalAlignment.Center,
				TextTrimming = TextTrimming.CharacterEllipsis,
			};
			Grid.SetRow(label, row);
			grid.Children.Add(label);
			var editor = CreateEditor(input, value);
			Grid.SetRow(editor, row);
			Grid.SetColumn(editor, 1);
			grid.Children.Add(editor);
			_editors[input.Name] = editor;
		}
		return grid;
	}

	private static Control CreateEditor(IndicatorInputDto input, double value)
	{
		if (input.Type == IndicatorInputType.Bool)
			return new CheckBox { IsChecked = value != 0 };
		var whole = input.Type == IndicatorInputType.Int;
		var editor = new NumericUpDown
		{
			Value = ToDecimal(value),
			Increment = whole ? 1 : ToDecimal(input.Step ?? GetStep(input.DefaultValue)),
			FormatString = whole ? "0" : "0.####",
			ParsingNumberStyle = NumberStyles.Float,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		if (input.Min is { } min && Math.Abs(min) < 1e15)
			editor.Minimum = (decimal)min;
		if (input.Max is { } max && Math.Abs(max) < 1e15)
			editor.Maximum = (decimal)max;
		return editor;
	}

	private Control BuildButtons()
	{
		var defaults = new Button { Content = "Defaults" };
		defaults.Click += (_, _) => ResetToDefaults();
		var cancel = new Button { Content = "Cancel" };
		cancel.Click += (_, _) => Close();
		var ok = new Button
		{
			Content = "OK",
			Classes = { "accent" },
			MinWidth = 72,
		};
		ok.Click += (_, _) => Apply();
		var buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0, 6, 0, 0),
		};
		if (_definition.Inputs.Count > 0)
			buttons.Children.Add(defaults);
		buttons.Children.Add(cancel);
		if (_definition.Inputs.Count > 0)
			buttons.Children.Add(ok);
		return buttons;
	}

	private void ResetToDefaults()
	{
		foreach (var input in _definition.Inputs)
			switch (_editors.GetValueOrDefault(input.Name))
			{
				case CheckBox check:
					check.IsChecked = input.DefaultValue != 0;
					break;
				case NumericUpDown number:
					number.Value = ToDecimal(input.DefaultValue);
					break;
			}
	}

	private void Apply()
	{
		if (_definition.Inputs.Count == 0)
		{
			Close();
			return;
		}
		var values = new Dictionary<string, double>();
		foreach (var (name, editor) in _editors)
			values[name] = editor switch
			{
				CheckBox check => check.IsChecked == true ? 1 : 0,
				NumericUpDown { Value: { } number } => (double)number,
				_ => double.NaN,
			};
		try
		{
			_apply(values);
			Close();
		}
		catch (LSCoreBadRequestException ex)
		{
			_error.Text = ex.Message;
			_error.IsVisible = true;
		}
	}

	private static double GetStep(double value) => Math.Abs(value) >= 10 ? 1 : 0.1;

	private static decimal ToDecimal(double value) =>
		double.IsFinite(value) && Math.Abs(value) < 1e15 ? (decimal)value : 0;
}
