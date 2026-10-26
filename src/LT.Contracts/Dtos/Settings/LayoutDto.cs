using LT.Contracts.Enums;

namespace LT.Contracts.Dtos.Settings;

public class LayoutDto
{
	public double LeftWidth { get; set; }
	public double RightWidth { get; set; }
	public double BottomHeight { get; set; }
	public string? LeftWidgetId { get; set; }
	public string? RightWidgetId { get; set; }
	public string? BottomWidgetId { get; set; }
	public List<WidgetPlacementDto> Widgets { get; set; } = [];
	public CenterNodeDto? Center { get; set; }
	public ChartTimeframe ChartTimeframe { get; set; }
	public double WindowWidth { get; set; }
	public double WindowHeight { get; set; }
	public bool IsMaximized { get; set; }
}
