// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.Json.Nodes;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace Cxdbg.Session;

/// <summary>
/// The session window: the target's tree, its screen, and what it is doing.
/// </summary>
/// <remarks>
/// <para>
/// Built with SharpConsoleUI, against a SharpConsoleUI application. The panes
/// are docked rather than placed, so a narrow terminal drops the side panes
/// and keeps the tree - which is what you navigate with.
/// </para>
/// </remarks>
internal sealed class DebuggerWindow
{
	private readonly ConsoleWindowSystem _system;
	private readonly TargetPoller _poller;

	private readonly MarkupControl _header = new(["attaching..."]);
	private readonly MarkupControl _tree = new([""]);
	private readonly MarkupControl _detail = new(["nothing selected"]);

	private int _selected;
	private List<string> _ids = [];

	internal DebuggerWindow(ConsoleWindowSystem system, TargetPoller poller)
	{
		_system = system;
		_poller = poller;
	}

	/// <summary>Builds the window and starts following the target.</summary>
	internal Window Show()
	{
		var window = new WindowBuilder(_system)
			.WithTitle("cxdbg")
			.Maximized()
			.Borderless()
			.OnKeyPressed(OnKey)
			.BuildAndShow();

		// Top first: a dock child claims its whole edge before the ones after
		// it narrow what is left.
		window.DockTop.Add(_header);
		window.DockRight.Add(Detail());
		window.DockBody.Add(Tree());

		_poller.Changed += OnTargetChanged;
		Render(_poller.State);

		return window;
	}

	private IWindowControl Tree() =>
		Controls.ScrollablePanel()
			.WithName("tree")
			.WithScrollbar()
			.WithMouseWheel()
			.AddControl(_tree)
			.Build();

	private IWindowControl Detail()
	{
		var panel = Controls.Panel()
			.WithHeader("selected")
			.WithWidth(44)
			.AddControl(_detail)
			.Build();

		return panel;
	}

	/// <summary>
	/// Handles the session's own keys.
	/// </summary>
	/// <remarks>
	/// Deliberately few. This drives a debugger, not an editor, and every key
	/// here is one that cannot be expressed as a question from the shell.
	/// </remarks>
	private void OnKey(object? sender, KeyPressedEventArgs e)
	{
		switch (e.KeyInfo.Key)
		{
			case ConsoleKey.DownArrow: e.Handled = Move(+1); return;
			case ConsoleKey.UpArrow: e.Handled = Move(-1); return;
		}

		switch (char.ToLowerInvariant(e.KeyInfo.KeyChar))
		{
			case 'f':
				// Freeze: stop polling so the picture holds still while you
				// read it. The target carries on; only the looking stops.
				_poller.Frozen = !_poller.Frozen;
				Render(_poller.State);
				e.Handled = true;
				return;

			case 'q':
				_system.Shutdown();
				e.Handled = true;
				return;
		}
	}

	private bool Move(int by)
	{
		if (_ids.Count == 0) return true;

		_selected = Math.Clamp(_selected + by, 0, _ids.Count - 1);
		Render(_poller.State);
		return true;
	}

	private void OnTargetChanged(TargetState state) =>
		_system.EnqueueOnUIThread(() => Render(state));

	private void Render(TargetState state)
	{
		RenderHeader(state);
		RenderTree(state);
		RenderDetail(state);
	}

	private void RenderHeader(TargetState state)
	{
		var parts = new List<string> { $"[bold]cxdbg[/]  frame {state.Frame}" };

		if (state.Gone) parts.Add("[red]the application is gone[/]");
		else if (_poller.Frozen) parts.Add("[yellow]frozen[/]");

		parts.Add("[dim]up/down select  f freeze  q quit[/]");

		_header.SetContent([string.Join("   ", parts)]);
	}

