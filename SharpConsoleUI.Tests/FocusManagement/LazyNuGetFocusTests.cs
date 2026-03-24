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
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Replicates the LazyNuGet window layout to reproduce focus/Tab bugs.
/// Layout: HGrid with left panel (ListControl) and right panel (SPC with toolbar + list + hidden toolbar).
/// </summary>
public class LazyNuGetFocusTests
{
	private readonly ITestOutputHelper _out;
	public LazyNuGetFocusTests(ITestOutputHelper output) => _out = output;

	private static ConsoleKeyInfo TabKey => new('\t', ConsoleKey.Tab, false, false, false);
	private static ConsoleKeyInfo ShiftTabKey => new('\t', ConsoleKey.Tab, true, false, false);

	/// <summary>
	/// Builds the LazyNuGet-like layout:
	/// Window
	///   HGrid (transparent)
	///     Column 0: _contextList (ListControl)
	///     Column 1: spacing
	///     Column 2: _detailsPanel (SPC)
	///       toolbar (ToolbarControl with 4 buttons)
	///       packageList (ListControl)
	///       selectionToolbar (hidden ToolbarControl)
	/// </summary>
	private static (ConsoleWindowSystem system, Window window,
		ListControl contextList, ScrollablePanelControl detailsPanel,
		ToolbarControl toolbar, ListControl packageList, ToolbarControl selectionToolbar)
		BuildLazyNuGetLayout()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 35);
		var window = new Window(system)
		{
			Title = "LazyNuGet", Left = 0, Top = 0, Width = 120, Height = 35
		};

		// Left panel: project list
		var contextList = new ListControl();
		contextList.StringItems = new List<string>
		{
			"MyProject.Core", "MyProject.Web", "MyProject.Tests",
			"MyProject.Api", "MyProject.Shared"
		};
		contextList.Margin = new Margin(0, 1, 0, 0);
		contextList.HorizontalAlignment = HorizontalAlignment.Stretch;
		contextList.VerticalAlignment = VerticalAlignment.Fill;

		// Right panel: details
		var detailsPanel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		// Toolbar with action buttons
		var viewBtn = new ButtonControl { Text = "View Packages" };
		var updateBtn = new ButtonControl { Text = "Update All" };
		var depsBtn = new ButtonControl { Text = "Deps" };
		var restoreBtn = new ButtonControl { Text = "Restore" };

		var toolbar = new ToolbarControl();
		toolbar.AddItem(viewBtn);
		toolbar.AddItem(updateBtn);
		toolbar.AddItem(depsBtn);
		toolbar.AddItem(restoreBtn);
		toolbar.Margin = new Margin(1, 0, 1, 0);

		// Package list
		var packageList = new ListControl();
		packageList.StringItems = new List<string>
		{
			"Newtonsoft.Json 13.0.3",
			"Serilog 3.1.1",
			"AutoMapper 12.0.1",
			"FluentValidation 11.8.0",
			"MediatR 12.2.0"
		};
		packageList.Margin = new Margin(1, 1, 1, 0);
		packageList.HorizontalAlignment = HorizontalAlignment.Stretch;
		packageList.VerticalAlignment = VerticalAlignment.Fill;

		// Hidden selection toolbar (shown when items are checked)
		var updateSelectedBtn = new ButtonControl { Text = "Update Selected" };
		var removeSelectedBtn = new ButtonControl { Text = "Remove Selected" };
		var selectionToolbar = new ToolbarControl();
		selectionToolbar.AddItem(updateSelectedBtn);
		selectionToolbar.AddItem(removeSelectedBtn);
		selectionToolbar.Margin = new Margin(1, 0, 1, 0);
		selectionToolbar.Visible = false; // Hidden until items are checked

		// Add to details panel
		detailsPanel.AddControl(new MarkupControl(new List<string> { "[cyan1]Project Dashboard[/]" })
		{
			Margin = new Margin(1, 1, 0, 0)
		});
		detailsPanel.AddControl(toolbar);
		detailsPanel.AddControl(new MarkupControl(new List<string> { "[grey50]Space: toggle · Tab: next[/]" })
		{
			Margin = new Margin(1, 0, 1, 0)
		});
		detailsPanel.AddControl(packageList);
		detailsPanel.AddControl(selectionToolbar);

		// Main grid
		var mainGrid = new HorizontalGridControl();
		var col1 = new ColumnContainer(mainGrid) { Width = 40 };
		var col2 = new ColumnContainer(mainGrid);

		col1.AddContent(new MarkupControl(new List<string> { "[grey70]Projects[/]" })
		{
			Margin = new Margin(1, 0, 0, 0)
		});
		col1.AddContent(contextList);

		col2.AddContent(new MarkupControl(new List<string> { "[grey70]Dashboard[/]" })
		{
			Margin = new Margin(1, 0, 0, 0)
		});
		col2.AddContent(detailsPanel);

		mainGrid.AddColumn(col1);
		mainGrid.AddColumn(col2);
		mainGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
		mainGrid.VerticalAlignment = VerticalAlignment.Fill;

		window.AddControl(mainGrid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, contextList, detailsPanel, toolbar, packageList, selectionToolbar);
	}

	#region Tab cycle: projects view

	/// <summary>
	/// Full Tab cycle through LazyNuGet projects view.
	/// Expected: contextList → toolbar(first button) → packageList → contextList (wrap)
	/// No phantom stops. No getting stuck.
	/// </summary>
	[Fact]
	public void ProjectsView_TabCycle_FullRoundTrip()
	{
		var (system, window, contextList, detailsPanel, toolbar, packageList, selectionToolbar) =
			BuildLazyNuGetLayout();

		// Track what gets focused at each Tab press
		var focusLog = new List<string>();

		void LogFocus(string step)
		{
			var fc = window.FocusManager.FocusedControl;
			string name = fc?.GetType().Name ?? "null";
			// Identify which specific control
			if (ReferenceEquals(fc, contextList)) name = "contextList";
			else if (ReferenceEquals(fc, packageList)) name = "packageList";
			else if (ReferenceEquals(fc, toolbar)) name = "toolbar";
			else if (ReferenceEquals(fc, selectionToolbar)) name = "selectionToolbar(HIDDEN!)";
			// Check if it's a button inside the toolbar
			else if (fc is ButtonControl btn)
			{
				var items = toolbar.Items;
				for (int i = 0; i < items.Count; i++)
				{
					if (ReferenceEquals(items[i], fc))
					{
						name = $"toolbar.btn[{i}]:{btn.Text}";
						break;
					}
				}
			}
			focusLog.Add($"{step}: {name}");
			_out.WriteLine($"{step}: {name}");
		}

		// Initial state after auto-focus
		LogFocus("Initial");

		// Press Tab 10 times and log each state
		for (int i = 1; i <= 10; i++)
		{
			window.SwitchFocus(backward: false);
			LogFocus($"Tab {i}");
		}

		// Verify: no hidden controls got focus
		Assert.DoesNotContain(focusLog, s => s.Contains("HIDDEN"));

		// Verify: the cycle includes contextList and packageList
		Assert.Contains(focusLog, s => s.Contains("contextList"));
		Assert.Contains(focusLog, s => s.Contains("packageList"));

		// Verify: clean cycle (unique controls repeat in same order)
		// Extract unique control names in order of first appearance
		var uniqueStops = new List<string>();
		foreach (var entry in focusLog.Skip(1)) // skip Initial
		{
			var name = entry.Split(": ", 2)[1];
			if (uniqueStops.Count == 0 || uniqueStops[^1] != name)
			{
				if (!uniqueStops.Contains(name))
					uniqueStops.Add(name);
			}
		}

		_out.WriteLine($"Unique stops: [{string.Join(", ", uniqueStops)}]");

		// Should have at most 4 unique stops (contextList, toolbar/button, packageList, maybe one more)
		Assert.True(uniqueStops.Count <= 5,
			$"Too many unique Tab stops ({uniqueStops.Count}): [{string.Join(", ", uniqueStops)}]");
	}

	/// <summary>
	/// Tab from packageList (last visible control in SPC) should exit to contextList.
	/// NOT go to the hidden selectionToolbar.
	/// </summary>
	[Fact]
	public void ProjectsView_TabFromPackageList_TwoCycles()
	{
		var (system, window, contextList, detailsPanel, toolbar, packageList, selectionToolbar) =
			BuildLazyNuGetLayout();

		// Focus packageList (last control in SPC)
		window.FocusManager.SetFocus(packageList, FocusReason.Keyboard);
		Assert.True(packageList.HasFocus, "Setup: packageList should be focused");

		string Identify(IFocusableControl? fc)
		{
			if (fc == null) return "null";
			if (ReferenceEquals(fc, contextList)) return "contextList";
			if (ReferenceEquals(fc, packageList)) return "packageList";
			if (ReferenceEquals(fc, toolbar)) return "toolbar";
			if (fc is ButtonControl btn)
			{
				for (int i = 0; i < toolbar.Items.Count; i++)
					if (ReferenceEquals(toolbar.Items[i], fc)) return $"toolbar.btn[{i}]";
				return $"btn:{btn.Text}";
			}
			return fc.GetType().Name;
		}

		// Diagnostic: check toolbar state
		_out.WriteLine($"toolbar.Visible={toolbar.Visible} toolbar.CanReceiveFocus={toolbar.CanReceiveFocus} toolbar.IsEnabled={toolbar.IsEnabled}");
		_out.WriteLine($"toolbar.Items.Count={toolbar.Items.Count}");
		var spcChildren = detailsPanel.GetChildren();
		_out.WriteLine($"SPC children: {string.Join(", ", spcChildren.Select(c => c.GetType().Name))}");

		// Tab from packageList through TWO full cycles
		var stops = new List<string>();
		for (int i = 0; i < 20; i++)
		{
			window.SwitchFocus(backward: false);
			var name = Identify(window.FocusManager.FocusedControl);
			stops.Add(name);
			_out.WriteLine($"Tab {i + 1}: {name}");
		}

		// After packageList, first Tab should go to contextList
		Assert.Equal("contextList", stops[0]);

		// Find where contextList appears — that's the cycle boundary
		// Full cycle should be: contextList → (SPC children) → contextList
		int firstContextList = 0;
		int secondContextList = stops.IndexOf("contextList", firstContextList + 1);
		Assert.True(secondContextList > 0,
			$"Should cycle back to contextList. Stops: [{string.Join(", ", stops)}]");

		// Third contextList (second full cycle)
		int thirdContextList = stops.IndexOf("contextList", secondContextList + 1);
		Assert.True(thirdContextList > 0,
			$"Should complete two full cycles. Stops: [{string.Join(", ", stops)}]");

		// Both cycles should be the same length
		int cycle1Length = secondContextList - firstContextList;
		int cycle2Length = thirdContextList - secondContextList;
		Assert.Equal(cycle1Length, cycle2Length);

		_out.WriteLine($"Cycle length: {cycle1Length} stops");
		_out.WriteLine($"Cycle: [{string.Join(" → ", stops.Skip(firstContextList).Take(cycle1Length))}]");
	}

	/// <summary>
	/// Tab from contextList should enter the SPC and focus the first focusable child.
	/// </summary>
	[Fact]
	public void ProjectsView_TabFromContextList_EntersSPC()
	{
		var (system, window, contextList, detailsPanel, toolbar, packageList, selectionToolbar) =
			BuildLazyNuGetLayout();

		// Focus contextList
		window.FocusManager.SetFocus(contextList, FocusReason.Keyboard);
		Assert.True(contextList.HasFocus, "Setup: contextList should be focused");

		// Tab should enter the SPC
		window.SwitchFocus(backward: false);
		var afterTab = window.FocusManager.FocusedControl;
		_out.WriteLine($"After Tab from contextList: focused={afterTab?.GetType().Name}");

		// Should be something inside the SPC (toolbar button or toolbar itself)
		Assert.False(contextList.HasFocus, "contextList should lose focus");
		Assert.NotNull(afterTab);
	}

	#endregion

	#region Hidden toolbar not reachable

	/// <summary>
	/// The hidden selectionToolbar should never receive focus via Tab.
	/// </summary>
	[Fact]
	public void ProjectsView_HiddenToolbar_NeverFocused()
	{
		var (system, window, contextList, detailsPanel, toolbar, packageList, selectionToolbar) =
			BuildLazyNuGetLayout();

		Assert.False(selectionToolbar.Visible, "Selection toolbar should be hidden");

		// Tab 20 times — hidden toolbar should never get focus
		for (int i = 0; i < 20; i++)
		{
			window.SwitchFocus(backward: false);
			var fc = window.FocusManager.FocusedControl;
			Assert.False(ReferenceEquals(fc, selectionToolbar),
				$"Hidden selectionToolbar should never get focus (Tab {i + 1})");
		}
	}

	#endregion

	#region Shift+Tab reverse cycle

	[Fact]
	public void ProjectsView_ShiftTabCycle()
	{
		var (system, window, contextList, detailsPanel, toolbar, packageList, selectionToolbar) =
			BuildLazyNuGetLayout();

		var focusLog = new List<string>();

		// Press Shift+Tab 10 times
		for (int i = 1; i <= 10; i++)
		{
			window.SwitchFocus(backward: true);
			var fc = window.FocusManager.FocusedControl;
			string name = fc?.GetType().Name ?? "null";
			if (ReferenceEquals(fc, contextList)) name = "contextList";
			else if (ReferenceEquals(fc, packageList)) name = "packageList";
			focusLog.Add(name);
			_out.WriteLine($"Shift+Tab {i}: {name}");
		}

		// Should include both contextList and packageList
		Assert.Contains(focusLog, s => s == "contextList");
		Assert.Contains(focusLog, s => s == "packageList");
	}

	#endregion

	#region Packages view (with TabControl)

	/// <summary>
	/// Builds the LazyNuGet packages view layout:
	/// Window
	///   HGrid (transparent)
	///     Column 0: _contextList (ListControl — package list)
	///     Column 2: _detailsPanel (SPC)
	///       toolbar (Update/Version/Remove buttons)
	///       tabControl (5 tabs — all display-only, no focusable children)
	/// </summary>
	private static (ConsoleWindowSystem system, Window window,
		ListControl contextList, ScrollablePanelControl detailsPanel,
		ToolbarControl toolbar, TabControl tabControl)
		BuildPackagesViewLayout()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 35);
		var window = new Window(system)
		{
			Title = "LazyNuGet - Packages", Left = 0, Top = 0, Width = 120, Height = 35
		};

		// Left panel: package list
		var contextList = new ListControl();
		contextList.StringItems = new List<string>
		{
			"Newtonsoft.Json", "Serilog", "AutoMapper", "FluentValidation", "MediatR"
		};
		contextList.Margin = new Margin(0, 1, 0, 0);
		contextList.HorizontalAlignment = HorizontalAlignment.Stretch;
		contextList.VerticalAlignment = VerticalAlignment.Fill;

		// Right panel: package details
		var detailsPanel = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};

		// Header
		detailsPanel.AddControl(new MarkupControl(new List<string>
		{
			"[cyan1 bold]Package: Newtonsoft.Json[/]",
			"[grey70]Installed: 13.0.3[/]",
			""
		}) { Margin = new Margin(1, 1, 0, 0) });

		// Action toolbar
		var updateBtn = new ButtonControl { Text = "Update" };
		var versionBtn = new ButtonControl { Text = "Change Version" };
		var removeBtn = new ButtonControl { Text = "Remove" };

		var toolbar = new ToolbarControl();
		toolbar.AddItem(updateBtn);
		toolbar.AddItem(versionBtn);
		toolbar.AddItem(removeBtn);
		toolbar.Margin = new Margin(1, 0, 1, 0);
		detailsPanel.AddControl(toolbar);

		// Separator
		detailsPanel.AddControl(new RuleControl { Margin = new Margin(1, 0, 1, 0) });

		// TabControl with display-only tabs (no focusable children!)
		var tabControl = new TabControl();
		tabControl.AddTab("Overview",
			new MarkupControl(new List<string> { "[grey70]Json.NET is a popular JSON framework.[/]", "", "[grey50]Downloads: 2.5B[/]" })
			{ Margin = new Margin(1, 1, 1, 0) });
		tabControl.AddTab("Deps",
			new MarkupControl(new List<string> { "[grey70]No dependencies[/]" })
			{ Margin = new Margin(1, 1, 1, 0) });
		tabControl.AddTab("Versions",
			new MarkupControl(new List<string> { "[grey70]13.0.3, 13.0.2, 13.0.1[/]" })
			{ Margin = new Margin(1, 1, 1, 0) });
		tabControl.AddTab("What's New",
			new MarkupControl(new List<string> { "[grey70]Bug fixes and improvements[/]" })
			{ Margin = new Margin(1, 1, 1, 0) });
		tabControl.AddTab("Security",
			new MarkupControl(new List<string> { "[green]No known vulnerabilities[/]" })
			{ Margin = new Margin(1, 1, 1, 0) });
		tabControl.Margin = new Margin(1, 1, 1, 0);
		detailsPanel.AddControl(tabControl);

		// Main grid
		var mainGrid = new HorizontalGridControl();
		var col1 = new ColumnContainer(mainGrid) { Width = 40 };
		var col2 = new ColumnContainer(mainGrid);

		col1.AddContent(new MarkupControl(new List<string> { "[grey70]Packages[/]" })
		{ Margin = new Margin(1, 0, 0, 0) });
		col1.AddContent(contextList);

		col2.AddContent(new MarkupControl(new List<string> { "[grey70]Details[/]" })
		{ Margin = new Margin(1, 0, 0, 0) });
		col2.AddContent(detailsPanel);

		mainGrid.AddColumn(col1);
		mainGrid.AddColumn(col2);
		mainGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
		mainGrid.VerticalAlignment = VerticalAlignment.Fill;

		window.AddControl(mainGrid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, contextList, detailsPanel, toolbar, tabControl);
	}

	/// <summary>
	/// Full Tab cycle through packages view.
	/// TabControl tabs have NO focusable children — Tab should skip through header and exit.
	/// Expected: contextList → toolbar → tabControl(header) → contextList (wrap)
	/// </summary>
	[Fact]
	public void PackagesView_TabCycle_TabControlWithNoFocusableChildren()
	{
		var (system, window, contextList, detailsPanel, toolbar, tabControl) =
			BuildPackagesViewLayout();

		var focusLog = new List<string>();

		void LogFocus(string step)
		{
			var fc = window.FocusManager.FocusedControl;
			string name = fc?.GetType().Name ?? "null";
			if (ReferenceEquals(fc, contextList)) name = "contextList";
			else if (ReferenceEquals(fc, toolbar)) name = "toolbar";
			else if (ReferenceEquals(fc, tabControl)) name = "tabControl";
			else if (fc is ButtonControl btn) name = $"btn:{btn.Text}";
			focusLog.Add($"{step}: {name}");
			_out.WriteLine($"{step}: {name}");
		}

		LogFocus("Initial");

		// Press Tab 12 times
		for (int i = 1; i <= 12; i++)
		{
			window.SwitchFocus(backward: false);
			LogFocus($"Tab {i}");
		}

		// Verify cycle includes expected controls
		Assert.Contains(focusLog, s => s.Contains("contextList"));
		// TabControl header should appear (it's IFocusableContainerWithHeader)
		Assert.Contains(focusLog, s => s.Contains("tabControl"));

		// Verify: Tab from tabControl header should NOT get stuck
		// (should exit to contextList, not cycle back to header)
		for (int i = 0; i < focusLog.Count - 1; i++)
		{
			if (focusLog[i].Contains("tabControl"))
			{
				// Next stop after tabControl should NOT be tabControl again
				Assert.False(focusLog[i + 1].Contains("tabControl"),
					$"Tab is stuck on tabControl! [{focusLog[i]}] → [{focusLog[i + 1]}]");
			}
		}
	}

	/// <summary>
	/// Tab from the last focusable control should wrap cleanly to the first.
	/// No phantom stops from TabControl's inactive tab content.
	/// </summary>
	[Fact]
	public void PackagesView_TabFromLast_WrapsToFirst()
	{
		var (system, window, contextList, detailsPanel, toolbar, tabControl) =
			BuildPackagesViewLayout();

		// Focus tabControl (last focusable in the SPC after toolbar)
		window.FocusManager.SetFocus(tabControl, FocusReason.Keyboard);
		_out.WriteLine($"Start: focused={window.FocusManager.FocusedControl?.GetType().Name}");

		// Tab should exit tabControl → exit SPC → wrap to contextList
		window.SwitchFocus(backward: false);
		var after1 = window.FocusManager.FocusedControl;
		_out.WriteLine($"Tab 1: focused={after1?.GetType().Name} is contextList={ReferenceEquals(after1, contextList)}");

		Assert.True(ReferenceEquals(after1, contextList),
			$"Tab from tabControl should wrap to contextList, got: {after1?.GetType().Name}");
	}

	#endregion
}
