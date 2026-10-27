// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace Cxdbg.Session;

/// <summary>
/// The target's control tree, kept in step with each poll.
/// </summary>
/// <remarks>
/// <para>
/// Merges a fresh <c>tree</c> reply into the existing nodes rather than
/// replacing them: a node that is still there keeps its object, so the bound
/// rows keep their selection and expansion, and only what actually changed
/// moves.
/// </para>
/// <para>
/// Identity is the control id, which is a name or <c>Type#hash</c> - stable
/// across polls and not positional, so inserting a sibling above a control
/// does not silently re-point a selection at a different one.
/// </para>
/// </remarks>
internal sealed class ControlTreeModel
{
	/// <summary>The windows, each the root of its own subtree.</summary>
	internal ObservableCollection<ControlNode> Roots { get; } = new();

	/// <summary>Every node by id, for the panes that select by id.</summary>
	private readonly Dictionary<string, ControlNode> _byId = new(StringComparer.Ordinal);

	/// <summary>The first node of this control type, if the tree has one.</summary>
	/// <remarks>
	/// By type, because <c>reachable</c> reports bare type names while the tree
	/// uses ids. Ambiguous when an app has two controls of a type both in the
	/// Tab order, which is a limit of what the reply carries rather than of
	/// this lookup.
	/// </remarks>
	internal ControlNode? FindByType(string type)
	{
		foreach (var node in All(Roots))
		{
			if (string.Equals(node.Type, type, StringComparison.Ordinal)) return node;
		}

		return null;
	}

	private static IEnumerable<ControlNode> All(IEnumerable<ControlNode> nodes)
	{
		foreach (var node in nodes)
		{
			yield return node;
			foreach (var child in All(node.Children)) yield return child;
		}
	}

	/// <summary>The node with this id, if the tree still has one.</summary>
	internal ControlNode? Find(string id) =>
		_byId.TryGetValue(id, out var node) ? node : null;

	/// <summary>Takes a fresh tree reply.</summary>
	/// <param name="reply">The <c>tree</c> reply.</param>
	/// <remarks>
	/// The desktop is the root, because that is what the tree actually is: a
	/// screen with windows on it, each with controls in it. Starting at the
	/// windows threw away the desktop's own facts - its theme, its panel rows,
	/// whether anything modal is open - which are exactly the things you want
	/// when a window is not where you expected.
	/// </remarks>
	internal void Merge(JsonNode? reply)
	{
		var windows = reply?["windows"]?.AsArray();
		if (windows == null) return;

		_byId.Clear();

		if (reply?["desktop"] is not JsonObject desktop) return;

		// One root, always the desktop, so its row is never removed and
		// re-added and the tree never loses its shape at the top.
		if (Roots.Count == 0)
		{
			Roots.Add(new ControlNode("desktop", "desktop", "desktop", desktop,
				ControlNode.NodeKind.Desktop));
		}

		var root = Roots[0];
		root.UpdateDesktop(desktop);
		_byId["desktop"] = root;

		var wanted = new List<(string Key, string Type, JsonObject? Node)>();

		foreach (var w in windows)
		{
			if (w is not JsonObject window) continue;

			string title = window["title"]?.GetValue<string>() ?? "window";

			// A window's own guid, so a window keeps its row across polls even
			// when another opens above it or its title changes.
			string key = window["id"]?.GetValue<string>() ?? title;

			wanted.Add((key, title, window));
		}

		MergeLevel(root.Children, wanted, ControlNode.NodeKind.Window);

		for (int i = 0; i < root.Children.Count && i < wanted.Count; i++)
		{
			if (wanted[i].Node is not { } window) continue;

			root.Children[i].UpdateWindow(window);

			if (window["node"] is JsonObject node)
				MergeChildren(root.Children[i], node);
		}
	}

	/// <summary>Merges one node's children into its collection.</summary>
	private void MergeChildren(ControlNode parent, JsonObject node)
	{
		var wanted = new List<(string Key, string Type, JsonObject? Node)>();

		foreach (var child in node["children"]?.AsArray() ?? [])
		{
			if (child is not JsonObject c) continue;

			string type = c["control"]?.GetValue<string>() ?? "?";
			string id = c["id"]?.GetValue<string>() ?? "";

			// Scaffolding has no id, so it gets a key from its type and
			// position. That is positional and would re-point under a
			// reshuffle - acceptable only because nothing can be asked about
			// a node with no id anyway.
			string key = id.Length > 0 ? id : $"{type}@{wanted.Count}";

			wanted.Add((key, type, c));
		}

		MergeLevel(parent.Children, wanted, ControlNode.NodeKind.Control);

		for (int i = 0; i < parent.Children.Count && i < wanted.Count; i++)
		{
			if (wanted[i].Node is { } childNode)
				MergeChildren(parent.Children[i], childNode);
		}
	}

	/// <summary>
	/// Makes one collection match a wanted list, moving rather than rebuilding.
	/// </summary>
	private void MergeLevel(
		ObservableCollection<ControlNode> have,
		List<(string Key, string Type, JsonObject? Node)> wanted,
		ControlNode.NodeKind kind)
	{
		// Anything gone, removed first, so the indices below line up. Keys are
		// the nodes' own, assigned at creation, never recomputed from a
		// position that may since have moved.
		for (int i = have.Count - 1; i >= 0; i--)
		{
			if (!wanted.Any(x => x.Key == have[i].Key)) have.RemoveAt(i);
		}

		for (int i = 0; i < wanted.Count; i++)
		{
			var (key, type, node) = wanted[i];

			int at = -1;
			for (int j = i; j < have.Count; j++)
			{
				if (have[j].Key == key) { at = j; break; }
			}

			if (at < 0)
			{
				// A window's id is its title, so the panes can address it; a
				// control's is the server's own control id.
				string id = kind == ControlNode.NodeKind.Window
					? type
					: node?["id"]?.GetValue<string>() ?? "";

				var fresh = new ControlNode(id, type, key, node ?? new JsonObject(), kind);

				if (node != null && kind == ControlNode.NodeKind.Control) fresh.Update(node);
				have.Insert(Math.Min(i, have.Count), fresh);
			}
			else
			{
				// Still there: keep the object, so its row keeps selection and
				// expansion, and only move it if it has actually shifted.
				if (at != i) have.Move(at, i);
				if (node != null && kind == ControlNode.NodeKind.Control) have[i].Update(node);
			}

			if (have[i].Id.Length > 0) _byId[have[i].Id] = have[i];
		}
	}
}
