using LT.App.ViewModels;

namespace LT.App.Widgets.Placeholder;

/// <summary>Stands in for a widget that is on the roadmap (docs/vision.md) but not built yet.</summary>
public class PlaceholderViewModel(string title, string description) : ViewModelBase
{
	public string Title { get; } = title;
	public string Description { get; } = description;
}
