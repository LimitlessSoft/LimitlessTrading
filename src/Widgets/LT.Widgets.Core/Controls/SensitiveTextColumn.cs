using Avalonia.Controls;

namespace LT.App.Controls;

/// <summary>
/// A DataGrid text column of sensitive values (account, quantity, P&amp;L): its cells are
/// <see cref="SensitiveTextBlock"/>s, masked in privacy mode. Binds, sorts and looks like
/// <see cref="DataGridTextColumn"/>.
/// </summary>
public class SensitiveTextColumn : DataGridTextColumn
{
	protected override Control GenerateElement(DataGridCell cell, object dataItem)
	{
		var text = new SensitiveTextBlock { Name = "CellTextBlock" };
		if (IsSet(FontFamilyProperty))
			text.FontFamily = FontFamily;
		if (IsSet(FontSizeProperty))
			text.FontSize = FontSize;
		if (IsSet(FontStyleProperty))
			text.FontStyle = FontStyle;
		if (IsSet(FontWeightProperty))
			text.FontWeight = FontWeight;
		if (IsSet(ForegroundProperty))
			text.Foreground = Foreground;
		if (Binding != null)
			text.Bind(TextBlock.TextProperty, Binding);
		return text;
	}
}
