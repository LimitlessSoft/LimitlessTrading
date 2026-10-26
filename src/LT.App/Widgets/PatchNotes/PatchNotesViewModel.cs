using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LT.App.Stores;
using LT.App.ViewModels;
using LT.Contracts.Dtos.PatchNotes;
using LT.Contracts.Enums;
using LT.Contracts.Interfaces.IManagers;

namespace LT.App.Widgets.PatchNotes;

/// <summary>
/// The patch notes window: every released version newest first, the running one marked. Shows
/// the cached notes at once and refreshes them from GitHub when it opens.
/// </summary>
public partial class PatchNotesViewModel : ViewModelBase, IDisposable
{
	private readonly IPatchNotesManager _patchNotesManager;
	private readonly DisplayTimeStore _displayTime;

	public PatchNotesViewModel(IPatchNotesManager patchNotesManager, DisplayTimeStore displayTime)
	{
		_patchNotesManager = patchNotesManager;
		_displayTime = displayTime;
		Apply(patchNotesManager.Status);
		patchNotesManager.StatusChanged += OnStatusChanged;
	}

	public ObservableCollection<PatchNotesReleaseItem> Releases { get; } = [];

	/// <summary>Loading, why the refresh failed, or that there is nothing to show; null otherwise.</summary>
	[ObservableProperty]
	public partial string? StatusText { get; set; }

	[ObservableProperty]
	public partial bool IsError { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
	public partial bool IsLoading { get; set; }

	[RelayCommand(CanExecute = nameof(CanRefresh))]
	private Task RefreshAsync() => _patchNotesManager.RefreshAsync();

	private bool CanRefresh() => !IsLoading;

	public void Dispose()
	{
		_patchNotesManager.StatusChanged -= OnStatusChanged;
		GC.SuppressFinalize(this);
	}

	private void OnStatusChanged(PatchNotesStatusDto status) =>
		Dispatcher.UIThread.Post(() => Apply(status));

	private void Apply(PatchNotesStatusDto status)
	{
		IsLoading = status.State == PatchNotesState.Loading;
		IsError = status.State == PatchNotesState.Failed;
		var hasReleases = status.Releases.Count > 0;
		StatusText = status.State switch
		{
			PatchNotesState.Loading when !hasReleases => "Loading the patch notes…",
			PatchNotesState.Failed when hasReleases =>
				$"{status.Error} Showing the notes saved earlier.",
			PatchNotesState.Failed => status.Error,
			PatchNotesState.Loaded when !hasReleases => "No releases yet.",
			_ => null,
		};

		// Rebuilt only when the list changed, so a refresh with nothing new keeps the scroll place
		var items = status
			.Releases.Select(x => new PatchNotesReleaseItem(x, _displayTime))
			.ToList();
		if (items.SequenceEqual(Releases))
			return;
		Releases.Clear();
		foreach (var item in items)
			Releases.Add(item);
	}
}

/// <summary>One version in the list.</summary>
public record PatchNotesReleaseItem
{
	public PatchNotesReleaseItem(PatchNotesReleaseDto release, DisplayTimeStore displayTime)
	{
		Version = release.Version;
		DateText = release.PublishedAt is { } published
			? displayTime.Format(published, "d MMM yyyy")
			: string.Empty;
		IsCurrent = release.IsCurrent;
		PageUri = Uri.TryCreate(release.PageUrl, UriKind.Absolute, out var page) ? page : null;
		Entries = release.Entries.Select(x => new PatchNoteEntryItem(x)).ToList();
	}

	public string Version { get; }
	public string DateText { get; }

	/// <summary>The version running now ("You're here").</summary>
	public bool IsCurrent { get; }
	public Uri? PageUri { get; }
	public IReadOnlyList<PatchNoteEntryItem> Entries { get; }
	public bool HasNoEntries => Entries.Count == 0;

	public virtual bool Equals(PatchNotesReleaseItem? other) =>
		other is not null
		&& Version == other.Version
		&& DateText == other.DateText
		&& IsCurrent == other.IsCurrent
		&& PageUri == other.PageUri
		&& Entries.SequenceEqual(other.Entries);

	public override int GetHashCode() => HashCode.Combine(Version, DateText, IsCurrent);
}

/// <summary>One change; its pull request opens in the browser.</summary>
public record PatchNoteEntryItem
{
	public PatchNoteEntryItem(PatchNoteEntryDto entry)
	{
		Text = entry.Text;
		PullRequestText = entry.PullRequestNumber is { } number ? $"#{number}" : null;
		PullRequestUri =
			entry.PullRequestUrl != null
			&& Uri.TryCreate(entry.PullRequestUrl, UriKind.Absolute, out var uri)
				? uri
				: null;
	}

	public string Text { get; }
	public string? PullRequestText { get; }
	public Uri? PullRequestUri { get; }
}
