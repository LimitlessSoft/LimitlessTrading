using LT.Contracts.Dtos.ChartDrawings;

namespace LT.App.Stores;

/// <summary>
/// The drawing last copied on a chart (Ctrl+C), pasted on any chart (Ctrl+V; another symbol's
/// too). In memory only — the system clipboard is left alone; used on the UI thread only.
/// </summary>
public class ChartDrawingClipboardStore
{
	public ChartDrawingDto? Drawing { get; set; }
}
