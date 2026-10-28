using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Entities;
using LT.Contracts.Helpers;

namespace LT.Contracts.DtoMappings.ChartDrawings;

/// <summary>A drawing saved before styles or levels gets the kind's built-in ones.</summary>
public class ChartDrawingDtoMapper : ILSCoreMapper<ChartDrawingEntity, ChartDrawingDto>
{
	public ChartDrawingDto ToMapped(ChartDrawingEntity source) =>
		new()
		{
			Id = source.Id,
			ConId = source.ConId,
			Kind = source.Kind,
			Points =
			[
				.. source.Points.Select(x => new ChartDrawingPointDto
				{
					Time = x.Time,
					Price = x.Price,
				}),
			],
			Text = source.Text,
			Style = source.Style is { } style
				? ChartDrawingHelpers.ToDto(style)
				: ChartDrawingHelpers.GetBuiltInStyle(source.Kind),
			IsLocked = source.IsLocked,
			IsHidden = source.IsHidden,
			FibLevels =
				!ChartDrawingHelpers.IsFib(source.Kind) ? []
				: source.FibLevels is { } levels ? ChartDrawingHelpers.ToDtos(levels)
				: ChartDrawingHelpers.GetDefaultFibLevels(source.Kind),
		};
}
