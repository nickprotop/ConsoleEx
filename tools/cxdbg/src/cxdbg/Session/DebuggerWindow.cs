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
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Color = SharpConsoleUI.Color;
using Point = System.Drawing.Point;

namespace Cxdbg.Session;

/// <summary>
/// The session window: a tree, what is known about the selection, and why.
/// </summary>
/// <remarks>
/// <para>
/// Tree on the left, two tabs on the right answering "what is this control", a
/// docked tab strip below answering "why". Every boundary is a draggable
/// splitter, because fixed pane sizes are a guess about someone else's
/// terminal.
/// </para>
/// <para>
/// Built out of the library's own controls rather than out of formatted text:
/// the tree is a <c>TreeTableControl</c> bound to a hierarchy, so clicking,
/// expanding, filtering and scroll-into-view are the control's job and not
/// this file's. An earlier version painted the tree as markup lines and had
/// none of that.
/// </para>
/// </remarks>
internal sealed class DebuggerWindow
{
	private readonly ConsoleWindowSystem _system;
	private readonly TargetPoller _poller;
	private readonly ControlTreeModel _model = new();

	private StatusBarControl _status = null!;
	private StatusBarItem _pidItem = null!;
	private StatusBarItem _frameItem = null!;
	private StatusBarItem _stateItem = null!;
	// One section per panel, each a table of facts: a column of names beside a
	// column of values is a table, and writing it with padding simulates one.
	private readonly FactsSource _factsA = new();
	private readonly FactsSource _factsB = new();
	private readonly FactsSource _factsC = new();
	private readonly FactsSource _factsD = new();

	private readonly MarkupControl _detailsTitle = new(["[dim]nothing selected[/]"]);
	private PanelControl _panelA = null!;
	private PanelControl _panelB = null!;
	private PanelControl _panelC = null!;
	private PanelControl _panelD = null!;

	private readonly CascadeSource _cascade = new();
	private readonly FocusSource _focus = new();
	private readonly EventSource _events = new();
	private readonly MarkupControl _controlsSaid = new(["[dim]ready[/]"]);
	private DropdownControl? _themePicker;

	/// <summary>
	/// Whether a dropdown's change came from the user or from being filled.
	/// </summary>
	/// <remarks>
	/// Setting SelectedIndex raises the same event a click does, so populating
	/// the theme picker announced "theme is now ModernGray" before anyone had
	/// touched it - and would have switched the log level to Trace on sight.
	/// </remarks>
	private bool _filling;
	private readonly MarkupControl _eventsStatus = new([""]);
	private readonly MarkupControl _focusSummary = new([""]);
	private readonly MarkupControl _cascadeSummary = new([""]);
	private readonly CellRowSource _cellRow = new();
	private readonly MarkupControl _cellHeader = new([""]);
	private TableControl _cellTable = null!;
	private readonly SurfaceViewControl _screen = new();
	private readonly MarkupControl _screenWhere = new(["[dim]no selection[/]"]);

	private TreeTableControl _tree = null!;
	private TabControl? _right;
	private TabControl? _dock;

	/// <summary>The control the panes are describing.</summary>
	private ControlNode? _selection;

	/// <summary>The screen row the cells dock is reading.</summary>
	private int _row;

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

		window.DockTop.Add(Status());
		window.DockBody.Add(Body());

		_poller.Changed += OnTargetChanged;
		Render(_poller.State);

