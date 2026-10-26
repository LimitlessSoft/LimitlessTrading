using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSCore.Validation.Contracts;
using LT.App.Helpers;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.ChartTemplates;
using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;
using LT.Contracts.Enums.ValidationCodes;
using LT.Contracts.Helpers;
using LT.Contracts.Requests.ChartTemplates;

namespace LT.App.Widgets.Chart;

/// <summary>
/// Chart templates (<see cref="LT.Contracts.Interfaces.IManagers.IChartTemplateManager"/>): the
/// header's template menu saves this chart's setup — timeframe, period, indicators with their
/// inputs, visibility and colours, right margin — under a name (a taken name asks before
/// replacing it), applies one, renames, deletes, marks one as the default for new charts, and
/// exports / imports them as JSON files (the files are picked by the view). Problems and results
/// show in the menu.
/// <para>
/// The chart remembers the template it last applied or was saved as (or the default it started
/// from). "Follow and save on change" ties it to that template: its edits are saved to it
/// (<see cref="ChartTemplateConstants.FollowSaveDebounce"/> after they stop, only the settings the
/// template holds) and every template save
/// (<see cref="LT.Contracts.Interfaces.IManagers.IChartTemplateManager.TemplateSaved"/>) is applied
/// to it, so all charts following one template stay alike. Applying a save is not an
/// edit (it is not saved back), and a save the chart already looks like is ignored. Turning it on
/// while the chart differs from the template asks first, then updates the chart to it.
/// </para>
/// </summary>
public partial class ChartViewModel
{
	private string? _templateId;
	private bool _applyingTemplate;

	// A timeframe or period changed while applying: one reload once the template is applied
	private bool _reloadAfterTemplate;

	/// <summary>The template this chart uses (last applied or saved as); null: none.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasAppliedTemplate))]
	[NotifyPropertyChangedFor(nameof(FollowStatus))]
	[NotifyPropertyChangedFor(nameof(FollowPrompt))]
	public partial string? AppliedTemplateName { get; private set; }

	/// <summary>Following needs a template; without one the switch is disabled.</summary>
	public bool HasAppliedTemplate => AppliedTemplateName != null;

	/// <summary>"Follow and save on change": this chart and its template stay alike.</summary>
	[ObservableProperty]
	public partial bool FollowTemplate { get; private set; }

	/// <summary>Following was asked for while the chart differs from the template; asks first.</summary>
	[ObservableProperty]
	public partial bool IsConfirmingFollow { get; private set; }

	/// <summary>Beside the switch: the template it follows.</summary>
	public string FollowStatus => AppliedTemplateName ?? "No template";

	public string FollowPrompt =>
		$"This chart uses an outdated version of template \"{AppliedTemplateName}\". Following it first updates the chart to the latest version.";

	/// <summary>Turns following on (asking first when the chart is outdated) or off.</summary>
	[RelayCommand]
	public void ToggleFollowTemplate()
	{
		IsConfirmingFollow = false;
		if (FollowTemplate)
			StopFollowingTemplate();
		else if (GetAppliedTemplate() is { } template)
		{
			if (ChartTemplateHelpers.Matches(template.Settings, GetTemplateSettings()))
				FollowTemplate = true;
			else
				IsConfirmingFollow = true;
		}
	}

	/// <summary>Updates the chart to the template's latest version, then follows it.</summary>
	[RelayCommand]
	public void ConfirmFollow()
	{
		IsConfirmingFollow = false;
		if (GetAppliedTemplate() is not { } template)
			return;
		ApplyTemplateSettings(template.Settings);
		FollowTemplate = true;
	}

	/// <summary>Leaves the chart as it is, not following.</summary>
	[RelayCommand]
	public void CancelFollow() => IsConfirmingFollow = false;

	/// <summary>
	/// Saves the chart's edits to the template it follows (the debounce's end; also when following
	/// stops, another template is applied or the chart closes). Nothing when they already match.
	/// </summary>
	public void SaveFollowedTemplate()
	{
		_templateSaveTimer.Stop();
		if (!FollowTemplate || GetAppliedTemplate() is not { } template)
			return;
		var own = GetTemplateSettings();
		if (ChartTemplateHelpers.Matches(template.Settings, own))
			return;
		try
		{
			_chartTemplateManager.UpdateSettings(
				new UpdateChartTemplateSettingsRequest
				{
					Id = template.Id,
					Settings = ChartTemplateHelpers.Merge(template.Settings, own),
				}
			);
		}
		catch (Exception ex)
		{
			var message = ErrorMessageHelpers.ToUserMessage(ex);
			ShowTemplateError(message);
			_messageLogManager.Add(
				MessageSeverity.Warning,
				MessageLogConstants.SourceChart,
				$"{Title}: template {template.Name} not saved ({message})"
			);
		}
	}

	private void FlushTemplateSave()
	{
		if (_templateSaveTimer.IsEnabled)
			SaveFollowedTemplate();
	}

