namespace LT.App.Stores;

/// <summary>A choice of the UI scale pickers, shown as "125%".</summary>
public sealed record UiScaleOption(int Percent)
{
	public override string ToString() => $"{Percent}%";
}
