using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Chart templates (<see cref="LT.Contracts.Interfaces.IManagers.IChartTemplateManager"/>): the
/// header's template menu saves this chart's setup — timeframe, indicators with their inputs,
/// visibility and colours, right margin — under a name (a taken name asks before replacing it),
/// applies one, renames, deletes, marks one as the default for new charts, and exports / imports
/// them as JSON files (the files are picked by the view). Problems and results show in the menu.
/// </summary>
public partial class ChartViewModel
{
	/// <summary>Template menu: every template, by name.</summary>
	public ObservableCollection<ChartTemplateItemViewModel> Templates { get; } = [];

	public bool HasTemplates => Templates.Count > 0;

	/// <summary>Template menu: the name to save the current setup under.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(OverwritePrompt))]
	public partial string? NewTemplateName { get; set; }

	/// <summary>Template menu: saving would replace a template of that name; asks first.</summary>
	[ObservableProperty]
	public partial bool IsConfirmingOverwrite { get; set; }

	public string OverwritePrompt => $"Replace {NewTemplateName?.Trim()}?";

	/// <summary>Template menu: the last error or result ("Imported 2 templates").</summary>
	[ObservableProperty]
	public partial string? TemplateMessage { get; set; }

	[ObservableProperty]
	public partial bool IsTemplateMessageError { get; set; }

	/// <summary>Reloads the list (another chart may have changed it) and clears the menu's state.</summary>
	public void RefreshTemplates()
	{
		IsConfirmingOverwrite = false;
		TemplateMessage = null;
		LoadTemplates();
	}

	/// <summary>What a template saved from this chart holds now.</summary>
	public ChartTemplateSettingsDto GetTemplateSettings() =>
		new()
		{
			Timeframe = SelectedTimeframe.Value,
			RightMargin = RightMargin,
			Indicators = [.. _indicators],
		};

	/// <summary>Saves the setup under <see cref="NewTemplateName"/>; a taken name asks first.</summary>
	[RelayCommand]
	public void SaveTemplate()
	{
		var name = NewTemplateName ?? string.Empty;
		if (_chartTemplateManager.FindByName(name) != null)
		{
			TemplateMessage = null;
			IsConfirmingOverwrite = true;
			return;
		}
		Save(overwrite: false);
	}

	[RelayCommand]
	public void ConfirmOverwrite() => Save(overwrite: true);

	[RelayCommand]
	public void CancelOverwrite() => IsConfirmingOverwrite = false;

	private void Save(bool overwrite)
	{
		IsConfirmingOverwrite = false;
		if (
			!RunTemplateChange(() =>
				_chartTemplateManager.Save(
					new SaveChartTemplateRequest
					{
						Name = NewTemplateName ?? string.Empty,
						Settings = GetTemplateSettings(),
						Overwrite = overwrite,
					}
				)
			)
		)
			return;
		ShowTemplateResult($"Saved {NewTemplateName?.Trim()}");
		NewTemplateName = null;
		LoadTemplates();
	}

	/// <summary>Applies the template to this chart; settings it doesn't hold stay as they are.</summary>
	public void ApplyTemplate(string id)
	{
		if (_chartTemplateManager.GetTemplate(id) is not { } template)
		{
			LoadTemplates();
			return;
		}
		ApplyTemplateSettings(template.Settings);
	}

	private void ApplyTemplateSettings(ChartTemplateSettingsDto settings)
	{
		if (settings.Timeframe is { } timeframe)
			SetTimeframe(timeframe);
		if (settings.RightMargin is { } margin)
			RightMargin = Math.Max(0, margin);
		if (settings.Indicators is { } indicators)
		{
			_indicators = ChartTemplateHelpers.CopyIndicators(indicators);
			IndicatorResults = [];
			OnIndicatorsEdited();
		}
	}

	[RelayCommand]
	private void ToggleDefaultTemplate(ChartTemplateItemViewModel item)
	{
		if (
			RunTemplateChange(() =>
				_chartTemplateManager.SetDefault(item.IsDefault ? null : item.Id)
			)
		)
			LoadTemplates();
	}

