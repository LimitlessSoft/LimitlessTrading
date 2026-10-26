using Avalonia.Controls;
using LT.Contracts.Dtos.Indicators;
using LT.Contracts.Helpers;

namespace LT.App.Widgets.Chart;

/// <summary>
/// The chart. Clicks in an indicator's legend row come here: the eye shows / hides it, ✕
/// removes it, the gear or the name opens its settings dialog.
/// </summary>
public partial class ChartView : UserControl
{
	public ChartView()
	{
		InitializeComponent();
		Chart.IndicatorAction += OnIndicatorAction;
	}

	private ChartViewModel? ViewModel => DataContext as ChartViewModel;

	private void OnIndicatorAction(string instanceId, ChartIndicatorAction action)
	{
		if (ViewModel is not { } viewModel)
			return;
		switch (action)
		{
			case ChartIndicatorAction.ToggleVisibility:
				viewModel.ToggleIndicator(instanceId);
				break;
			case ChartIndicatorAction.Remove:
				viewModel.RemoveIndicator(instanceId);
				break;
			case ChartIndicatorAction.OpenSettings:
				_ = OpenSettingsAsync(viewModel, instanceId);
				break;
		}
	}

	private async Task OpenSettingsAsync(ChartViewModel viewModel, string instanceId)
	{
		var (definition, instance) = viewModel.GetIndicator(instanceId);
		if (instance == null)
			return;
		// A script that is gone still opens, to say so
		definition ??= new IndicatorDefinitionDto
		{
			Id = instance.DefinitionId,
			Name = IndicatorHelpers.GetMissingLabel(instance.DefinitionId),
			Error = "This indicator is no longer available.",
		};
		var window = new IndicatorSettingsWindow(
			definition,
			instance,
			inputs => viewModel.UpdateIndicatorInputs(instanceId, inputs)
		);
		if (TopLevel.GetTopLevel(this) is Window owner)
			await window.ShowDialog(owner);
		else
			window.Show();
	}
}
