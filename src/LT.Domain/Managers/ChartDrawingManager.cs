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
/// others (<see cref="DrawingsChanged"/>). Each change is also an undo step of its contract; the
/// history lives in memory only.
/// </summary>
public class ChartDrawingManager(IChartDrawingRepository repository) : IChartDrawingManager
{
	private readonly Lock _lock = new();
	private ChartDrawingFileEntity? _file;

	// Per contract: undo steps (the newest last) and the undone ones to redo (the latest last)
	private readonly Dictionary<
		int,
		(List<IReadOnlyList<ChartDrawingChange>> Undo, List<IReadOnlyList<ChartDrawingChange>> Redo)
	> _history = [];

	public event Action? DrawingsChanged;

	// Read under _lock
	private ChartDrawingFileEntity File => _file ??= repository.Get();
	private List<ChartDrawingEntity> Drawings => File.Drawings;

	public IReadOnlyList<ChartDrawingDto> GetDrawings(int conId)
	{
		lock (_lock)
			return Drawings
				.Where(x => x.ConId == conId)
				.ToMappedList<ChartDrawingEntity, ChartDrawingDto>();
	}

	public ChartDrawingStyleDto GetDefaultStyle(ChartDrawingKind kind)
	{
		lock (_lock)
			return File.ToolStyles.FirstOrDefault(x => x.Kind == kind)?.Style is { } style
				? ChartDrawingHelpers.ToDto(style)
				: ChartDrawingHelpers.GetBuiltInStyle(kind);
	}

