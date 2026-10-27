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
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace Cxdbg.Session;

/// <summary>
/// The session window: a tree, what is known about the selection, and why.
/// </summary>
/// <remarks>
/// <para>
/// Tree on the left, always. Two tabs on the right answering "what is this
/// control", a docked tab strip below answering "why". Two independent axes:
/// you can hold a detail view and change the question underneath it, or hold a
/// question and flip between the facts and the picture.
/// </para>
/// <para>
/// The screen tab is a tab rather than a third column because a permanent one
/// spent a third of the width showing an unreadable thumbnail. As a tab it
/// costs nothing when unused and gets the full pane when wanted.
/// </para>
/// </remarks>
internal sealed class DebuggerWindow
{
	private readonly ConsoleWindowSystem _system;
	private readonly TargetPoller _poller;

	private readonly MarkupControl _header = new(["attaching..."]);
	private readonly MarkupControl _tree = new([""]);
	private readonly MarkupControl _details = new(["nothing selected"]);
	private readonly MarkupControl _size = new([""]);
	private readonly MarkupControl _cells = new([""]);
	private readonly SurfaceViewControl _screen = new();
	private readonly MarkupControl _screenWhere = new(["[dim]no selection[/]"]);

	private ScrollablePanelControl _treeScroller = null!;

	/// <summary>The screen row the cells dock is reading.</summary>
	private int _row;

	private TabControl _right = null!;
	private TabControl _dock = null!;

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
			.BuildAndShow();

		window.KeyPressed += OnKey;

		// Top first: a dock child claims its whole edge before the ones after
		// it narrow what is left. Bottom next, so the right pane is the height
		// that is actually left over.
		// The header docks; everything else is a grid with draggable
		// boundaries. Fixed pane sizes were the wrong answer - an 8-row dock
		// on a short terminal leaves the tree one row, and a breakpoint that
		// drops a pane is me guessing which one you wanted. Splitters let you
		// decide, and the grid already knows how to carry them.
		window.DockTop.Add(_header);
		window.DockBody.Add(Body());

		_poller.Changed += OnTargetChanged;
		Render(_poller.State);