	private void RenderTree(TargetState state)
	{
		if (state.Tree == null)
		{
			_tree.SetContent(["[dim]waiting for the application[/]"]);
			return;
		}

		var lines = new List<string>();
		var ids = new List<string>();

		// The reply is a window per entry, each with one layout node at its
		// root. Read from the schema the server actually sends, not a guess at
		// it: the first version of this walked "root", which does not exist,
		// and reported "no controls" against a perfectly healthy app.
		foreach (var window in state.Tree["windows"]?.AsArray() ?? [])
		{
			string title = window?["title"]?.GetValue<string>() ?? "window";
			lines.Add($"[bold]{title.EscapeMarkupBraces()}[/]");
			ids.Add("");

			Walk(window?["node"], 1, lines, ids);
		}

		_ids = ids;
		if (_selected >= _ids.Count) _selected = Math.Max(0, _ids.Count - 1);

		// Mark the selection in the text itself: the pane is markup, and a
		// cursor the reader cannot see is not a selection.
		for (int i = 0; i < lines.Count && i < _ids.Count; i++)
		{
			if (i == _selected) lines[i] = "[invert]" + lines[i] + "[/]";
		}

		_tree.SetContent(lines.Count == 0 ? ["[dim]no controls[/]"] : lines);
	}

	private static void Walk(JsonNode? node, int depth, List<string> lines, List<string> ids)
	{
		if (node == null) return;

		if (node is JsonArray array)
		{
			foreach (var child in array) Walk(child, depth, lines, ids);
			return;
		}

		// A control's id is what every other verb takes; its type is what a
		// reader recognises. Nodes without an id are layout scaffolding, shown
		// because the shape matters, but not selectable.
		string type = node["control"]?.GetValue<string>() ?? "?";
		string id = node["id"]?.GetValue<string>() ?? "";
		var bounds = node["bounds"];

		var line = new StringBuilder();
		line.Append(new string(' ', depth * 2));
		line.Append(type.EscapeMarkupBraces());

		if (bounds != null)
		{
			int w = bounds["w"]?.GetValue<int>() ?? -1;
			int h = bounds["h"]?.GetValue<int>() ?? -1;
			line.Append($"  [dim]{w}x{h}[/]");

			// The pattern tmux can never show you: present in the tree,
			// reachable by focus, and painting nothing.
			if (w == 0 || h == 0) line.Append("  [red]invisible[/]");
		}

		lines.Add(line.ToString());
		ids.Add(id);

		Walk(node["children"], depth + 1, lines, ids);
	}

	private void RenderDetail(TargetState state)
	{
		if (_ids.Count == 0 || _selected >= _ids.Count)
		{
			_detail.SetContent(["[dim]nothing selected[/]"]);
			return;
		}

		string id = _ids[_selected];
		var lines = new List<string> { $"[bold]{id.EscapeMarkupBraces()}[/]", "" };

		// A cascade is asked for on SELECTION, never polled: measure forces a
		// relayout, and polling it would make the debugger the cause of the
		// churn it reports.
		var measure = _poller.AskNow("measure", new Dictionary<string, JsonNode?> { ["control"] = id });

		if (measure?["cascade"] is JsonArray cascade && cascade.Count > 0)
		{
			lines.Add("[bold]why this size[/]");
			foreach (var link in cascade)
			{
				string who = link?["container"]?.GetValue<string>() ?? "?";
				string offered = $"{link?["maxWidth"]} x {link?["maxHeight"]}";
				bool deciding = link?["deciding"]?.GetValue<bool>() == true;

				lines.Add((deciding ? "[yellow]-> " : "   ") + $"{who.EscapeMarkupBraces()}  {offered}" + (deciding ? "[/]" : ""));
			}
		}
		else
		{
			lines.Add("[dim]no cascade for this control[/]");
		}

		_detail.SetContent(lines);
	}
}

/// <summary>Keeps control ids from being read as markup.</summary>
internal static class MarkupSafety
{
	/// <summary>Escapes the one character markup treats specially.</summary>
	/// <remarks>
	/// A control's id can contain anything its author typed into Name. An id
	/// with a bracket in it would otherwise open a tag and swallow the rest of
	/// the line - the debugger misreporting the thing it exists to report.
	/// </remarks>
	internal static string EscapeMarkupBraces(this string text) =>
		text.Replace("[", "[[");
}
