using LSCore.Mapper.Contracts;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.ChartDrawings;

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
		};
}
