using LT.Contracts.Dtos.Settings;
using LT.Contracts.Enums;

namespace LT.Contracts.Helpers;

/// <summary>
/// Operations on the center split tree. Each returns the (possibly new) root; null means the
/// center is empty. A split always has two or more children and never directly contains a split
/// of the same orientation.
/// </summary>
public static class CenterLayoutHelpers
{
	public static CenterNodeDto Leaf(string widgetId) => new() { WidgetId = widgetId };

	public static IEnumerable<string> GetWidgetIds(CenterNodeDto? node) =>
		node is null ? []
		: node.IsLeaf ? [node.WidgetId!]
		: node.Children.SelectMany(GetWidgetIds);

	/// <summary>
	/// Places <paramref name="widgetId"/> on <paramref name="edge"/> of the leaf holding
	/// <paramref name="targetId"/>. Without a target (or an unknown one) it is added to the right
	/// of everything.
	/// </summary>
	public static CenterNodeDto Insert(
		CenterNodeDto? root,
		string widgetId,
		string? targetId = null,
		DropEdge edge = DropEdge.Right
	)
	{
		var leaf = Leaf(widgetId);
		if (root is null)
			return leaf;

		var orientation = edge is DropEdge.Left or DropEdge.Right
			? SplitOrientation.Horizontal
			: SplitOrientation.Vertical;
		var before = edge is DropEdge.Left or DropEdge.Top;

		var target = targetId is null ? null : Find(root, targetId);
		if (target is null)
		{
			target = root;
			orientation = SplitOrientation.Horizontal;
			before = false;
		}

		var parent = FindParent(root, target);
		if (parent?.Orientation == orientation)
		{
			// Join the existing row/column, taking half of the target's space
			leaf.Size = target.Size / 2;
			target.Size /= 2;
			var index = parent.Children.IndexOf(target);
			parent.Children.Insert(before ? index : index + 1, leaf);
			return root;
		}
		if (target == root && !root.IsLeaf && root.Orientation == orientation)
		{
			var size = root.Children.Average(x => x.Size);
			leaf.Size = size;
			root.Children.Insert(before ? 0 : root.Children.Count, leaf);
			return root;
		}

		// Split the target in two: it keeps its place, the new split takes its size
		var split = new CenterNodeDto
		{
			Orientation = orientation,
			Size = target.Size,
			Children = before ? [leaf, target] : [target, leaf],
		};
		target.Size = 1;
		leaf.Size = 1;
		if (parent is null)
			return split;
		parent.Children[parent.Children.IndexOf(target)] = split;
		return root;
	}

	/// <summary>Removes the widget's leaf, collapsing splits left with a single child.</summary>
	public static CenterNodeDto? Remove(CenterNodeDto? root, string widgetId)
	{
		if (root is null)
			return null;
		if (root.IsLeaf)
			return root.WidgetId == widgetId ? null : root;

		for (var i = 0; i < root.Children.Count; i++)
		{
			var child = Remove(root.Children[i], widgetId);
			if (child is null)
				root.Children.RemoveAt(i--);
			else
				root.Children[i] = child;
		}
		return Simplify(root);
	}

	/// <summary>
	/// Makes a saved tree consistent with the open center widgets: drops leaves of widgets that
	/// aren't in the center and adds center widgets missing from the tree (to the right).
	/// </summary>
	public static CenterNodeDto? Normalize(
		CenterNodeDto? root,
		IReadOnlyCollection<string> centerIds
	)
	{
		root = Prune(root, centerIds, []);
		var present = GetWidgetIds(root).ToHashSet();
		foreach (var id in centerIds.Where(x => !present.Contains(x)))
			root = Insert(root, id);
		return root;
	}

	private static CenterNodeDto? Prune(
		CenterNodeDto? node,
		IReadOnlyCollection<string> centerIds,
		HashSet<string> seen
	)
	{
		if (node is null)
			return null;
		if (node.IsLeaf)
			return centerIds.Contains(node.WidgetId!) && seen.Add(node.WidgetId!) ? node : null;

		node.Children = node
			.Children.Select(x => Prune(x, centerIds, seen))
			.OfType<CenterNodeDto>()
			.ToList();
		return Simplify(node);
	}

	// One child: the split disappears. Same-orientation child splits are merged into the parent.
	private static CenterNodeDto? Simplify(CenterNodeDto split)
	{
		if (split.Children.Count == 0)
			return null;
		if (split.Children.Count == 1)
		{
			var only = split.Children[0];
			only.Size = split.Size;
			return only;
		}

		var merged = new List<CenterNodeDto>();
		foreach (var child in split.Children)
		{
			if (!child.IsLeaf && child.Orientation == split.Orientation)
			{
				var total = child.Children.Sum(x => x.Size);
				foreach (var grandChild in child.Children)
				{
					grandChild.Size = child.Size * grandChild.Size / total;
					merged.Add(grandChild);
				}
			}
			else
				merged.Add(child);
		}
		split.Children = merged;
		return split;
	}

	private static CenterNodeDto? Find(CenterNodeDto node, string widgetId) =>
		node.IsLeaf
			? node.WidgetId == widgetId
				? node
				: null
			: node.Children.Select(x => Find(x, widgetId)).FirstOrDefault(x => x != null);

	private static CenterNodeDto? FindParent(CenterNodeDto node, CenterNodeDto child) =>
		node.IsLeaf ? null
		: node.Children.Contains(child) ? node
		: node.Children.Select(x => FindParent(x, child)).FirstOrDefault(x => x != null);
}
