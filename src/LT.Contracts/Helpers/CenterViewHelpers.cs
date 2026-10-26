using LT.Contracts.Constants;
using LT.Contracts.Dtos.Settings;

namespace LT.Contracts.Helpers;

/// <summary>
/// The center's views (tabs): each owns a split tree, and every center widget sits in exactly
/// one of them.
/// </summary>
public static class CenterViewHelpers
{
	public static string NewId() => Guid.NewGuid().ToString("N");

	/// <summary>
	/// Makes saved views consistent with the open center widgets. No views (a layout saved before
	/// views) gives one, "Main", holding <paramref name="legacyCenter"/>. Ids are made unique and
	/// names non-blank; a widget's leaf is kept in the first view holding it, leaves of widgets
	/// not in the center are dropped, and center widgets in no view go to the selected one.
	/// Always returns at least one view.
	/// </summary>
	public static List<CenterViewDto> Normalize(
		IReadOnlyList<CenterViewDto>? views,
		CenterNodeDto? legacyCenter,
		string? selectedId,
		IReadOnlyCollection<string> centerIds
	)
	{
		var result = views is { Count: > 0 }
			? views.ToList()
			:
			[
				new CenterViewDto
				{
					Id = NewId(),
					Name = WorkspaceConstants.DefaultViewName,
					Root = legacyCenter,
				},
			];

		var ids = new HashSet<string>();
		var seen = new HashSet<string>();
		foreach (var view in result)
		{
			if (string.IsNullOrEmpty(view.Id) || !ids.Add(view.Id))
				ids.Add(view.Id = NewId());
			view.Name = CleanName(view.Name) ?? GetNewName(result.Select(x => x.Name));
			view.Root = CenterLayoutHelpers.Prune(view.Root, centerIds, seen);
		}

		var target = result.Find(x => x.Id == selectedId) ?? result[0];
		foreach (var id in centerIds.Where(x => !seen.Contains(x)))
			target.Root = CenterLayoutHelpers.Insert(target.Root, id);
		return result;
	}

	/// <summary>The selected view's id; an unknown or missing one falls back to the first view.</summary>
	public static string GetSelectedId(IReadOnlyList<CenterViewDto> views, string? selectedId) =>
		views.Any(x => x.Id == selectedId) ? selectedId! : views[0].Id;

	/// <summary>The first free "View N" (N from 2: the first view is "Main").</summary>
	public static string GetNewName(IEnumerable<string> existing)
	{
		var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
		for (var n = 2; ; n++)
		{
			var name = $"{WorkspaceConstants.NewViewNamePrefix} {n}";
			if (!names.Contains(name))
				return name;
		}
	}

	/// <summary>
	/// Name of a copy of view <paramref name="sourceName"/>: "&lt;name&gt; copy", or the first free
	/// "&lt;name&gt; copy N" (N from 2). The source name is cut so the suffix always fits the limit.
	/// </summary>
	public static string GetDuplicateName(string sourceName, IEnumerable<string> existing)
	{
		var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var source = CleanName(sourceName) ?? WorkspaceConstants.DefaultViewName;
		for (var n = 1; ; n++)
		{
			var suffix =
				n == 1
					? $" {WorkspaceConstants.DuplicateViewNameSuffix}"
					: $" {WorkspaceConstants.DuplicateViewNameSuffix} {n}";
			var room = WorkspaceConstants.ViewNameMaxLength - suffix.Length;
			var name = CleanName(
				(source.Length > room ? source[..room].TrimEnd() : source) + suffix
			)!;
			if (!names.Contains(name))
				return name;
		}
	}

	/// <summary>
	/// A copy of a view: a new id, <see cref="GetDuplicateName"/> and the source's split tree with
	/// each widget swapped for its copy (<paramref name="widgetIds"/>: source id → copy id).
	/// Widgets without a copy (single-instance ones) are left out of the tree.
	/// </summary>
	public static CenterViewDto Duplicate(
		string sourceName,
		CenterNodeDto? sourceRoot,
		IReadOnlyDictionary<string, string> widgetIds,
		IEnumerable<string> existingNames
	) =>
		new()
		{
			Id = NewId(),
			Name = GetDuplicateName(sourceName, existingNames),
			Root = CenterLayoutHelpers.Copy(sourceRoot, widgetIds),
		};

	/// <summary>A typed view name trimmed and cut to the limit; null when blank (keep the old one).</summary>
	public static string? CleanName(string? name)
	{
		var trimmed = name?.Trim();
		if (string.IsNullOrEmpty(trimmed))
			return null;
		return trimmed.Length > WorkspaceConstants.ViewNameMaxLength
			? trimmed[..WorkspaceConstants.ViewNameMaxLength].TrimEnd()
			: trimmed;
	}

	/// <summary>Index <paramref name="step"/> tabs away, wrapping around both ends (Ctrl+Tab).</summary>
	public static int Cycle(int index, int count, int step) =>
		count <= 0 ? 0 : ((index + step) % count + count) % count;

	/// <summary>The tab selected after closing the one at <paramref name="closedIndex"/>: the next, else the previous.</summary>
	public static int GetIndexAfterClose(int closedIndex, int remainingCount) =>
		Math.Clamp(closedIndex, 0, Math.Max(remainingCount - 1, 0));
}
