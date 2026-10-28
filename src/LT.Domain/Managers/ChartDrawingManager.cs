using LSCore.Exceptions;
using LSCore.Mapper.Domain;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartDrawings;
using LT.Contracts.Entities;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.ChartDrawings;

namespace LT.Domain.Managers;

/// <summary>
/// Singleton (Program.cs): holds the drawings read once from chart-drawings.json and saves every
/// change, so every chart of a contract shows the same drawings and an edit on one reaches the
/// others (<see cref="DrawingsChanged"/>).
/// </summary>
public class ChartDrawingManager(IChartDrawingRepository repository) : IChartDrawingManager
{
	private readonly Lock _lock = new();
	private List<ChartDrawingEntity>? _drawings;

	public event Action? DrawingsChanged;

	// Read under _lock
	private List<ChartDrawingEntity> Drawings => _drawings ??= repository.Get();

	public IReadOnlyList<ChartDrawingDto> GetDrawings(int conId)
	{
		lock (_lock)
			return Drawings
				.Where(x => x.ConId == conId)
				.ToMappedList<ChartDrawingEntity, ChartDrawingDto>();
	}

	public ChartDrawingDto Create(CreateChartDrawingRequest request)
	{
		request.Validate();
		ChartDrawingDto created;
		lock (_lock)
		{
			if (Drawings.Count >= ChartDrawingConstants.MaxDrawings)
				throw new LSCoreBadRequestException(
					ChartDrawingValidationCodes.CDVC_008.GetValidationMessage(
						ChartDrawingConstants.MaxDrawings
					)
				);
			if (
				Drawings.Count(x => x.ConId == request.ConId)
				>= ChartDrawingConstants.MaxDrawingsPerContract
			)
				throw new LSCoreBadRequestException(
					ChartDrawingValidationCodes.CDVC_007.GetValidationMessage(
						ChartDrawingConstants.MaxDrawingsPerContract
					)
				);
			var drawing = new ChartDrawingEntity
			{
				Id = Guid.NewGuid().ToString("N"),
				ConId = request.ConId,
				Kind = request.Kind,
				Points = ToEntities(request.Points),
				Text = request.Kind == ChartDrawingKind.Text ? request.Text!.Trim() : null,
			};
			Drawings.Add(drawing);
			repository.Save(Drawings);
			created = drawing.ToMapped<ChartDrawingEntity, ChartDrawingDto>();
		}
		DrawingsChanged?.Invoke();
		return created;
	}

	public ChartDrawingDto Update(UpdateChartDrawingRequest request)
	{
		request.Validate();
		ChartDrawingDto updated;
		lock (_lock)
		{
			var drawing =
				Drawings.FirstOrDefault(x => x.Id == request.Id)
				?? throw new LSCoreBadRequestException(
					ChartDrawingValidationCodes.CDVC_009.GetValidationMessage()
				);
			if (!ChartDrawingHelpers.HasValidPointCount(drawing.Kind, request.Points.Count))
				throw new LSCoreBadRequestException(
					ChartDrawingValidationCodes.CDVC_003.GetValidationMessage()
				);
			if (drawing.Kind == ChartDrawingKind.Text)
			{
				// A note keeps its text when only moved
				var text = request.Text ?? drawing.Text;
				if (string.IsNullOrWhiteSpace(text))
					throw new LSCoreBadRequestException(
						ChartDrawingValidationCodes.CDVC_005.GetValidationMessage()
					);
				drawing.Text = text.Trim();
			}
			drawing.Points = ToEntities(request.Points);
			repository.Save(Drawings);
			updated = drawing.ToMapped<ChartDrawingEntity, ChartDrawingDto>();
		}
		DrawingsChanged?.Invoke();
		return updated;
	}

	public void Delete(string id)
	{
		lock (_lock)
		{
			if (Drawings.RemoveAll(x => x.Id == id) == 0)
				return;
			repository.Save(Drawings);
		}
		DrawingsChanged?.Invoke();
	}

	public int DeleteAll(int conId)
	{
		int removed;
		lock (_lock)
		{
			removed = Drawings.RemoveAll(x => x.ConId == conId);
			if (removed == 0)
				return 0;
			repository.Save(Drawings);
		}
		DrawingsChanged?.Invoke();
		return removed;
	}

	private static List<ChartDrawingPointEntity> ToEntities(List<ChartDrawingPointDto> points) =>
		[.. points.Select(x => new ChartDrawingPointEntity { Time = x.Time, Price = x.Price })];
}
