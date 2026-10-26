using Avalonia.Controls;
using LT.App.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace LT.App.Widgets.PatchNotes;

/// <summary>Not modal: trading goes on while it is open. Refreshes the notes when it opens.</summary>
public partial class PatchNotesWindow : AppWindow
{
	public PatchNotesWindow() => InitializeComponent();

	/// <summary>Opens the patch notes over <paramref name="owner"/>.</summary>
	public static PatchNotesWindow Open(Window owner)
	{
		var viewModel = Program.Services.GetRequiredService<PatchNotesViewModel>();
		var window = new PatchNotesWindow { DataContext = viewModel };
		window.Closed += (_, _) => viewModel.Dispose();
		window.Show(owner);
		viewModel.RefreshCommand.Execute(null);
		return window;
	}
}