	private void StopFollowingTemplate()
	{
		FlushTemplateSave();
		IsConfirmingFollow = false;
		FollowTemplate = false;
	}

	// A template-relevant setting changed: saved once the edits stop, when this chart follows
	private void OnTemplateSettingsEdited()
	{
		if (_applyingTemplate || !FollowTemplate || _disposed)
			return;
		_templateSaveTimer.Stop();
		_templateSaveTimer.Start();
	}

	// Any chart's save (this one's too: it matches already, so nothing happens)
	private void OnTemplateSaved(ChartTemplateDto template)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => OnTemplateSaved(template));
			return;
		}
		if (_disposed || template.Id != _templateId)
			return;
		AppliedTemplateName = template.Name;
		if (
			!FollowTemplate
			|| ChartTemplateHelpers.Matches(template.Settings, GetTemplateSettings())
		)
			return;
		// The template's latest version wins over an edit still waiting to be saved
		_templateSaveTimer.Stop();
		ApplyTemplateSettings(template.Settings);
	}

	// The template in use as stored now; a deleted one is forgotten (and no longer followed)
	private ChartTemplateDto? GetAppliedTemplate()
	{
		if (_templateId == null)
			return null;
		var template = _chartTemplateManager.GetTemplate(_templateId);
		SetAppliedTemplate(template);
		if (template == null)
			ShowTemplateError(ChartTemplateValidationCodes.CHTVC_004.GetValidationMessage());
		return template;
	}

	private void SetAppliedTemplate(ChartTemplateDto? template)
	{
		_templateId = template?.Id;
		AppliedTemplateName = template?.Name;
		if (template != null)
			return;
		_templateSaveTimer.Stop();
		IsConfirmingFollow = false;
		FollowTemplate = false;
	}

	// A restored chart following its template takes the template's latest version
	private void LoadTemplateState(WidgetPlacementDto placement)
	{
		var template = placement.ChartTemplateId is { } id
			? _chartTemplateManager.GetTemplate(id)
			: null;
		SetAppliedTemplate(template);
		if (template == null || !placement.ChartFollowTemplate)
			return;
		ApplyTemplateSettings(template.Settings);
		FollowTemplate = true;
	}

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
		IsConfirmingFollow = false;
		TemplateMessage = null;
		LoadTemplates();
	}

	/// <summary>What a template saved from this chart holds now.</summary>
	public ChartTemplateSettingsDto GetTemplateSettings() =>
		new()
		{
			Timeframe = SelectedTimeframe.Value,
			Period = SelectedPeriod.Value,
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
		FlushTemplateSave();
		ChartTemplateDto? saved = null;
		if (
			!RunTemplateChange(() =>
				saved = _chartTemplateManager.Save(
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
		// The chart now uses what it was saved as (and follows that one, if it was following)
		SetAppliedTemplate(saved);
		ShowTemplateResult($"Saved {NewTemplateName?.Trim()}");
		NewTemplateName = null;
		LoadTemplates();
	}

	/// <summary>
	/// Applies the template to this chart; settings it doesn't hold stay as they are. The chart
	/// uses it from now on (and follows it instead, if it was following another).
	/// </summary>
	public void ApplyTemplate(string id)
	{
		if (_chartTemplateManager.GetTemplate(id) is not { } template)
		{
			LoadTemplates();
			return;
		}
		FlushTemplateSave();
		ApplyTemplateSettings(template.Settings);
		SetAppliedTemplate(template);
	}

	// Not an edit: never saved back to a template
	private void ApplyTemplateSettings(ChartTemplateSettingsDto settings)
	{
		_applyingTemplate = true;
		_reloadAfterTemplate = false;
		try
		{
			if (settings.Timeframe is { } timeframe)
				SetTimeframe(timeframe);
			if (settings.Period is { } period)
				SetPeriod(period);
			if (settings.RightMargin is { } margin)
				RightMargin = Math.Max(0, margin);
			if (settings.Indicators is { } indicators)
			{
				_indicators = ChartTemplateHelpers.CopyIndicators(indicators);
				IndicatorResults = [];
				OnIndicatorsEdited();
			}
		}
		finally
		{
			_applyingTemplate = false;
		}
		if (_reloadAfterTemplate)
			Reload();
	}

	// Timeframe and period: applying a template that sets both asks IB for bars once
	private void ReloadUnlessApplyingTemplate()
	{
		if (_applyingTemplate)
			_reloadAfterTemplate = true;
		else
			Reload();
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
		var templates = _chartTemplateManager.GetTemplates();
		foreach (var template in templates)
			Templates.Add(new ChartTemplateItemViewModel(this, template));
		OnPropertyChanged(nameof(HasTemplates));
		// Renamed or deleted since
		if (_templateId != null)
			SetAppliedTemplate(templates.FirstOrDefault(x => x.Id == _templateId));
	}
}
