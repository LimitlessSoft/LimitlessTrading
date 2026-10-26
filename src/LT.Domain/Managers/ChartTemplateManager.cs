using LSCore.Exceptions;
using LSCore.Validation.Contracts;
using LSCore.Validation.Domain;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Entities;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Interfaces.IManagers;
using LT.Contracts.Interfaces.IRepositories;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.Domain.Managers;

/// <summary>
/// Chart templates in <see cref="SettingsEntity.ChartTemplates"/>. A singleton: every chart
/// listens to the same <see cref="TemplateSaved"/>.
/// </summary>
public class ChartTemplateManager(ISettingsRepository settingsRepository) : IChartTemplateManager
{
	public event Action<ChartTemplateDto>? TemplateSaved;

	public IReadOnlyList<ChartTemplateDto> GetTemplates()
	{
		var settings = settingsRepository.Get();
		return
		[
			.. ChartTemplateHelpers
				.GetTemplates(settings)
				.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
				.Select(x => ChartTemplateHelpers.ToDto(x, settings.DefaultChartTemplateId)),
		];
	}

	public ChartTemplateDto? GetTemplate(string id)
	{
		var settings = settingsRepository.Get();
		return
			ChartTemplateHelpers.GetTemplates(settings).FirstOrDefault(x => x.Id == id)
				is { } template
			? ChartTemplateHelpers.ToDto(template, settings.DefaultChartTemplateId)
			: null;
	}

	public ChartTemplateDto? FindByName(string name)
	{
		var settings = settingsRepository.Get();
		return
			ChartTemplateHelpers.FindByName(ChartTemplateHelpers.GetTemplates(settings), name)
				is { } template
			? ChartTemplateHelpers.ToDto(template, settings.DefaultChartTemplateId)
			: null;
	}

	public ChartTemplateDto? GetDefault() =>
		settingsRepository.Get().DefaultChartTemplateId is { } id ? GetTemplate(id) : null;

	public ChartTemplateDto Save(SaveChartTemplateRequest request)
	{
		request.Validate();
		var name = request.Name.Trim();
		var settings = settingsRepository.Get();
		var templates = ChartTemplateHelpers.GetTemplates(settings);
		var template = ChartTemplateHelpers.FindByName(templates, name);
		if (template != null && !request.Overwrite)
			throw new LSCoreBadRequestException(
				ChartTemplateValidationCodes.CHTVC_003.GetValidationMessage(template.Name)
			);
		// Overwriting keeps the id (and with it the default) and the stored name's spelling
		var saved = ChartTemplateHelpers.ToEntity(
			template?.Id ?? Guid.NewGuid().ToString("N"),
			template?.Name ?? name,
			request.Settings
		);
		if (template == null)
			templates.Add(saved);
		else
			templates[templates.IndexOf(template)] = saved;
		settingsRepository.Save(settings);
		var dto = ChartTemplateHelpers.ToDto(saved, settings.DefaultChartTemplateId);
		TemplateSaved?.Invoke(dto);
		return dto;
	}

	public ChartTemplateDto UpdateSettings(UpdateChartTemplateSettingsRequest request)
	{
		request.Validate();
		var settings = settingsRepository.Get();
		var templates = ChartTemplateHelpers.GetTemplates(settings);
		var template =
			templates.FirstOrDefault(x => x.Id == request.Id)
			?? throw new LSCoreBadRequestException(
				ChartTemplateValidationCodes.CHTVC_004.GetValidationMessage()
			);
		var saved = ChartTemplateHelpers.ToEntity(template.Id, template.Name, request.Settings);
		templates[templates.IndexOf(template)] = saved;
		settingsRepository.Save(settings);
		var dto = ChartTemplateHelpers.ToDto(saved, settings.DefaultChartTemplateId);
		TemplateSaved?.Invoke(dto);
		return dto;
	}

	public ChartTemplateDto Rename(RenameChartTemplateRequest request)
	{
		request.Validate();
		var name = request.Name.Trim();
		var settings = settingsRepository.Get();
		var templates = ChartTemplateHelpers.GetTemplates(settings);
		var template =
			templates.FirstOrDefault(x => x.Id == request.Id)
			?? throw new LSCoreBadRequestException(
				ChartTemplateValidationCodes.CHTVC_004.GetValidationMessage()
			);
		if (ChartTemplateHelpers.FindByName(templates, name) is { } other && other != template)
			throw new LSCoreBadRequestException(
				ChartTemplateValidationCodes.CHTVC_003.GetValidationMessage(other.Name)
			);
		template.Name = name;
		settingsRepository.Save(settings);
		return ChartTemplateHelpers.ToDto(template, settings.DefaultChartTemplateId);
	}

	public void Delete(string id)
	{
		var settings = settingsRepository.Get();
		if (ChartTemplateHelpers.GetTemplates(settings).RemoveAll(x => x.Id == id) == 0)
			return;
		if (settings.DefaultChartTemplateId == id)
			settings.DefaultChartTemplateId = null;
		settingsRepository.Save(settings);
	}

	public void SetDefault(string? id)
	{
		var settings = settingsRepository.Get();
		if (id != null && ChartTemplateHelpers.GetTemplates(settings).All(x => x.Id != id))
			throw new LSCoreBadRequestException(
				ChartTemplateValidationCodes.CHTVC_004.GetValidationMessage()
			);
		settings.DefaultChartTemplateId = id;
		settingsRepository.Save(settings);
	}

	public string Export(IReadOnlyCollection<string>? ids = null) =>
		ChartTemplateHelpers.SerializeFile(
			ChartTemplateHelpers
				.GetTemplates(settingsRepository.Get())
				.Where(x => ids == null || ids.Contains(x.Id))
				.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
		);

	public IReadOnlyList<ChartTemplateDto> Import(string json)
	{
		if (json.Length > ChartTemplateConstants.MaxFileLength)
			throw new LSCoreBadRequestException(
				ChartTemplateValidationCodes.CHTVC_013.GetValidationMessage(
					ChartTemplateConstants.MaxFileLength / (1024 * 1024)
				)
			);
		var file =
			ChartTemplateHelpers.ParseFile(json)
			?? throw new LSCoreBadRequestException(
				ChartTemplateValidationCodes.CHTVC_005.GetValidationMessage()
			);
		var request = new ImportChartTemplatesRequest
		{
			Format = file.Format,
			Templates =
			[
				.. (file.Templates ?? [])
					.Where(x => x != null)
					.Select(x => ChartTemplateHelpers.ToDto(x, null)),
			],
		};
		request.Validate();

		var settings = settingsRepository.Get();
		var templates = ChartTemplateHelpers.GetTemplates(settings);
		var added = new List<ChartTemplateEntity>();
		foreach (var imported in request.Templates)
		{
			// Stored as this version writes it: whatever it didn't know is gone already
			var template = ChartTemplateHelpers.ToEntity(
				Guid.NewGuid().ToString("N"),
				ChartTemplateHelpers.GetFreeName(imported.Name, templates.Select(x => x.Name)),
				imported.Settings
			);
			templates.Add(template);
			added.Add(template);
		}
		settingsRepository.Save(settings);
		return
		[
			.. added.Select(x => ChartTemplateHelpers.ToDto(x, settings.DefaultChartTemplateId)),
		];
	}
}