	[RelayCommand]
	private void BeginRenameTemplate(ChartTemplateItemViewModel item)
	{
		foreach (var row in Templates)
			row.IsConfirmingDelete = row.IsRenaming = false;
		item.RenameText = item.Name;
		item.IsRenaming = true;
	}

	/// <summary>Saves the typed name; on an error (blank, taken) the name box stays open.</summary>
	[RelayCommand]
	private void CommitRenameTemplate(ChartTemplateItemViewModel item)
	{
		if (!item.IsRenaming)
			return;
		// Close the box first: hiding it fires its LostFocus, which must not commit again
		item.IsRenaming = false;
		if (item.RenameText.Trim() == item.Name)
			return;
		if (
			RunTemplateChange(() =>
				_chartTemplateManager.Rename(
					new RenameChartTemplateRequest { Id = item.Id, Name = item.RenameText }
				)
			)
		)
			LoadTemplates();
		else
			item.IsRenaming = true;
	}

	[RelayCommand]
	private void CancelRenameTemplate(ChartTemplateItemViewModel item)
	{
		item.IsRenaming = false;
		TemplateMessage = null;
	}

	[RelayCommand]
	private void AskDeleteTemplate(ChartTemplateItemViewModel item)
	{
		foreach (var row in Templates)
			row.IsConfirmingDelete = row.IsRenaming = false;
		item.IsConfirmingDelete = true;
	}

	[RelayCommand]
	private void ConfirmDeleteTemplate(ChartTemplateItemViewModel item)
	{
		RunTemplateChange(() => _chartTemplateManager.Delete(item.Id));
		LoadTemplates();
	}

	[RelayCommand]
	private void CancelDeleteTemplate(ChartTemplateItemViewModel item) =>
		item.IsConfirmingDelete = false;

	/// <summary>The JSON file of one template, or of all of them (<paramref name="id"/> null).</summary>
	public string ExportTemplates(string? id) =>
		_chartTemplateManager.Export(id != null ? [id] : null);

	/// <summary>A file name to suggest for exporting one template, or all of them.</summary>
	public string GetTemplateFileName(string? id) =>
		ChartTemplateHelpers.GetFileName(
			id != null && _chartTemplateManager.GetTemplate(id) is { } template
				? template.Name
				: ChartTemplateConstants.AllTemplatesFileName
		);

	/// <summary>The export was written (or failed, with the reason).</summary>
	public void OnTemplatesExported(string fileName, int count, Exception? error)
	{
		if (error != null)
			ShowTemplateError(ErrorMessageHelpers.ToUserMessage(error));
		else
			ShowTemplateResult(
				count == 1 ? $"Exported to {fileName}" : $"Exported {count} templates to {fileName}"
			);
	}

	/// <summary>Adds the templates of an exported file; a taken name gets " (2)" ….</summary>
	public void ImportTemplates(string json)
	{
		IReadOnlyList<ChartTemplateDto> imported = [];
		if (!RunTemplateChange(() => imported = _chartTemplateManager.Import(json)))
			return;
		LoadTemplates();
		ShowTemplateResult(
			imported.Count == 1
				? $"Imported {imported[0].Name}"
				: $"Imported {imported.Count} templates"
		);
	}

	/// <summary>A file that couldn't be read.</summary>
	public void ShowTemplateError(string message)
	{
		TemplateMessage = message;
		IsTemplateMessageError = true;
	}

	private void ShowTemplateResult(string message)
	{
		TemplateMessage = message;
		IsTemplateMessageError = false;
	}

	private bool RunTemplateChange(Action change)
	{
		try
		{
			change();
			TemplateMessage = null;
			return true;
		}
		catch (Exception ex)
		{
			ShowTemplateError(ErrorMessageHelpers.ToUserMessage(ex));
			return false;
		}
	}

	private void LoadTemplates()
	{
		Templates.Clear();
		foreach (var template in _chartTemplateManager.GetTemplates())
			Templates.Add(new ChartTemplateItemViewModel(this, template));
		OnPropertyChanged(nameof(HasTemplates));
	}
}