		return window;
	}

	/// <summary>
	/// The status bar: what is being looked at, and what the keys do.
	/// </summary>
	/// <remarks>
	/// The real status bar control rather than a markup line pretending to be
	/// one, so the key hints are items that can be clicked as well as read, and
	/// the live values are labels updated in place rather than a string rebuilt
	/// each frame.
	/// </remarks>
	private IWindowControl Status()
	{
		_pidItem = new StatusBarItem { Shortcut = "pid", Label = "-" };
		_frameItem = new StatusBarItem { Shortcut = "frame", Label = "0" };
		_stateItem = new StatusBarItem { Label = "", IsVisible = false };

		_status = new StatusBarBuilder()
			.AddLeft(_pidItem)
			.AddLeft(_frameItem)
			.AddLeft(_stateItem)
			.AddRight("1-3", "pane", () => SelectRight(((_right?.ActiveTabIndex ?? 0) + 1) % 3))
			.AddRight("F1-F4", "dock", () => SelectDock(((_dock?.ActiveTabIndex ?? 0) + 1) % 4))
			.AddRight("[ ]", "row")
			.AddRight("f", "freeze", ToggleFreeze)
			.AddRight("q", "quit", () => _system.Shutdown())
			.Build();

		return _status;
	}

	private void ToggleFreeze()
	{
		_poller.Frozen = !_poller.Frozen;
		Render(_poller.State);
	}

	/// <summary>
	/// Tree beside the right pane, dock underneath, every boundary draggable.
	/// </summary>
	/// <remarks>
	/// Two star columns at 2:1 and two star rows at 2:1 to start, so nothing is
	/// fixed and nothing is starved at any size. Fill and Stretch, or the grid
	/// sizes to its content and the star tracks divide a single row - which the
	/// tree reported as "GridControl got 118x1 wants 118x1" inside a 35-row
	/// body, found by attaching cxdbg to itself.
	/// </remarks>
	private IWindowControl Body() =>
		Controls.Grid()
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

	/// <summary>
	/// The target's control tree, as a real tree table.
	/// </summary>
	/// <remarks>
	/// Everything a tree needs is already in the control: clicking selects,
	/// clicking a guide expands, <c>/</c> filters while keeping the ancestors
	/// of a match so you still see where it lives, the selected row scrolls
	/// into view, and the columns line up because they are columns.
	/// <para>
	/// Bound to the model rather than filled on each poll, so a poll updates
	/// properties and the rows are diffed. That is what keeps the selection and
	/// the expansion through an update several times a second.
	/// </para>
	/// </remarks>
	private IWindowControl Tree()
	{
		_tree = TreeTableControl.Create()
			.WithName("tree")
			.AddColumn("control", TextJustification.Left, width: 32)
			.AddColumn("got", TextJustification.Right, width: 9)
			.AddColumn("wants", TextJustification.Right, width: 9)
			.AddColumn("layout", TextJustification.Left)
			.WithFiltering()
			.WithFilterIncludingDescendants()
			.WithTreeColumn(0)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.StretchHorizontal()
			.BindItems(
				_model.Roots,
				n => n.Children,
				n => n.Cells(),
				o =>
				{
					// Identity by control id, so a node is the same node across
					// polls and keeps its row - with its selection and expansion.
					o.ItemComparer = ControlNodeComparer.Instance;
					o.IsExpanded = n => n.Expanded;
					o.IsExpandedChanged = (n, open) => n.Expanded = open;
				})
			.Build();

		_tree.SelectedRowChanged += (_, _) => OnSelectionChanged();

		return _tree;
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

		_right.AddTab("1 details", Details());

		// The screen tab repaints the target's own cells, so what appears here
		// is the application's screen rather than a description of it, with one
		// line saying where the selection sits in it.
		// A click in the picture goes to the application at that point, and
		// selects whatever is painting there. The protocol routes it through
		// the target's own hit-test dispatch, so the application responds
		// exactly as it would to someone sitting in front of it.
		_screen.CellClicked += (_, at) => OnScreenClicked(at);

		_right.AddTab("2 screen", Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_screenWhere)
			.AddControl(_screen)
			.Build());

		_right.AddTab("3 control", Controls_());

		return _right;
	}

	/// <summary>
	/// What is known about the selection, a panel per section.
	/// </summary>
	/// <remarks>
	/// Panels inside a scroller, each holding a table of facts, rather than one
	/// markup control with blank lines between headings. The sections are real
	/// regions with their own borders, so they read as sections.
	/// </remarks>
	private IWindowControl Details()
	{
		_panelA = Section("size", _factsA);
		_panelB = Section("where", _factsB);
		_panelC = Section("reach", _factsC);
		_panelD = Section("state", _factsD);

		return Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_detailsTitle)
			.AddControl(_panelA)
			.AddControl(_panelB)
			.AddControl(_panelC)
			.AddControl(_panelD)
			.Build();
	}

	/// <summary>One titled section of the details pane.</summary>
	private static PanelControl Section(string header, FactsSource facts) =>
		Controls.Panel()
			.WithHeader(header)
			.WithBorderStyle(BorderStyle.Rounded)
			.WithAlignment(HorizontalAlignment.Stretch)
			// No border and no header: the panel already has both, and a
			// facts table drawing its own inside them is chrome around
			// chrome in a pane that has no width to spare.
			.AddControl(TableControl.Create()
				.WithDataSource(facts)
				.NoBorder()
				.HideHeader()
				.StretchHorizontal()
				.Build())
			.Build();

	/// <summary>
	/// The things about the application that can be changed from here.
	/// </summary>
	/// <remarks>
	/// Deliberate acts with consequences - resizing the screen, closing a
	/// window - so they are controls to press rather than keys that could be
	/// hit by accident. The dock stays read-only and answers "why"; this
	/// answers "what can I change".
	/// </remarks>
	private IWindowControl Controls_()
	{
		var panel = Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_controlsSaid)
			.AddControl(Section("screen", Controls.Prompt()
				.WithPrompt("size ")
				.WithPlaceholder("120x40")
				.OnEntered((_, text) => Resize(text))
				.Build()))
			.AddControl(Section("theme", ThemePicker()))
			.AddControl(Section("logging", Controls.Dropdown()
				.WithPrompt("level ")
				.AddItems("Trace", "Debug", "Information", "Warning", "Error")
				.OnSelectedItemChanged((_, item) => SetLogLevel(item?.Text))
				.Build()))
			.AddControl(Section("the selected control", Buttons(
				(" focus ", () => ActOnSelection("focusctl")),
				(" invoke ", () => ActOnSelection("invoke")))))
			.AddControl(Section("the selected window", Buttons(
				(" activate ", () => ActOnWindow("activate")),
				(" close ", () => ActOnWindow("close")))))
			.Build();

		return panel;
	}

	/// <summary>Buttons side by side, each as wide as its own text.</summary>
	private static IWindowControl Buttons(params (string Text, Action OnClick)[] buttons)
	{
		var grid = Controls.Grid()
			.Columns(Enumerable.Repeat(GridLength.Auto(), buttons.Length).ToArray())
			.Rows(GridLength.Auto())
			.ColumnGap(1);

		for (int i = 0; i < buttons.Length; i++)
		{
			var (text, onClick) = buttons[i];

			grid = grid.Place(Controls.Button()
				.WithText(text)
				.OnClick((_, _) => onClick())
				.Build(), 0, i);
		}

		return grid.Build();
	}

	/// <summary>One titled group of controls.</summary>
	private static PanelControl Section(string header, IWindowControl content) =>
		Controls.Panel()
			.WithHeader(header)
			.WithBorderStyle(BorderStyle.Rounded)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(content)
			.Build();

	/// <summary>The themes the application has, asked for rather than assumed.</summary>
	private IWindowControl ThemePicker()
	{
		_themePicker = Controls.Dropdown()
			.WithPrompt("theme ")
			.OnSelectedItemChanged((_, item) => SetTheme(item?.Text))
			.Build();

		return _themePicker;
	}

	/// <summary>
	/// Asks the application which themes it has, once.
	/// </summary>
	/// <remarks>
	/// Asked rather than assumed: the list is the application's, and a picker
	/// offering themes it does not have would be a list of errors waiting to
	/// happen. Once, because the registry does not change while it runs.
	/// </remarks>
	private void FillThemePicker()
	{
		if (_themePicker == null || _themePicker.Items.Count > 0) return;

		var reply = _poller.AskNow("theme");
		var themes = reply?["themes"]?.AsArray();

		if (themes == null || themes.Count == 0) return;

		string? current = reply?["theme"]?.GetValue<string>();
		int selected = 0;

		_filling = true;

		try
		{
			for (int i = 0; i < themes.Count; i++)
			{
				string name = themes[i]?["name"]?.GetValue<string>() ?? "";
				if (name.Length == 0) continue;

				_themePicker.AddItem(new DropdownItem(name));
				if (name == current) selected = i;
			}

			// The one that is on, so the picker opens showing the truth rather
			// than its first entry.
			_themePicker.SelectedIndex = selected;
		}
		finally
		{
			_filling = false;
		}
	}

	/// <summary>Resizes a headless application's screen.</summary>
	/// <remarks>
	/// Only a headless one: a real terminal owns its own size, and the server
	/// refuses with an explanation rather than pretending. Narrow and short
	/// sizes are where layout bugs hide, so reaching them without restarting
	/// is most of the value here.
	/// </remarks>
	private void Resize(string? text)
	{
		var parts = (text ?? "").Split('x', 'X');

		if (parts.Length != 2
			|| !int.TryParse(parts[0].Trim(), out int w)
			|| !int.TryParse(parts[1].Trim(), out int h))
		{
			Said("[yellow]a size looks like 120x40[/]");
			return;
		}

		Said(_poller.AskNow("size", new Dictionary<string, JsonNode?>
		{
			["w"] = w,
			["h"] = h,
		}), $"resized to {w}x{h}");
	}

	private void SetTheme(string? name)
	{
		if (_filling || string.IsNullOrWhiteSpace(name)) return;

		Said(_poller.AskNow("theme", new Dictionary<string, JsonNode?> { ["name"] = name }),
			$"theme is now {name}");
	}

	private void SetLogLevel(string? level)
	{
		if (_filling || string.IsNullOrWhiteSpace(level)) return;

		Said(_poller.AskNow("set", new Dictionary<string, JsonNode?> { ["logLevel"] = level }),
			$"log level is now {level}");
	}

	/// <summary>Focuses or activates whatever the tree has selected.</summary>
	private void ActOnSelection(string verb)
	{
		if (_selection is not { Id.Length: > 0 } selected)
		{
			Said("[yellow]select a control first[/]");
			return;
		}

		Said(_poller.AskNow(verb, new Dictionary<string, JsonNode?> { ["name"] = selected.Id }),
			$"{verb} {selected.Id}");
	}

	/// <summary>Activates or closes the window the selection lives in.</summary>
	private void ActOnWindow(string what)
	{
		if (WindowOf(_selection) is not { Length: > 0 } title)
		{
			Said("[yellow]select something inside a window first[/]");
			return;
		}

		Said(_poller.AskNow("window", new Dictionary<string, JsonNode?>
		{
			[what] = title,
		}), $"{what} {title}");
	}

	/// <summary>Reports what the application said, or what went wrong.</summary>
	private void Said(JsonNode? reply, string onSuccess)
	{
		if (reply?["ok"]?.GetValue<bool>() == true)
		{
			Said($"[green]{Safe(onSuccess)}[/]");
			return;
		}

		// The server's own words: it knows why it refused - which driver owns
		// the size, which themes exist - and flattening that into "failed"
		// throws away the only part worth reading.
		Said($"[red]{Safe(reply?["error"]?.GetValue<string>() ?? "no answer")}[/]");
	}

	private void Said(string markup) => _controlsSaid.SetContent([markup]);

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
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_cascadeSummary)
			.AddControl(TableControl.Create()
				.WithName("cascade")
				.WithDataSource(_cascade)
				.StretchHorizontal()
				.Build())
			.Build());
		// Tab order with each stop's geometry: the one question neither
		// reachable nor the tree answers alone.
		_dock.AddTab("2 focus", Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_focusSummary)
			.AddControl(TableControl.Create()
				.WithName("focus")
				.WithDataSource(_focus)
				.NoBorder()
				.StretchHorizontal()
				.Build())
			.Build());

		_dock.AddTab("3 cells", Cells());

		// Input as the target recorded it, when it was built to record. The
		// recorder is fixed at construction, so when it is off this tab says
		// so rather than showing an empty table that reads as silence.
		_dock.AddTab("4 events", Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_eventsStatus)
			.AddControl(TableControl.Create()
				.WithName("events")
				.WithDataSource(_events)
				.NoBorder()
				.StretchHorizontal()
				.Build())
			.Build());

		return _dock;
	}

	/// <summary>
	/// One row of the target's screen, column by column, reading across.
	/// </summary>
	/// <remarks>
	/// A table over a data source, so the dock's width is the row's direction.
	/// An earlier version wrote a line per column, which ran vertically down a
	/// horizontal dock - the wrong way round for a row of a screen.
	/// </remarks>
	private IWindowControl Cells()
	{
		// The columns and their widths come from the data source, which is what
		// defines them when there is one.
		_cellTable = TableControl.Create()
			.WithName("cells")
			.WithDataSource(_cellRow)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.StretchHorizontal()
			.Build();

		// A scroller rather than a nested grid: the grid's Auto row had nothing
		// to size to, because a table over a data source does not ask for a
		// height, and both children came out 0x0 inside a 118x10 cell.
		return Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(_cellHeader)
			.AddControl(_cellTable)
			.Build();
	}

	/// <summary>A scrolling pane around one markup control.</summary>
	private static IWindowControl Pane(IWindowControl content) =>
		Controls.ScrollablePanel()
			.WithScrollbar()
			.WithMouseWheel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(content)
			.Build();

	/// <summary>
	/// The session's own keys.
	/// </summary>
	/// <remarks>
	/// Few, and deliberately not the ones the tree wants: arrows, Enter, the
	/// filter key and the mouse all belong to the tree table, which does more
	/// with them than this file could. These are the ones that are about the
	/// session rather than about a control.
	/// </remarks>
	private void OnKey(object? sender, KeyPressedEventArgs e)
	{
		// A control that took the key keeps it. Without this the session's own
		// shortcuts ate every 1, 2, 3, [, ], f and q before an input could see
		// them, so the size prompt received an empty string and the filter
		// could not be typed into either.
		if (e.AlreadyHandled) return;

		switch (e.KeyInfo.Key)
		{
			case ConsoleKey.F1: e.Handled = SelectDock(0); return;
			case ConsoleKey.F2: e.Handled = SelectDock(1); return;
			case ConsoleKey.F3: e.Handled = SelectDock(2); return;
			case ConsoleKey.F4: e.Handled = SelectDock(3); return;
		}

		// Function keys are the session's whatever has focus, because nothing
		// types them. Characters are only the session's when no control is
		// taking text.
		if (TypingSomewhere()) return;

		switch (e.KeyInfo.KeyChar)
		{
			case '1': e.Handled = SelectRight(0); return;
			case '2': e.Handled = SelectRight(1); return;
			case '3': e.Handled = SelectRight(2); return;

			// The row the cells dock reads. Square brackets because they sit
			// next to each other and read as "move the window along".
			case '[': e.Handled = MoveRow(-1); return;
			case ']': e.Handled = MoveRow(+1); return;
		}

		switch (char.ToLowerInvariant(e.KeyInfo.KeyChar))
		{
			case 'f':
				// Freeze: the target carries on, only the looking stops.
				ToggleFreeze();
				e.Handled = true;
				return;

			case 'q':
				_system.Shutdown();
				e.Handled = true;
				return;
		}
	}

	/// <summary>
	/// Whether a control has the keyboard for typing.
	/// </summary>
	/// <remarks>
	/// The tree's filter, the size prompt, a dropdown being searched: all of
	/// them want plain characters, and a shortcut that takes them first makes
	/// the control look broken rather than the shortcut look greedy.
	/// </remarks>
	private bool TypingSomewhere()
	{
		var focused = _tree.GetParentWindow()?.FocusManager.FocusedControl;

		return focused is PromptControl or DropdownControl
			|| (focused is TableControl table && table.IsFiltering);
	}

	private bool SelectRight(int index)
	{
		if (_right == null || index < 0 || index >= 3) return false;

		_right.ActiveTabIndex = index;
		Render(_poller.State);
		return true;
	}

	private bool SelectDock(int index)
	{
		if (_dock == null || index < 0 || index >= 4) return false;

		_dock.ActiveTabIndex = index;
		Render(_poller.State);
		return true;
	}

	private bool MoveRow(int by)
	{
		int height = Math.Max(1, _poller.State.Screen.Count);
		_row = Math.Clamp(_row + by, 0, height - 1);

		RenderCells();
		return true;
	}

	/// <summary>
	/// Takes the tree's selection and re-answers everything about it.
	/// </summary>
	/// <remarks>
	/// The cascade is asked for HERE, on selection, and never on the poll:
	/// measure forces a relayout, so polling it would make the debugger the
	/// cause of the churn it reports.
	/// </remarks>
	private void OnSelectionChanged()
	{
		_selection = _tree.SelectedRow?.Tag as ControlNode;

		// The screen follows the selection: the desktop shows the composed
		// desktop, anything inside a window shows that window. Asking for the
		// window the selection lives in is what makes the picture answer the
		// question you are asking rather than always the active one.
		_poller.ScreenOf = WindowOf(_selection);

		RenderDetails();
		RenderSize();
		RenderScreenWhere();
	}

	private void OnTargetChanged(TargetState state) =>
		_system.EnqueueOnUIThread(() => Render(state));

	private void Render(TargetState state)
	{
		RenderHeader(state);

		// The model merges rather than replaces, so the bound rows keep their
		// selection and expansion through this.
		_model.Merge(state.Tree);

		// The selected node object may have been replaced by the merge, so the
		// panes re-read it from the tree rather than holding the old one.
		_selection = _tree.SelectedRow?.Tag as ControlNode;

		// The focus chain comes from the poll, so the details pane can say
		// whether the selection is the control that has the keyboard.
		_focusChain = (state.Focus?["chain"]?.AsArray() ?? [])
			.Select(n => n?.GetValue<string>() ?? "")
			.ToList();

		_tabStops = (state.Reachable?["stops"]?.AsArray() ?? [])
			.Select(n => n?["control"]?.GetValue<string>() ?? "")
			.ToList();

		RenderDetails();
		RenderSize();
		RenderScreen(state);
		RenderScreenWhere();
		RenderCells();
		RenderFocus(state);
		RenderEvents();
		FillThemePicker();
	}

	private void RenderHeader(TargetState state)
	{
		// Labels updated in place, in one batch, so the bar is not rebuilt
		// several times a second and its items keep their identity.
		_status.BatchUpdate(() =>
		{
			_pidItem.Label = _poller.Pid.ToString();
			_frameItem.Label = state.Frame.ToString();

			if (state.Gone)
			{
				_stateItem.Label = "the application is gone";
				_stateItem.LabelForeground = Color.Red;
				_stateItem.IsVisible = true;
			}
			else if (_poller.Frozen)
			{
				_stateItem.Label = "frozen";
				_stateItem.LabelForeground = Color.Yellow;
				_stateItem.IsVisible = true;
			}
			else
			{
				_stateItem.IsVisible = false;
			}
		});
	}

	/// <summary>
	/// Everything known about the selection, section by section.
	/// </summary>
	/// <remarks>
	/// Each section is a panel holding a table of facts. The desktop, a window
	/// and a control are three different things with three different sets of
	/// facts, so each fills the sections with its own.
	/// </remarks>
	private void RenderDetails()
	{
		_factsA.Clear();
		_factsB.Clear();
		_factsC.Clear();
		_factsD.Clear();

		var node = _selection;

		if (node == null)
		{
			_detailsTitle.SetContent(["[dim]nothing selected[/]"]);
			ShowSections(false, false, false, false);
			return;
		}

		switch (node.Kind)
		{
			case ControlNode.NodeKind.Desktop: DesktopFacts(node.Raw); break;
			case ControlNode.NodeKind.Window: WindowFacts(node.Raw); break;
			default: ControlFacts(node); break;
		}

		_factsA.Done();
		_factsB.Done();
		_factsC.Done();
		_factsD.Done();

		ShowSections(_factsA.HasFacts, _factsB.HasFacts, _factsC.HasFacts, _factsD.HasFacts);
	}

	/// <summary>Hides the sections that have nothing in them.</summary>
	/// <remarks>
	/// An empty bordered panel reads as something missing rather than something
	/// absent, so a section with no facts is not drawn at all.
	/// </remarks>
	private void ShowSections(bool a, bool b, bool c, bool d)
	{
		_panelA.Visible = a;
		_panelB.Visible = b;
		_panelC.Visible = c;
		_panelD.Visible = d;
	}

	/// <summary>The desktop: the screen everything else is drawn on.</summary>
	private void DesktopFacts(JsonObject d)
	{
		_detailsTitle.SetContent([$"[bold]desktop[/]  [dim]{d["theme"]}[/]"]);

		_factsA.Add("screen", $"{d["width"]} x {d["height"]}");
		_factsA.Add("windows", d["windows"]?.ToString());

		_factsB.Add("origin", $"{d["originX"]},{d["originY"]}");
		_factsB.Add("bottom", d["desktopBottom"]?.ToString());

		int top = d["topPanelRows"]?.GetValue<int>() ?? 0;
		int bottom = d["bottomPanelRows"]?.GetValue<int>() ?? 0;

		_factsB.Add("top rows", top.ToString(), "[dim]the system's own panel[/]");
		_factsB.Add("bottom rows", bottom.ToString());

		if (top + bottom > 0)
		{
			_factsB.Add("", "",
				$"[dim]{top + bottom} of {d["height"]} rows are not a window's to use[/]");
		}

		// Both of these change how input is routed, so a window that seems
		// deaf is often a modal or a portal open somewhere else.
		if (d["modals"]?.GetValue<bool>() == true)
			_factsC.Add("modal", "[yellow]open[/]", "[yellow]it is taking the input[/]");

		if (d["portals"]?.GetValue<bool>() == true)
			_factsC.Add("portal", "[yellow]open[/]", "[yellow]it sees the keys first[/]");
	}

	/// <summary>A window: where it is, what it is allowed to do.</summary>
	private void WindowFacts(JsonObject w)
	{
		var b = w["bounds"];
		var a = w["absolute"];
		var st = w["state"];

		_detailsTitle.SetContent([
			$"[bold]{Safe(w["title"]?.GetValue<string>() ?? "window")}[/]",
			$"[dim]{Safe(w["id"]?.GetValue<string>() ?? "")}[/]",
		]);

		_factsA.Add("size", $"{b?["w"]} x {b?["h"]}");
		_factsA.Add("root", Safe(w["root"]?.GetValue<string>() ?? "-"));

		_factsB.Add("on desktop", $"{b?["x"]},{b?["y"]}");
		_factsB.Add("on screen", $"{a?["x"]},{a?["y"]}");

		_factsC.Add("window", Safe(st?["windowState"]?.GetValue<string>() ?? "-"));
		_factsC.Add("active", Yes(st?["active"]), st?["active"]?.GetValue<bool>() == true
			? "[dim]its keys are the ones being typed[/]" : null);
		_factsC.Add("modal", Yes(st?["modal"]));

		_factsD.Add("move", Yes(st?["movable"]));
		_factsD.Add("resize", Yes(st?["resizable"]));
		_factsD.Add("close", Yes(st?["closable"]));
	}

	/// <summary>A control: its size, its place, whether it can be reached.</summary>
	private void ControlFacts(ControlNode node)
	{
		var raw = node.Raw;

		_detailsTitle.SetContent(node.Id.Length > 0
			? [$"[bold]{Safe(node.Id)}[/]"]
			: [$"[bold]{Safe(node.Type)}[/]",
				"[dim]layout scaffolding - nothing else can be asked about it[/]"]);

		var bounds = raw["bounds"];
		var desired = raw["desired"];
		var absolute = raw["absolute"];

		int w = bounds?["w"]?.GetValue<int>() ?? -1;
		int h = bounds?["h"]?.GetValue<int>() ?? -1;
		int dw = desired?["w"]?.GetValue<int>() ?? w;
		int dh = desired?["h"]?.GetValue<int>() ?? h;

		_factsA.Add("got", $"{w} x {h}");

		if (dw != w || dh != h)
		{
			_factsA.Add("wanted", $"[yellow]{dw} x {dh}[/]",
				dw > w || dh > h
					? "[yellow]it asked for more than it was given[/]"
					: "[dim]it was given more than it asked for[/]");
		}

		if (w == 0 || h == 0)
			_factsA.Add("", "", "[red]it paints nothing[/]");

		_factsB.Add("in window", $"{bounds?["x"]},{bounds?["y"]}");
		_factsB.Add("on screen", $"{absolute?["x"]},{absolute?["y"]}");
		_factsB.Add("layout", Safe(node.Layout.Length > 0 ? node.Layout : "-"));

		var state = raw["state"] as JsonObject;
		bool focusable = state?["focusable"]?.GetValue<bool>() == true;

		_factsC.Add("visible", raw["visible"]?.ToString());
		_factsC.Add("enabled", Yes(state?["enabled"]));
		_factsC.Add("focusable", Yes(state?["focusable"]),
			focusable && (w == 0 || h == 0)
				? "[red]Tab reaches it, you cannot see it[/]"
				: null);

		int stop = TabStopOf(node);
		if (stop > 0) _factsC.Add("tab stop", $"{stop} of {_tabStops.Count}");

		if (_focusChain.Count > 0 && _focusChain[^1] == node.Type)
			_factsC.Add("focused", "[green]yes[/]", "[dim]it has the keyboard[/]");

		// Whatever this control type reports about itself, in the library's
		// shared vocabulary - the names are its, not cxdbg's.
		if (state != null)
		{
			foreach (var (key, value) in state)
			{
				if (key is "enabled" or "focusable" or "interactive") continue;
				_factsD.Add(key, Safe(Short(value?.ToString() ?? "")));
			}

			// The three scroll names together answer a question none of them
			// answers alone.
			if (state["scrollOffset"]?.GetValue<int>() is { } off
				&& state["viewportExtent"]?.GetValue<int>() is { } view
				&& state["contentExtent"]?.GetValue<int>() is { } content
				&& content > view)
			{
				_factsD.Add("", "", $"[dim]showing {view} of {content}, {content - view - off} below[/]");
			}
		}
	}

	/// <summary>This control's place in the Tab order, or 0 when it has none.</summary>
	private int TabStopOf(ControlNode node)
	{
		for (int i = 0; i < _tabStops.Count; i++)
		{
			if (_tabStops[i] == node.Type) return i + 1;
		}

		return 0;
	}

	private static string Yes(JsonNode? node) =>
		node?.GetValue<bool>() == true ? "yes" : "no";

	/// <summary>
	/// Passes a click on the picture through to the application.
	/// </summary>
	/// <param name="at">Where, in the target's own coordinates.</param>
	/// <remarks>
	/// Two things at once, because both are what you meant: the application
	/// gets the click, and the tree selects the control painting there. The
	/// first is the point of a remote view; the second saves hunting down the
	/// tree for what you just pointed at.
	/// </remarks>
	private void OnScreenClicked(Point at)
	{
		// The picture may be one window rather than the desktop, in which case
		// its cells start at the window's own origin.
		int x = at.X + _screenOrigin.X;
		int y = at.Y + _screenOrigin.Y;

		_poller.AskNow("mouse", new Dictionary<string, JsonNode?>
		{
			["x"] = x,
			["y"] = y,
			["button"] = "left",
			["action"] = "click",
		});

		SelectPainting(x, y);
	}

	/// <summary>Selects the deepest control whose rectangle holds a point.</summary>
	/// <remarks>
	/// Deepest, because every ancestor contains the point too and the one you
	/// pointed at is the innermost. The snapshot does no hit-testing, so this
	/// is cxdbg's own arithmetic over the tree's absolute bounds.
	/// </remarks>
	private void SelectPainting(int x, int y)
	{
		ControlNode? best = null;
		int bestArea = int.MaxValue;

		void Search(ControlNode node)
		{
			var b = node.Raw["absolute"];

			if (b != null && node.Kind == ControlNode.NodeKind.Control)
			{
				int bx = b["x"]?.GetValue<int>() ?? 0;
				int by = b["y"]?.GetValue<int>() ?? 0;
				int bw = b["w"]?.GetValue<int>() ?? 0;
				int bh = b["h"]?.GetValue<int>() ?? 0;

				if (bw > 0 && bh > 0
					&& x >= bx && x < bx + bw
					&& y >= by && y < by + bh
					&& bw * bh < bestArea)
				{
					best = node;
					bestArea = bw * bh;
				}
			}

			foreach (var child in node.Children) Search(child);
		}

		foreach (var root in _model.Roots) Search(root);

		if (best is not { Id.Length: > 0 } found) return;

		// By index, because the selection is set by index: the row object
		// leads to it through the table's own row list, which is the only
		// public way across.
		var rows = _tree.Rows;

		for (int i = 0; i < rows.Count; i++)
		{
			if (!ReferenceEquals(rows[i].Tag, found)) continue;

			_tree.SelectedRowIndex = i;
			return;
		}
	}

	/// <summary>Where the screen pane's cells start, in the target's coordinates.</summary>
	private Point _screenOrigin;

	/// <summary>The Tab order, with what each stop looks like.</summary>
	private void RenderFocus(TargetState state)
	{
		string? focused = state.Focus?["focused"]?.GetValue<string>();

		_focusSummary.SetContent([
			_tabStops.Count == 0
				? "[dim]nothing in this window can take the keyboard[/]"
				: $"[bold]{_tabStops.Count}[/] {(_tabStops.Count == 1 ? "stop" : "stops")}   "
					+ $"[dim]the keyboard is on[/] [green]{Safe(focused ?? "nothing")}[/]",
		]);

		_focus.SetOrder(_tabStops, focused, _model);
	}

	/// <summary>
	/// What input the target has seen.
	/// </summary>
	/// <remarks>
	/// Asked for only while its tab is showing: a tape nobody is reading is a
	/// round trip per frame for nothing.
	/// </remarks>
	private void RenderEvents()
	{
		if (_dock?.ActiveTabIndex != 3) return;

		_events.SetEvents(_poller.AskNow("events"));
		_eventsStatus.SetContent([$"[dim]{_events.Status}[/]"]);
	}

	/// <summary>Where the selection sits on the target's screen.</summary>
	private void RenderScreenWhere()
	{
		var box = _selection?.Raw["absolute"];

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

	/// <summary>Shortens a value that would otherwise fill the pane.</summary>
	private static string Short(string text) =>
		text.Length <= 80 ? text : text[..77] + "...";

	/// <summary>Why the selection is the size it is.</summary>
	/// <remarks>
	/// Asked for on SELECTION and never on the poll: measure forces a relayout,
	/// so polling it would make the debugger the cause of the churn it reports.
	/// </remarks>
	private void RenderSize()
	{
		if (_selection is not { Id.Length: > 0 } selected)
		{
			_cascadeSummary.SetContent(["[dim]select a control with an id[/]"]);
			_cascade.SetCascade(null, "");
			return;
		}

		var reply = _poller.AskNow("measure",
			new Dictionary<string, JsonNode?> { ["control"] = selected.Id });

		_cascade.SetCascade(reply, Safe(selected.Id));
		_cascadeSummary.SetContent([_cascade.Summary]);
	}

	/// <summary>
	/// Hands the cells dock one row of the screen.
	/// </summary>
	/// <remarks>
	/// Read from the cells the screen pane is already holding, so the dock
	/// costs no extra round trip and cannot disagree with the picture above it.
	/// </remarks>
	private void RenderCells()
	{
		var screen = _poller.State.Screen;

		if (screen.Count == 0)
		{
			_cellHeader.SetContent(["[dim]waiting for the application[/]"]);
			return;
		}

		int row = Math.Clamp(_row, 0, screen.Count - 1);
		var cells = screen[row];

		int wide = 0;
		int glyphs = 0;

		foreach (var c in cells)
		{
			if (c.IsWideContinuation) continue;
			glyphs++;
			if (UnicodeWidth.GetRuneWidth(c.Character) == 2) wide++;
		}

		var header = new StringBuilder();
		header.Append($"[bold]row {row}[/]   {cells.Count} columns, {glyphs} glyphs");

		if (wide > 0)
			header.Append($"   [yellow]{wide} wide, taking {wide * 2} columns[/]");

		header.Append("   [dim][[ ]] moves the row[/]");

		_cellHeader.SetContent([header.ToString()]);
		_cellRow.SetRow(row, cells);
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

		// A window snapshot's cells start at that window's own origin, so a
		// click in the picture has to be put back where it came from before
		// the application can be asked about it.
		_screenOrigin = WindowOrigin();
	}

	/// <summary>The origin of whatever the screen pane is showing.</summary>
	private Point WindowOrigin()
	{
		if (_poller.ScreenOf is not { Length: > 0 } title) return new Point(0, 0);

		foreach (var window in _model.Roots.SelectMany(r => r.Children))
		{
			if (window.Type != title) continue;

			var a = window.Raw["absolute"];
			return new Point(a?["x"]?.GetValue<int>() ?? 0, a?["y"]?.GetValue<int>() ?? 0);
		}

		return new Point(0, 0);
	}

	/// <summary>
	/// The window a node lives in, or null when it is the desktop.
	/// </summary>
	private string? WindowOf(ControlNode? node)
	{
		if (node == null || node.Kind == ControlNode.NodeKind.Desktop) return null;

		// Walk up to the window: a control knows its own id, not its window's,
		// so the model's shape is what answers this.
		foreach (var window in _model.Roots.SelectMany(r => r.Children))
		{
			if (window == node || Contains(window, node)) return window.Type;
		}

		return null;
	}

	private static bool Contains(ControlNode parent, ControlNode node) =>
		parent.Children.Any(c => c == node || Contains(c, node));

	/// <summary>The Tab order the target last reported.</summary>
	private List<string> _tabStops = [];

	/// <summary>The focus chain the target last reported.</summary>
	private List<string> _focusChain = [];

	/// <summary>Keeps a control's own text from being read as markup.</summary>
	private static string Safe(string text) => text.Replace("[", "[[");
}
