using LT.Contracts.Dtos.Settings;

namespace LT.App.Workspace;

/// <summary>A widget view model that keeps per-instance settings in its saved placement.</summary>
public interface IWidgetState
{
	void LoadState(WidgetPlacementDto placement);
	void SaveState(WidgetPlacementDto placement);
}