		return window;
	}

	/// <summary>
	/// Tree beside the right pane, dock underneath, every boundary draggable.
	/// </summary>
	/// <remarks>
	/// Two star columns at 2:1 and two star rows at 2:1 to start, so nothing
	/// is fixed and nothing is starved at any size. The splitters are Tab
	/// stops as well as drag targets, so the layout is reachable from the
	/// keyboard too.
	/// </remarks>
	private IWindowControl Body() =>
		Controls.Grid()
			// Fill and Stretch, or the grid sizes to its content: star rows
			// dividing one row give every pane a height of zero, which the
			// tree reports as "GridControl got 118x1 wants 118x1" inside a
			// 35-row body. Found by attaching cxdbg to itself.
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Columns(GridLength.Star(2), GridLength.Star(1))
			.Rows(GridLength.Star(2), GridLength.Star(1))
			.ColumnSplitterAfter(0)
			.RowSplitterAfter(0)
			.Place(Tree(), 0, 0)
			.Place(Right(), 0, 1)
			.Place(Dock(), 1, 0, rowSpan: 1, colSpan: 2)
			.Build();

	private IWindowControl Tree()
	{
		_treeScroller = Controls.ScrollablePanel()
			.WithName("tree")
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_tree)
			.Build();

		return _treeScroller;
	}

	/// <summary>The right pane: what this control is, or what it looks like.</summary>
	private IWindowControl Right()
	{
		_right = new TabControl
		{
			Name = "right",
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};

		_right.AddTab("1 details", Controls.ScrollablePanel()
			.WithScrollbar()
			.AddControl(_details)
			.Build());

		// The screen tab repaints the target's own cells, so what appears here
		// is the application's screen rather than a description of it, with
		// one line saying where the selection sits in it.
		var screenPane = Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.AddControl(_screenWhere)
			.AddControl(_screen)
			.Build();

		_right.AddTab("2 screen", screenPane);

		return _right;
	}

	/// <summary>The dock: why the selection is the way it is.</summary>
	private IWindowControl Dock()
	{
		_dock = new TabControl
		{
			Name = "dock",
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};

		_dock.AddTab("1 size", Controls.ScrollablePanel()
			.WithScrollbar()
			.AddControl(_size)
			.Build());

		// Cells is the question text cannot answer, docked where you are
		// already looking rather than in a mode of its own. The screen tab
		// drives it: pick a row up there, read its columns down here.
		_dock.AddTab("3 cells", Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.AddControl(_cells)
			.Build());

		return _dock;
	}

	/// <summary>
	/// The session's own keys.
	/// </summary>
	/// <remarks>
	/// Digits move the right pane, function keys the dock. Two tab strips on
	/// one screen is this layout's real risk, so the two take visibly
	/// different classes of key rather than sharing one and hoping focus says
	/// which is which.
	/// </remarks>
	private void OnKey(object? sender, KeyPressedEventArgs e)
	{
		switch (e.KeyInfo.Key)
		{
			case ConsoleKey.DownArrow: e.Handled = Move(+1); return;
			case ConsoleKey.UpArrow: e.Handled = Move(-1); return;
			case ConsoleKey.F1: e.Handled = SelectDock(0); return;
			case ConsoleKey.F3: e.Handled = SelectDock(1); return;
		}

		switch (e.KeyInfo.KeyChar)
		{
			case '1': e.Handled = SelectRight(0); return;
			case '2': e.Handled = SelectRight(1); return;
		}

		switch (e.KeyInfo.KeyChar)
		{
			// The row the cells dock reads. Square brackets because they sit
			// next to each other and read as "move the window along".
			case '[': e.Handled = MoveRow(-1); return;
			case ']': e.Handled = MoveRow(+1); return;
		}

		switch (char.ToLowerInvariant(e.KeyInfo.KeyChar))
		{
			case 'f':
				// Freeze: the target carries on, only the looking stops.
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

	private bool SelectRight(int index)
	{
		if (index < 0 || index >= 2) return false;

		_right.ActiveTabIndex = index;
		Render(_poller.State);
		return true;
	}

	private bool SelectDock(int index)
	{
		if (index < 0 || index >= 2) return false;

		_dock.ActiveTabIndex = index;
		Render(_poller.State);
		return true;
	}

	private bool MoveRow(int by)
	{
		int height = Math.Max(1, _screen.SurfaceHeight);
		_row = Math.Clamp(_row + by, 0, height - 1);

		RenderCells();
		return true;
	}

	private bool Move(int by)
	{
		if (_ids.Count == 0) return true;

		int next = Math.Clamp(_selected + by, 0, _ids.Count - 1);
		if (next == _selected) return true;

		_selected = next;

		// The cascade is asked for on SELECTION, never on the poll: measure
		// forces a relayout, so polling it would make the debugger the cause
		// of the churn it reports.
		Render(_poller.State);
		ShowSelection();
		return true;
	}

	/// <summary>
	/// Scrolls the tree so the selected row is on screen.
	/// </summary>
	/// <remarks>
	/// A highlight that moves out of view leaves you steering something you
	/// cannot see. The tree is one markup control inside a scroller, so the
	/// row is a region of that child rather than a child of its own.
	/// </remarks>
	private void ShowSelection() =>
		_treeScroller.ScrollChildRegionIntoView(_tree, _selected, 1);

	private void OnTargetChanged(TargetState state) =>
		_system.EnqueueOnUIThread(() => Render(state));

	private void Render(TargetState state)
	{
		RenderHeader(state);
		RenderTree(state);
		RenderDetails(state);
		RenderSize();
		RenderScreen(state);
		RenderCells();
	}

	private void RenderHeader(TargetState state)
	{
		var parts = new List<string>
		{
			$"[bold]cxdbg[/]  pid {_poller.Pid}  frame {state.Frame}",
		};

		if (state.Gone) parts.Add("[red]the application is gone[/]");
		else if (_poller.Frozen) parts.Add("[yellow]frozen[/]");

		parts.Add("[dim]up/down select   1 2 pane   F1 dock   f freeze   q quit[/]");

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

		foreach (var window in state.Tree["windows"]?.AsArray() ?? [])
		{
			string title = window?["title"]?.GetValue<string>() ?? "window";
			lines.Add($"[bold]{Safe(title)}[/]");
			ids.Add("");

			Walk(window?["node"], 1, lines, ids);
		}

		_ids = ids;
		if (_selected >= _ids.Count) _selected = Math.Max(0, _ids.Count - 1);

		for (int i = 0; i < lines.Count && i < _ids.Count; i++)
		{
			if (i == _selected) lines[i] = "[invert]" + lines[i] + "[/]";
		}

		_tree.SetContent(lines.Count == 0 ? ["[dim]no controls[/]"] : lines);
	}

	private static void Walk(JsonNode? node, int depth, List<string> lines, List<string> ids)
	{
		if (node == null) return;

		string type = node["control"]?.GetValue<string>() ?? "?";
		string id = node["id"]?.GetValue<string>() ?? "";
		var bounds = node["bounds"];
		var desired = node["desired"];

		var line = new StringBuilder();
		line.Append(new string(' ', depth * 2));
		line.Append(Safe(type));

		if (bounds != null)
		{
			int w = bounds["w"]?.GetValue<int>() ?? -1;
			int h = bounds["h"]?.GetValue<int>() ?? -1;
			line.Append($"  [dim]{w}x{h}[/]");

			// What it wanted, only when it differs from what it got. Most
			// controls got what they asked for, and saying so for all of them
			// buries the ones that did not.
			int dw = desired?["w"]?.GetValue<int>() ?? w;
			int dh = desired?["h"]?.GetValue<int>() ?? h;
			if (dw != w || dh != h) line.Append($"  [yellow]wants {dw}x{dh}[/]");

			// Present in the tree, reachable by focus, painting nothing: the
			// pattern no other tool shows you.
			if (w == 0 || h == 0) line.Append("  [red]invisible[/]");
		}

		lines.Add(line.ToString());
		ids.Add(id);

		foreach (var child in node["children"]?.AsArray() ?? [])
			Walk(child, depth + 1, lines, ids);
	}

	private void RenderDetails(TargetState state)
	{
		var node = SelectedNode(state);
		if (node == null)
		{
			_details.SetContent(["[dim]nothing selected[/]"]);
			return;
		}

		var lines = new List<string>
		{
			$"[bold]{Safe(node["id"]?.GetValue<string>() ?? node["control"]?.GetValue<string>() ?? "?")}[/]",
			"",
		};

		var bounds = node["bounds"];
		var desired = node["desired"];

		lines.Add($"wants       {desired?["w"]} x {desired?["h"]}");
		lines.Add($"got         {bounds?["w"]} x {bounds?["h"]}");

		if ((bounds?["w"]?.GetValue<int>() ?? 1) == 0 || (bounds?["h"]?.GetValue<int>() ?? 1) == 0)
			lines.Add("            [red]invisible[/]");

		lines.Add($"layout      {Safe(node["layout"]?.GetValue<string>() ?? "-")}");
		lines.Add($"visible     {node["visible"]}");

		// Whatever this control type reports about itself, in the shared
		// vocabulary: the state names are the library's, not cxdbg's.
		if (node["state"] is JsonObject s && s.Count > 0)
		{
			lines.Add("");
			foreach (var (key, value) in s)
				lines.Add($"{key,-11} {Safe(value?.ToString() ?? "")}");
		}

		string? focused = state.Focus?["focused"]?.GetValue<string>();
		if (focused != null)
		{
			lines.Add("");
			lines.Add($"focus is on {Safe(focused)}");
		}

		_details.SetContent(lines);
	}

	private void RenderSize()
	{
		if (_ids.Count == 0 || _selected >= _ids.Count || _ids[_selected].Length == 0)
		{
			_size.SetContent(["[dim]select a control with an id[/]"]);
			return;
		}

		string id = _ids[_selected];
		var reply = _poller.AskNow("measure", new Dictionary<string, JsonNode?> { ["control"] = id });

		var controls = reply?["controls"]?.AsArray();
		if (controls == null || controls.Count == 0)
		{
			_size.SetContent([$"[dim]no cascade for {Safe(id)}[/]"]);
			return;
		}

		var lines = new List<string>();

		foreach (var c in controls)
		{
			lines.Add($"[bold]{Safe(c?["control"]?.GetValue<string>() ?? id)}[/]"
				+ $"   wants {c?["desiredWidth"]}x{c?["desiredHeight"]}"
				+ $"   offered {c?["offeredWidth"]}x{c?["offeredHeight"]}");

			foreach (var link in c?["cascade"]?.AsArray() ?? [])
			{
				string who = link?["control"]?.GetValue<string>()
					?? link?["layout"]?.GetValue<string>() ?? "?";

				// setsWidth / setsHeight is the SERVER saying which link
				// decided. cxdbg does not work it out from the numbers.
				bool setsW = link?["setsWidth"]?.GetValue<bool>() == true;
				bool setsH = link?["setsHeight"]?.GetValue<bool>() == true;

				string row = $"  {Safe(who),-24} offered "
					+ $"{link?["offeredWidth"],4} x {link?["offeredHeight"],-4}";

				if (setsW || setsH)
				{
					string which = setsW && setsH ? "size" : setsW ? "width" : "height";
					lines.Add($"[yellow]{row}   decided the {which}[/]");
				}
				else
				{
					lines.Add(row);
				}
			}
		}

		_size.SetContent(lines);
	}

	/// <summary>
	/// What is in each column of one screen row.
	/// </summary>
	/// <remarks>
	/// Read from the cells the pane is already holding, so the dock costs no
	/// extra round trip and always agrees with the picture above it. A
	/// continuation cell says so rather than repeating the glyph before it,
	/// which is the whole reason to look here.
	/// </remarks>
	private void RenderCells()
	{
		var screen = _poller.State.Screen;

		if (screen.Count == 0)
		{
			_cells.SetContent(["[dim]waiting for the application[/]"]);
			return;
		}

		int row = Math.Clamp(_row, 0, screen.Count - 1);
		var cells = screen[row];

		var lines = new List<string>
		{
			$"[bold]row {row}[/]   {cells.Count} columns"
				+ "   [dim][[ ]] moves the row[/]",
			"",
		};

		int wide = 0;
		int glyphs = 0;

		foreach (var c in cells)
		{
			if (c.IsWideContinuation) continue;
			glyphs++;
			if (UnicodeWidth.GetRuneWidth(c.Character) == 2) wide++;
		}

		for (int col = 0; col < cells.Count; col++)
		{
			var c = cells[col];

			if (c.IsWideContinuation)
			{
				lines.Add($"{col,5}         [dim]<- the column before this one is two wide[/]");
				continue;
			}

			int w = UnicodeWidth.GetRuneWidth(c.Character);
			string ch = c.Character.Value == ' ' ? "[dim]' '[/]" : Safe(c.Character.ToString());

			var line = new StringBuilder();
			line.Append($"{col,5}   {ch}   [dim]width {w}[/]");

			if (c.Combiners is { Length: > 0 } comb)
				line.Append($"   [yellow]+{comb.Length} combining[/]");

			if (c.Decorations != TextDecoration.None)
				line.Append($"   [dim]{c.Decorations.ToString().ToLowerInvariant()}[/]");

			lines.Add(line.ToString());
		}

        lines.Add("");
		lines.Add(wide > 0
			? $"[yellow]{glyphs} glyphs in {cells.Count} columns: {wide} wide, taking {wide * 2}[/]"
			: $"[dim]{glyphs} glyphs in {cells.Count} columns[/]");

		_cells.SetContent(lines);
	}

	/// <summary>
	/// Shows the target's own screen.
	/// </summary>
	/// <remarks>
	/// The cells arrive as cells and are painted as cells, so the pane shows
	/// the application rather than a description of it. Nothing is serialised
	/// as text in between, which is why there is nothing here to go wrong.
	/// </remarks>
	private void RenderScreen(TargetState state)
	{
		if (state.Screen.Count > 0) _screen.SetSurface(state.Screen);

		// Where the selection paints, in the target's own coordinates. The
		// snapshot does no hit-testing, so this is cxdbg's arithmetic over the
		// tree's absolute bounds - and the case worth seeing is the control
		// whose rectangle is empty, which paints nowhere at all.
		var node = SelectedNode(state);
		var box = node?["absolute"];

		if (box == null)
		{
			_screenWhere.SetContent(["[dim]no selection[/]"]);
			return;
		}

		int w = box["w"]?.GetValue<int>() ?? 0;
		int h = box["h"]?.GetValue<int>() ?? 0;

		_screenWhere.SetContent([w == 0 || h == 0
			? $"[red]selection paints nothing[/] [dim]at {box["x"]},{box["y"]}[/]"
			: $"[dim]selection at[/] {box["x"]},{box["y"]} [dim]spanning[/] {w}x{h}"]);
	}

	private JsonObject? SelectedNode(TargetState state)
	{
		if (state.Tree == null || _ids.Count == 0 || _selected >= _ids.Count) return null;

		string want = _ids[_selected];
		if (want.Length == 0) return null;

		JsonObject? found = null;

		void Search(JsonNode? node)
		{
			if (found != null || node is not JsonObject o) return;

			if (o["id"]?.GetValue<string>() == want) { found = o; return; }

			foreach (var k in o["children"]?.AsArray() ?? []) Search(k);
		}

		foreach (var w in state.Tree["windows"]?.AsArray() ?? []) Search(w?["node"]);
		return found;
	}

	/// <summary>Keeps a control's own text from being read as markup.</summary>
	private static string Safe(string text) => text.Replace("[", "[[");
}