	public ChartDrawingDto Create(CreateChartDrawingRequest request)
	{
		request.Validate();
		var style = request.Style ?? GetDefaultStyle(request.Kind);
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
				Style = ChartDrawingHelpers.ToEntity(style),
				FibLevels =
					ChartDrawingHelpers.IsFib(request.Kind) && request.FibLevels is { } levels
						? ChartDrawingHelpers.ToEntities(levels)
						: null,
			};
			Drawings.Add(drawing);
			Record(
				drawing.ConId,
				[
					new ChartDrawingChange(
						null,
						ChartDrawingHistoryHelpers.Clone(drawing),
						ChartDrawingHistoryHelpers.GetContractIndex(Drawings, drawing.Id)
					),
				]
			);
			repository.Save(File);
			created = drawing.ToMapped<ChartDrawingEntity, ChartDrawingDto>();
		}
		DrawingsChanged?.Invoke();
		return created;
	}

	public ChartDrawingDto Update(UpdateChartDrawingRequest request)
	{
		request.Validate();
		return Change(
			request.Id,
			drawing =>
			{
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
			}
		);
	}

	public ChartDrawingDto UpdateProperties(UpdateChartDrawingPropertiesRequest request)
	{
		request.Validate();
		return Change(
			request.Id,
			drawing =>
			{
				if (request.Style is { } style)
				{
					drawing.Style = ChartDrawingHelpers.ToEntity(style);
					// The kind's next drawings start with it
					File.ToolStyles.RemoveAll(x => x.Kind == drawing.Kind);
					File.ToolStyles.Add(
						new ChartDrawingToolStyleEntity
						{
							Kind = drawing.Kind,
							Style = ChartDrawingHelpers.ToEntity(style),
						}
					);
				}
				if (request.IsLocked is { } locked)
					drawing.IsLocked = locked;
				if (request.IsHidden is { } hidden)
					drawing.IsHidden = hidden;
				if (request.FibLevels is { } levels && ChartDrawingHelpers.IsFib(drawing.Kind))
					drawing.FibLevels = ChartDrawingHelpers.ToEntities(levels);
			}
		);
	}

	public void Delete(string id)
	{
		lock (_lock)
		{
			var index = Drawings.FindIndex(x => x.Id == id);
			if (index < 0)
				return;
			var drawing = Drawings[index];
			var change = new ChartDrawingChange(
				ChartDrawingHistoryHelpers.Clone(drawing),
				null,
				ChartDrawingHistoryHelpers.GetContractIndex(Drawings, id)
			);
			Drawings.RemoveAt(index);
			Record(drawing.ConId, [change]);
			repository.Save(File);
		}
		DrawingsChanged?.Invoke();
	}

	public int DeleteAll(int conId)
	{
		int removed;
		lock (_lock)
		{
			var step = Drawings
				.Where(x => x.ConId == conId)
				.Select(
					(x, i) => new ChartDrawingChange(ChartDrawingHistoryHelpers.Clone(x), null, i)
				)
				.ToList();
			removed = Drawings.RemoveAll(x => x.ConId == conId);
			if (removed == 0)
				return 0;
			Record(conId, step);
			repository.Save(File);
		}
		DrawingsChanged?.Invoke();
		return removed;
	}

	public int ShowHidden(int conId)
	{
		int shown;
		lock (_lock)
		{
			var step = new List<ChartDrawingChange>();
			foreach (var drawing in Drawings.Where(x => x.ConId == conId && x.IsHidden))
			{
				var before = ChartDrawingHistoryHelpers.Clone(drawing);
				drawing.IsHidden = false;
				step.Add(
					new ChartDrawingChange(
						before,
						ChartDrawingHistoryHelpers.Clone(drawing),
						ChartDrawingHistoryHelpers.GetContractIndex(Drawings, drawing.Id)
					)
				);
			}
			shown = step.Count;
			if (shown == 0)
				return 0;
			Record(conId, step);
			repository.Save(File);
		}
		DrawingsChanged?.Invoke();
		return shown;
	}

	public bool CanUndo(int conId)
	{
		lock (_lock)
			return _history.TryGetValue(conId, out var history) && history.Undo.Count > 0;
	}

	public bool CanRedo(int conId)
	{
		lock (_lock)
			return _history.TryGetValue(conId, out var history) && history.Redo.Count > 0;
	}

	public bool Undo(int conId) => Step(conId, undo: true);

	public bool Redo(int conId) => Step(conId, undo: false);

	private bool Step(int conId, bool undo)
	{
		lock (_lock)
		{
			if (!_history.TryGetValue(conId, out var history))
				return false;
			var (from, to) = undo ? (history.Undo, history.Redo) : (history.Redo, history.Undo);
			if (from.Count == 0)
				return false;
			var step = from[^1];
			from.RemoveAt(from.Count - 1);
			ChartDrawingHistoryHelpers.Apply(Drawings, step, undo);
			to.Add(step);
			repository.Save(File);
		}
		DrawingsChanged?.Invoke();
		return true;
	}

	// One drawing changed in place: validated by the edit (which throws to refuse), recorded,
	// saved and raised
	private ChartDrawingDto Change(string id, Action<ChartDrawingEntity> edit)
	{
		ChartDrawingDto changed;
		lock (_lock)
		{
			var drawing =
				Drawings.FirstOrDefault(x => x.Id == id)
				?? throw new LSCoreBadRequestException(
					ChartDrawingValidationCodes.CDVC_009.GetValidationMessage()
				);
			var before = ChartDrawingHistoryHelpers.Clone(drawing);
			edit(drawing);
			Record(
				drawing.ConId,
				[
					new ChartDrawingChange(
						before,
						ChartDrawingHistoryHelpers.Clone(drawing),
						ChartDrawingHistoryHelpers.GetContractIndex(Drawings, id)
					),
				]
			);
			repository.Save(File);
			changed = drawing.ToMapped<ChartDrawingEntity, ChartDrawingDto>();
		}
		DrawingsChanged?.Invoke();
		return changed;
	}

	// Under _lock
	private void Record(int conId, IReadOnlyList<ChartDrawingChange> step)
	{
		if (!_history.TryGetValue(conId, out var history))
			_history[conId] = history = ([], []);
		ChartDrawingHistoryHelpers.Push(
			history.Undo,
			history.Redo,
			step,
			ChartDrawingConstants.MaxUndoSteps
		);
	}

	private static List<ChartDrawingPointEntity> ToEntities(List<ChartDrawingPointDto> points) =>
		[.. points.Select(x => new ChartDrawingPointEntity { Time = x.Time, Price = x.Price })];
}
