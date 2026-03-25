// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Comprehensive TabControl focus traversal tests.
/// Covers: root-level, inside HGrid, inside SPC, nested containers,
/// multiple tabs, tab switching, backward traversal, and the LazyDotIDE layout.
/// </summary>
public class TabControlFocusTests
{
	private readonly ITestOutputHelper _out;
	public TabControlFocusTests(ITestOutputHelper output) => _out = output;

	#region Helpers

	private static (ConsoleWindowSystem system, Window window) SetupWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Width = 100, Height = 30 };
		return (system, window);
	}

	private static void Activate(ConsoleWindowSystem system, Window window)
	{
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
	}

	private string Identify(Window window, params (IFocusableControl control, string name)[] known)
	{
		var fc = window.FocusManager.FocusedControl;
		if (fc == null) return "null";
		foreach (var (control, name) in known)
			if (ReferenceEquals(fc, control)) return name;
		return fc.GetType().Name;
	}

	private List<string> TabN(Window window, int count, bool backward,
		params (IFocusableControl control, string name)[] known)
	{
		var stops = new List<string>();
		for (int i = 0; i < count; i++)
		{
			window.SwitchFocus(backward);
			var name = Identify(window, known);
			stops.Add(name);
			_out.WriteLine($"{(backward ? "Shift+Tab" : "Tab")} {i + 1}: {name}");
		}
		return stops;
	}

	#endregion

	#region 1. Root-level TabControl

	/// <summary>
	/// TabControl at window root with focusable content.
	/// Tab should go: button → tabHeader → editor → button (wrap).
	/// </summary>
	[Fact]
	public void RootLevel_TabIntoTabContent()
	{
		var (system, window) = SetupWindow();

		var button = new ButtonControl { Text = "Before" };
		var tab = new TabControl();
		var editor = new MultilineEditControl();
		tab.AddTab("Code", editor);
		tab.AddTab("Output", new MarkupControl(new List<string> { "output" }));

		window.AddControl(button);
		window.AddControl(tab);
		Activate(system, window);

		// Auto-focus lands on button (first focusable)
		Assert.True(button.HasFocus, "Auto-focus on button");

		var known = new (IFocusableControl, string)[]
		{
			(button, "button"), (tab, "tabHeader"), (editor, "editor")
		};

		var stops = TabN(window, 6, false, known);

		// Expected cycle: button → tabHeader → editor → button → ...
		Assert.Equal("tabHeader", stops[0]);
		Assert.Equal("editor", stops[1]);
		Assert.Equal("button", stops[2]);
		Assert.Equal("tabHeader", stops[3]);
	}

	/// <summary>
	/// Shift+Tab backward from editor goes to tabHeader, then button.
	/// </summary>
	[Fact]
	public void RootLevel_ShiftTabOutOfContent()
	{
		var (system, window) = SetupWindow();

		var button = new ButtonControl { Text = "Before" };
		var tab = new TabControl();
		var editor = new MultilineEditControl();
		tab.AddTab("Code", editor);

		window.AddControl(button);
		window.AddControl(tab);
		Activate(system, window);

		// Focus editor directly
		window.FocusManager.SetFocus(editor, FocusReason.Keyboard);
		Assert.True(editor.HasFocus);

		var known = new (IFocusableControl, string)[]
		{
			(button, "button"), (tab, "tabHeader"), (editor, "editor")
		};

		var stops = TabN(window, 3, backward: true, known);
		Assert.Equal("tabHeader", stops[0]);
		Assert.Equal("button", stops[1]);
		Assert.Equal("editor", stops[2]); // wrap
	}

	/// <summary>
	/// TabControl with no focusable children in active tab.
	/// Tab goes: tabHeader → next control (skip empty content).
	/// </summary>
	[Fact]
	public void RootLevel_TabWithNoFocusableContent()
	{
		var (system, window) = SetupWindow();

		var btn1 = new ButtonControl { Text = "Before" };
		var tab = new TabControl();
		tab.AddTab("Info", new MarkupControl(new List<string> { "info" }));
		var btn2 = new ButtonControl { Text = "After" };

		window.AddControl(btn1);
		window.AddControl(tab);
		window.AddControl(btn2);
		Activate(system, window);

		Assert.True(btn1.HasFocus);

		var known = new (IFocusableControl, string)[]
		{
			(btn1, "btn1"), (tab, "tabHeader"), (btn2, "btn2")
		};

		var stops = TabN(window, 4, false, known);
		Assert.Equal("tabHeader", stops[0]);
		Assert.Equal("btn2", stops[1]); // skip empty content
		Assert.Equal("btn1", stops[2]); // wrap
	}

	#endregion

	#region 2. TabControl inside HorizontalGrid (LazyDotIDE bug)

	/// <summary>
	/// LazyDotIDE-like layout: HGrid(tree, tab[editor1,editor2,editor3], tab[...]).
	/// Tab from tree → tabHeader → active editor → next tabHeader → ... → tree (wrap).
	/// This was the original bug: HGrid.GetFocusableChildren treated TabControl as a
	/// leaf stop, skipping its content entirely.
	/// </summary>
	[Fact]
	public void HGrid_TabFromTree_EntersTabContent()
	{
		var (system, window) = SetupWindow();

		// Left column: tree
		var tree = new TreeControl();
		var srcN = tree.AddRootNode("src"); srcN.AddChild("main.cs");

		// Middle column: tab control with 3 editor tabs
		var middleTab = new TabControl();
		var editor1 = new MultilineEditControl { Name = "editor1" };
		var editor2 = new MultilineEditControl { Name = "editor2" };
		var editor3 = new MultilineEditControl { Name = "editor3" };
		middleTab.AddTab("main.cs", editor1);
		middleTab.AddTab("app.cs", editor2);
		middleTab.AddTab("test.cs", editor3);

		// Right column: properties tab
		var rightTab = new TabControl();
		var propList = new ListControl(new[] { "Build", "Debug", "Test" });
		rightTab.AddTab("Properties", propList);

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid);
		var col3 = new ColumnContainer(grid) { Width = 30 };

		col1.AddContent(tree);
		col2.AddContent(middleTab);
		col3.AddContent(rightTab);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		window.AddControl(grid);
		Activate(system, window);

		// Auto-focus on tree
		Assert.True(tree.HasFocus, "Auto-focus on tree");

		var known = new (IFocusableControl, string)[]
		{
			(tree, "tree"),
			(middleTab, "middleTabHeader"), (editor1, "editor1"),
			(editor2, "editor2"), (editor3, "editor3"),
			(rightTab, "rightTabHeader"), (propList, "propList")
		};

		var stops = TabN(window, 10, false, known);

		// Critical assertions for the original bug:
		// After tree, we should reach middleTabHeader
		Assert.Equal("middleTabHeader", stops[0]);
		// After middleTabHeader, we should enter the active tab's editor (NOT skip to rightTab)
		Assert.Equal("editor1", stops[1]);
		// After editor1, we should go to rightTabHeader
		Assert.Equal("rightTabHeader", stops[2]);
		// After rightTabHeader, we should enter its content
		Assert.Equal("propList", stops[3]);
		// After propList, wrap to tree
		Assert.Equal("tree", stops[4]);
	}

	/// <summary>
	/// Backward traversal through HGrid with TabControls.
	/// </summary>
	[Fact]
	public void HGrid_ShiftTab_ReversesIntoTabContent()
	{
		var (system, window) = SetupWindow();

		var tree = new TreeControl();
		tree.AddRootNode("src");

		var middleTab = new TabControl();
		var editor = new MultilineEditControl { Name = "editor" };
		middleTab.AddTab("main.cs", editor);

		var rightTab = new TabControl();
		var propList = new ListControl(new[] { "Build" });
		rightTab.AddTab("Props", propList);

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid);
		var col3 = new ColumnContainer(grid) { Width = 30 };

		col1.AddContent(tree);
		col2.AddContent(middleTab);
		col3.AddContent(rightTab);

		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		window.AddControl(grid);
		Activate(system, window);

		// Start from tree, Shift+Tab should wrap to propList → rightTabHeader → editor → middleTabHeader
		var known = new (IFocusableControl, string)[]
		{
			(tree, "tree"), (middleTab, "middleTabHeader"), (editor, "editor"),
			(rightTab, "rightTabHeader"), (propList, "propList")
		};

		var stops = TabN(window, 5, backward: true, known);
		Assert.Equal("propList", stops[0]);      // wrap backward: last control
		Assert.Equal("rightTabHeader", stops[1]);
		Assert.Equal("editor", stops[2]);
		Assert.Equal("middleTabHeader", stops[3]);
		Assert.Equal("tree", stops[4]);           // back to start
	}

	/// <summary>
	/// Switching the active tab changes which content is reachable via Tab.
	/// </summary>
	[Fact]
	public void HGrid_SwitchTab_TabTraversesNewContent()
	{
		var (system, window) = SetupWindow();

		var btn = new ButtonControl { Text = "Left" };
		var tab = new TabControl();
		var editor1 = new MultilineEditControl { Name = "editor1" };
		var list2 = new ListControl(new[] { "item" }) { Name = "list2" };
		tab.AddTab("Tab1", editor1);
		tab.AddTab("Tab2", list2);

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid);
		col1.AddContent(btn);
		col2.AddContent(tab);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		window.AddControl(grid);
		Activate(system, window);

		var known = new (IFocusableControl, string)[]
		{
			(btn, "btn"), (tab, "tabHeader"), (editor1, "editor1"), (list2, "list2")
		};

		// Tab1 active: btn → tabHeader → editor1 → btn
		Assert.True(btn.HasFocus);
		var stops1 = TabN(window, 3, false, known);
		Assert.Equal("tabHeader", stops1[0]);
		Assert.Equal("editor1", stops1[1]);
		Assert.Equal("btn", stops1[2]);

		// Switch to Tab2
		tab.ActiveTabIndex = 1;

		// Now: btn → tabHeader → list2 → btn
		var stops2 = TabN(window, 3, false, known);
		Assert.Equal("tabHeader", stops2[0]);
		Assert.Equal("list2", stops2[1]);
		Assert.Equal("btn", stops2[2]);
	}

	#endregion

	#region 3. TabControl inside ScrollablePanelControl

	/// <summary>
	/// TabControl inside an SPC (common pattern: toolbar + tabs in a panel).
	/// Toolbar focuses its first button, then Tab goes: button → tabHeader → editor → button (wrap).
	/// </summary>
	[Fact]
	public void InsideSPC_TabEntersAndExitsTabContent()
	{
		var (system, window) = SetupWindow();

		var panel = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill, AutoScroll = false };
		var runBtn = new ButtonControl { Text = "Run" };
		var toolbar = new ToolbarControl();
		toolbar.AddItem(runBtn);
		var tab = new TabControl();
		var editor = new MultilineEditControl { Name = "editor" };
		tab.AddTab("Code", editor);

		panel.AddControl(toolbar);
		panel.AddControl(tab);
		window.AddControl(panel);
		Activate(system, window);

		var known = new (IFocusableControl, string)[]
		{
			(toolbar, "toolbar"), (runBtn, "runBtn"),
			(tab, "tabHeader"), (editor, "editor")
		};

		_out.WriteLine($"Initial: {Identify(window, known)}");

		// SPC scope traversal — toolbar delegates to its button internally
		var stops = TabN(window, 6, false, known);

		// Verify tab header and editor appear in the cycle
		Assert.Contains("tabHeader", stops);
		Assert.Contains("editor", stops);

		// Verify editor comes immediately after tabHeader
		for (int i = 0; i < stops.Count - 1; i++)
		{
			if (stops[i] == "tabHeader")
			{
				Assert.Equal("editor", stops[i + 1]);
				break;
			}
		}
	}

	#endregion

	#region 4. Multiple TabControls in same container

	[Fact]
	public void MultipleTabControls_TabTraversesAllHeaders()
	{
		var (system, window) = SetupWindow();

		var tab1 = new TabControl();
		var editor1 = new MultilineEditControl { Name = "editor1" };
		tab1.AddTab("File1", editor1);

		var tab2 = new TabControl();
		var editor2 = new MultilineEditControl { Name = "editor2" };
		tab2.AddTab("File2", editor2);

		window.AddControl(tab1);
		window.AddControl(tab2);
		Activate(system, window);

		var known = new (IFocusableControl, string)[]
		{
			(tab1, "tab1Header"), (editor1, "editor1"),
			(tab2, "tab2Header"), (editor2, "editor2")
		};

		// Auto-focus on tab1 header (first focusable)
		_out.WriteLine($"Initial: {Identify(window, known)}");

		var stops = TabN(window, 5, false, known);
		// tab1Header → editor1 → tab2Header → editor2 → tab1Header
		Assert.Equal("editor1", stops[0]);
		Assert.Equal("tab2Header", stops[1]);
		Assert.Equal("editor2", stops[2]);
		Assert.Equal("tab1Header", stops[3]);
	}

	#endregion

	#region 5. Nested containers inside tab content

	/// <summary>
	/// Tab content is an SPC containing a list — Tab should enter through.
	/// </summary>
	[Fact]
	public void TabContentIsSPC_TabEntersNestedScope()
	{
		var (system, window) = SetupWindow();

		var btn = new ButtonControl { Text = "Before" };
		var tab = new TabControl();
		var innerPanel = new ScrollablePanelControl { AutoScroll = false };
		var innerList = new ListControl(new[] { "A", "B" });
		innerPanel.AddControl(innerList);
		tab.AddTab("Panel Tab", innerPanel);

		window.AddControl(btn);
		window.AddControl(tab);
		Activate(system, window);

		Assert.True(btn.HasFocus);

		var known = new (IFocusableControl, string)[]
		{
			(btn, "btn"), (tab, "tabHeader"), (innerPanel, "innerPanel"),
			(innerList, "innerList")
		};

		var stops = TabN(window, 4, false, known);
		Assert.Equal("tabHeader", stops[0]);
		// After tabHeader, should enter innerPanel (opaque scope) → which enters innerList
		Assert.Equal("innerList", stops[1]);
		Assert.Equal("btn", stops[2]); // wrap
	}

	/// <summary>
	/// Tab content is an HGrid — Tab should traverse its columns.
	/// </summary>
	[Fact]
	public void TabContentIsHGrid_TabTraversesGridChildren()
	{
		var (system, window) = SetupWindow();

		var btn = new ButtonControl { Text = "Outside" };
		var tab = new TabControl();

		// Tab content: inner HGrid with two lists
		var innerGrid = new HorizontalGridControl();
		var innerCol1 = new ColumnContainer(innerGrid);
		var innerCol2 = new ColumnContainer(innerGrid);
		var list1 = new ListControl(new[] { "A" });
		var list2 = new ListControl(new[] { "B" });
		innerCol1.AddContent(list1);
		innerCol2.AddContent(list2);
		innerGrid.AddColumn(innerCol1);
		innerGrid.AddColumn(innerCol2);

		tab.AddTab("Grid Tab", innerGrid);

		window.AddControl(btn);
		window.AddControl(tab);
		Activate(system, window);

		Assert.True(btn.HasFocus);

		var known = new (IFocusableControl, string)[]
		{
			(btn, "btn"), (tab, "tabHeader"),
			(list1, "list1"), (list2, "list2")
		};

		var stops = TabN(window, 5, false, known);
		Assert.Equal("tabHeader", stops[0]);
		// Inner grid is a scope — entering it should focus list1, then list2
		Assert.Equal("list1", stops[1]);
		Assert.Equal("list2", stops[2]);
		Assert.Equal("btn", stops[3]); // wrap
	}

	#endregion

	#region 6. LazyDotIDE full layout

	/// <summary>
	/// Replicates the full LazyDotIDE layout:
	/// Window
	///   HGrid
	///     Col0: TreeControl (file tree)
	///     Col1: TabControl with 3 tabs (each an editor — MultilineEditControl)
	///     Col2: TabControl (Properties tab with ListControl)
	///
	/// Validates: Tab from tree → middle tab header → active editor → right tab header
	///          → property list → tree (full cycle).
	/// This is the regression test for the bug where HGrid.GetFocusableChildren
	/// treated IFocusableContainerWithHeader as a leaf, skipping tab content.
	/// </summary>
	[Fact]
	public void LazyDotIDE_FullCycle_TwoRoundTrips()
	{
		var (system, window) = SetupWindow();

		// File tree (left)
		var fileTree = new TreeControl();
		var srcNode = fileTree.AddRootNode("src");
		srcNode.AddChild("Program.cs");
		srcNode.AddChild("Startup.cs");
		srcNode.AddChild("App.cs");
		fileTree.VerticalAlignment = VerticalAlignment.Fill;

		// Editor tabs (center)
		var editorTabs = new TabControl();
		var programEditor = new MultilineEditControl { Name = "ProgramEditor" };
		var startupEditor = new MultilineEditControl { Name = "StartupEditor" };
		var appEditor = new MultilineEditControl { Name = "AppEditor" };
		editorTabs.AddTab("Program.cs", programEditor);
		editorTabs.AddTab("Startup.cs", startupEditor);
		editorTabs.AddTab("App.cs", appEditor);
		editorTabs.VerticalAlignment = VerticalAlignment.Fill;

		// Properties tab (right)
		var propTabs = new TabControl();
		var buildList = new ListControl(new[] { "Debug", "Release", "Profile" });
		propTabs.AddTab("Build", buildList);
		propTabs.AddTab("Errors", new MarkupControl(new List<string> { "No errors" }));
		propTabs.VerticalAlignment = VerticalAlignment.Fill;

		// Assemble HGrid
		var mainGrid = new HorizontalGridControl();
		var colTree = new ColumnContainer(mainGrid) { Width = 25 };
		var colEditor = new ColumnContainer(mainGrid);
		var colProps = new ColumnContainer(mainGrid) { Width = 30 };

		colTree.AddContent(fileTree);
		colEditor.AddContent(editorTabs);
		colProps.AddContent(propTabs);

		mainGrid.AddColumn(colTree);
		mainGrid.AddColumn(colEditor);
		mainGrid.AddColumn(colProps);

		mainGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
		mainGrid.VerticalAlignment = VerticalAlignment.Fill;

		window.AddControl(mainGrid);
		Activate(system, window);

		// Auto-focus on tree
		Assert.True(fileTree.HasFocus, "Auto-focus should land on fileTree");

		var known = new (IFocusableControl, string)[]
		{
			(fileTree, "fileTree"),
			(editorTabs, "editorTabHeader"), (programEditor, "programEditor"),
			(startupEditor, "startupEditor"), (appEditor, "appEditor"),
			(propTabs, "propTabHeader"), (buildList, "buildList")
		};

		// Run two full cycles to prove stability
		_out.WriteLine($"Initial: {Identify(window, known)}");
		var stops = TabN(window, 11, false, known);

		// Cycle 1: tree → editorTabHeader → programEditor → propTabHeader → buildList → tree
		Assert.Equal("editorTabHeader", stops[0]);
		Assert.Equal("programEditor", stops[1]);
		Assert.Equal("propTabHeader", stops[2]);
		Assert.Equal("buildList", stops[3]);
		Assert.Equal("fileTree", stops[4]);

		// Cycle 2: identical
		Assert.Equal("editorTabHeader", stops[5]);
		Assert.Equal("programEditor", stops[6]);
		Assert.Equal("propTabHeader", stops[7]);
		Assert.Equal("buildList", stops[8]);
		Assert.Equal("fileTree", stops[9]);
	}

	/// <summary>
	/// LazyDotIDE: switch editor tab, then Tab cycle traverses new editor.
	/// </summary>
	[Fact]
	public void LazyDotIDE_SwitchEditorTab_TraversesNewEditor()
	{
		var (system, window) = SetupWindow();

		var tree = new TreeControl();
		tree.AddRootNode("src");

		var editorTabs = new TabControl();
		var editor1 = new MultilineEditControl { Name = "Editor1" };
		var editor2 = new MultilineEditControl { Name = "Editor2" };
		editorTabs.AddTab("file1.cs", editor1);
		editorTabs.AddTab("file2.cs", editor2);

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 25 };
		var col2 = new ColumnContainer(grid);
		col1.AddContent(tree);
		col2.AddContent(editorTabs);
		grid.AddColumn(col1);
		grid.AddColumn(col2);

		window.AddControl(grid);
		Activate(system, window);

		var known = new (IFocusableControl, string)[]
		{
			(tree, "tree"), (editorTabs, "tabHeader"),
			(editor1, "editor1"), (editor2, "editor2")
		};

		// Tab1 active: tree → tabHeader → editor1 → tree
		var stops = TabN(window, 3, false, known);
		Assert.Equal("tabHeader", stops[0]);
		Assert.Equal("editor1", stops[1]);
		Assert.Equal("tree", stops[2]);

		// Switch to Tab2
		editorTabs.ActiveTabIndex = 1;

		// Now: tree → tabHeader → editor2 → tree
		stops = TabN(window, 3, false, known);
		Assert.Equal("tabHeader", stops[0]);
		Assert.Equal("editor2", stops[1]);
		Assert.Equal("tree", stops[2]);
	}

	/// <summary>
	/// LazyDotIDE: Shift+Tab backward cycle is correct mirror.
	/// </summary>
	[Fact]
	public void LazyDotIDE_ShiftTab_FullBackwardCycle()
	{
		var (system, window) = SetupWindow();

		var tree = new TreeControl();
		tree.AddRootNode("src");

		var editorTabs = new TabControl();
		var editor = new MultilineEditControl { Name = "Editor" };
		editorTabs.AddTab("main.cs", editor);

		var propTabs = new TabControl();
		var propList = new ListControl(new[] { "Build" });
		propTabs.AddTab("Build", propList);

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 25 };
		var col2 = new ColumnContainer(grid);
		var col3 = new ColumnContainer(grid) { Width = 25 };
		col1.AddContent(tree);
		col2.AddContent(editorTabs);
		col3.AddContent(propTabs);
		grid.AddColumn(col1);
		grid.AddColumn(col2);
		grid.AddColumn(col3);

		window.AddControl(grid);
		Activate(system, window);

		Assert.True(tree.HasFocus);

		var known = new (IFocusableControl, string)[]
		{
			(tree, "tree"), (editorTabs, "editorTabHeader"), (editor, "editor"),
			(propTabs, "propTabHeader"), (propList, "propList")
		};

		// Shift+Tab from tree wraps backward through: propList → propTabHeader → editor → editorTabHeader → tree
		var stops = TabN(window, 5, backward: true, known);
		Assert.Equal("propList", stops[0]);
		Assert.Equal("propTabHeader", stops[1]);
		Assert.Equal("editor", stops[2]);
		Assert.Equal("editorTabHeader", stops[3]);
		Assert.Equal("tree", stops[4]);
	}

	#endregion

	#region 7. Edge cases

	/// <summary>
	/// TabControl with no tabs should be skipped in traversal.
	/// </summary>
	[Fact]
	public void EmptyTabControl_SkippedInTraversal()
	{
		var (system, window) = SetupWindow();

		var btn1 = new ButtonControl { Text = "Before" };
		var emptyTab = new TabControl(); // no tabs
		var btn2 = new ButtonControl { Text = "After" };

		window.AddControl(btn1);
		window.AddControl(emptyTab);
		window.AddControl(btn2);
		Activate(system, window);

		Assert.True(btn1.HasFocus);
		Assert.False(emptyTab.CanReceiveFocus, "Empty TabControl should not be focusable");

		var known = new (IFocusableControl, string)[]
		{
			(btn1, "btn1"), (btn2, "btn2")
		};

		var stops = TabN(window, 3, false, known);
		Assert.Equal("btn2", stops[0]);
		Assert.Equal("btn1", stops[1]); // wrap
	}

	/// <summary>
	/// TabControl where active tab content becomes invisible mid-cycle.
	/// </summary>
	[Fact]
	public void ActiveTabContentHidden_SkipsInvisibleContent()
	{
		var (system, window) = SetupWindow();

		var btn = new ButtonControl { Text = "Before" };
		var tab = new TabControl();
		var editor = new MultilineEditControl();
		tab.AddTab("Code", editor);

		window.AddControl(btn);
		window.AddControl(tab);
		Activate(system, window);

		// Hide the editor (simulates dynamic content)
		editor.Visible = false;

		Assert.True(btn.HasFocus);

		var known = new (IFocusableControl, string)[]
		{
			(btn, "btn"), (tab, "tabHeader"), (editor, "editor")
		};

		// Tab should skip invisible editor: btn → tabHeader → btn
		var stops = TabN(window, 3, false, known);
		Assert.Equal("tabHeader", stops[0]);
		Assert.Equal("btn", stops[1]); // editor skipped
	}

	/// <summary>
	/// TabControl inside HGrid with splitter — splitter is still a Tab stop.
	/// </summary>
	[Fact]
	public void HGrid_WithSplitter_TabIncludesSplitter()
	{
		var (system, window) = SetupWindow();

		var list = new ListControl(new[] { "item" });
		var tab = new TabControl();
		var editor = new MultilineEditControl();
		tab.AddTab("Code", editor);

		var grid = new HorizontalGridControl();
		var col1 = new ColumnContainer(grid) { Width = 30 };
		var col2 = new ColumnContainer(grid);
		col1.AddContent(list);
		col2.AddContent(tab);
		grid.AddColumn(col1);
		grid.AddColumnWithSplitter(col2);

		window.AddControl(grid);
		Activate(system, window);

		// Collect all stops
		var allStops = new List<string>();
		for (int i = 0; i < 10; i++)
		{
			window.SwitchFocus(false);
			var fc = window.FocusManager.FocusedControl;
			allStops.Add(fc?.GetType().Name ?? "null");
		}
		_out.WriteLine($"Stops: [{string.Join(", ", allStops)}]");

		// Should include SplitterControl somewhere in the cycle
		Assert.Contains(allStops, s => s == "SplitterControl");
		// Should include TabControl and MultilineEditControl
		Assert.Contains(allStops, s => s == "TabControl");
		Assert.Contains(allStops, s => s == "MultilineEditControl");
	}

	#endregion
}
