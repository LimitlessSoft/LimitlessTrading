using LSCore.Mapper.Contracts;
using LT.Contracts.DtoMappings.Contracts;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Settings;

public class WidgetPlacementEntityMapper : ILSCoreMapper<WidgetPlacementDto, WidgetPlacementEntity>
{
	public WidgetPlacementEntity ToMapped(WidgetPlacementDto source) =>
		new()
		{
			Id = source.Id,
			Tool = source.Tool,
			Dock = source.Dock,
			FloatX = source.FloatX,
			FloatY = source.FloatY,
			FloatWidth = source.FloatWidth,
			FloatHeight = source.FloatHeight,
			LinkGroup = source.LinkGroup,
			IsPinned = source.IsPinned,
			ChartTimeframe = source.ChartTimeframe,
			ChartLinked = source.ChartLinked,
			ChartContract = source.ChartContract is { } contract
				? new ContractEntityMapper().ToMapped(contract)
				: null,
		};
}
