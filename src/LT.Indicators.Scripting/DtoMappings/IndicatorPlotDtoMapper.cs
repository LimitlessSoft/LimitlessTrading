using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Enums;
using LT.Indicators.Sdk;

namespace LT.Indicators.Scripting.DtoMappings;

public class IndicatorPlotDtoMapper : ILSCoreMapper<PlotOutput, IndicatorPlotDto>
{
	public IndicatorPlotDto ToMapped(PlotOutput source) =>
		new()
		{
			Name = source.Name,
			Kind = source.Kind switch
			{
				PlotKind.Histogram => IndicatorPlotKind.Histogram,
				PlotKind.Band => IndicatorPlotKind.Band,
				PlotKind.Markers => IndicatorPlotKind.Markers,
				PlotKind.Level => IndicatorPlotKind.Level,
				_ => IndicatorPlotKind.Line,
			},
			Color = source.Color.ToArgb(),
			Color2 = source.Color2?.ToArgb(),
			BarColors = source.BarColors is { } colors ? [.. colors.Select(x => x.ToArgb())] : null,
			Width = source.Width,
			LineStyle = source.LineStyle switch
			{
				LineStyle.Dashed => IndicatorLineStyle.Dashed,
				LineStyle.Dotted => IndicatorLineStyle.Dotted,
				_ => IndicatorLineStyle.Solid,
			},
			Values = source.Values,
			Values2 = source.Values2,
			Level = source.Level,
			Shape = source.Shape switch
			{
				MarkerShape.TriangleDown => IndicatorMarkerShape.TriangleDown,
				MarkerShape.Circle => IndicatorMarkerShape.Circle,
				MarkerShape.Square => IndicatorMarkerShape.Square,
				MarkerShape.Cross => IndicatorMarkerShape.Cross,
				_ => IndicatorMarkerShape.TriangleUp,
			},
			Position = source.Position switch
			{
				MarkerPosition.AboveBar => IndicatorMarkerPosition.AboveBar,
				MarkerPosition.AtValue => IndicatorMarkerPosition.AtValue,
				_ => IndicatorMarkerPosition.BelowBar,
			},
		};

	public static IndicatorPane ToPane(PaneKind pane) =>
		pane switch
		{
			PaneKind.Separate => IndicatorPane.Separate,
			PaneKind.Volume => IndicatorPane.Volume,
			_ => IndicatorPane.Overlay,
		};
}
