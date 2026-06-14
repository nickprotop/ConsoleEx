// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Host-integration matrix for <see cref="CollapsiblePanel"/>: proves the panel works while
/// HOSTING a representative mix of controls AND while PLACED inside the real host containers
/// (Window, ScrollablePanelControl, ColumnContainer/HorizontalGridControl). Every test drives
/// the real render path and asserts on the stripped (ANSI-free) visible content — show/hide,
/// sibling reflow, and independent toggling.
/// </summary>
public class CollapsiblePanelHostIntegrationTests
{
	#region Render helpers

	private static MarkupControl Label(string text) =>
		new MarkupControl(new List<string> { text });

	/// <summary>
	/// Strips ANSI escape codes from output lines to get plain text.
	/// </summary>
	private static string StripAnsiCodes(IEnumerable<string> lines)
	{
		return string.Join("\n", lines.Select(line =>
			System.Text.RegularExpressions.Regex.Replace(line, @"\x1b\[[0-9;]*m", "")));
	}

	/// <summary>
	/// Renders a control inside a fresh test window and returns the plain-text (ANSI-stripped) lines.
	/// Mirrors CollapsiblePanelTests.RenderToLines.
	/// </summary>
	private static List<string> RenderToLines(IWindowControl control, int width, int height)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Width = width, Height = height };
		window.AddControl(control);
		var output = window.RenderAndGetVisibleContent();
		return StripAnsiCodes(output).Split('\n').ToList();
	}

	/// <summary>
	/// Index of the first rendered line whose text contains <paramref name="needle"/>, or -1.
	/// </summary>
	private static int FirstLineContaining(IEnumerable<string> lines, string needle)
	{
		int i = 0;
		foreach (var line in lines)
		{
			if (line.Contains(needle))
				return i;
			i++;
		}
		return -1;
	}

	/// <summary>
	/// Builds a CollapsiblePanel hosting a representative mix of controls:
	/// a markup label, a button, a checkbox, and a nested CollapsiblePanel with its own label.
	/// </summary>
	private static CollapsiblePanel MakeRichPanel(string title) =>
		ControlsFactory.CollapsiblePanel(title)
			.WithWidth(30)
			.AddControl(Label("rich body label"))
			.AddControl(ControlsFactory.Button("Press Me").Build())
			.AddControl(ControlsFactory.Checkbox("Toggle Option").Build())
			.AddControl(
				ControlsFactory.CollapsiblePanel("Nested Section")
					.AddControl(Label("nested body label"))
					.Build())
			.Build();

	#endregion

	#region 1. Window host

	[Fact]
	public void InWindow_ExpandCollapseShowsHidesBody()
	{
		var panel = MakeRichPanel("Rich Panel");

		// Expanded: body children are visible.
		var expanded = RenderToLines(panel, width: 40, height: 16);
		Assert.Contains(expanded, l => l.Contains("Rich Panel"));     // header
		Assert.Contains(expanded, l => l.Contains("rich body label")); // hosted markup label
		Assert.Contains(expanded, l => l.Contains("Press Me"));        // hosted button
		Assert.Contains(expanded, l => l.Contains("Toggle Option"));   // hosted checkbox
		Assert.Contains(expanded, l => l.Contains("Nested Section"));  // nested panel header

		// Collapse: body children disappear, header stays.
		panel.Collapse();
		var collapsed = RenderToLines(panel, width: 40, height: 16);
		Assert.Contains(collapsed, l => l.Contains("Rich Panel"));        // header still present
		Assert.DoesNotContain(collapsed, l => l.Contains("rich body label"));
		Assert.DoesNotContain(collapsed, l => l.Contains("Press Me"));
		Assert.DoesNotContain(collapsed, l => l.Contains("Toggle Option"));
		Assert.DoesNotContain(collapsed, l => l.Contains("Nested Section"));
	}

	#endregion

	#region 2. ScrollablePanel host

	[Fact]
	public void InScrollablePanel_RendersAndToggles()
	{
		var panel = MakeRichPanel("Scrolled Panel");

		var scroller = ControlsFactory.ScrollablePanel()
			.WithHeight(14)
			.AddControl(panel)
			.Build();

		// Expanded inside a scrolled viewport host → body child visible.
		var expanded = RenderToLines(scroller, width: 44, height: 16);
		Assert.Contains(expanded, l => l.Contains("Scrolled Panel"));
		Assert.Contains(expanded, l => l.Contains("rich body label"));
		Assert.Contains(expanded, l => l.Contains("Press Me"));

		// Collapse → body hidden, header remains; host still renders fine.
		panel.Collapse();
		var collapsed = RenderToLines(scroller, width: 44, height: 16);
		Assert.Contains(collapsed, l => l.Contains("Scrolled Panel"));
		Assert.DoesNotContain(collapsed, l => l.Contains("rich body label"));
		Assert.DoesNotContain(collapsed, l => l.Contains("Press Me"));
	}

	#endregion

	#region 3. ColumnContainer host — sibling reflow

	[Fact]
	public void InColumnContainer_SiblingReflowsOnCollapse()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 24);
		var window = new Window(system) { Width = 60, Height = 24 };

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid) { Width = 40 };

		var panel = ControlsFactory.CollapsiblePanel("Reflow Panel")
			.WithWidth(36)
			.AddControl(Label("body line A"))
			.AddControl(Label("body line B"))
			.AddControl(Label("body line C"))
			.Build();

		var sibling = Label("SIBLING MARKER");

		column.AddContent(panel);
		column.AddContent(sibling);
		grid.AddColumn(column);
		window.AddControl(grid);

		// --- Expanded: capture the sibling's row (it sits below the panel body). ---
		var expandedLines = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n').ToList();
		Assert.Contains(expandedLines, l => l.Contains("body line A"));
		Assert.Contains(expandedLines, l => l.Contains("body line C"));
		int siblingRowExpanded = FirstLineContaining(expandedLines, "SIBLING MARKER");
		Assert.True(siblingRowExpanded >= 0, "Sibling label must be present while the panel is expanded.");

		// --- Collapse: the host must reflow the sibling UP. ---
		panel.Collapse();
		var collapsedLines = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n').ToList();

		// Body lines are gone.
		Assert.DoesNotContain(collapsedLines, l => l.Contains("body line A"));
		Assert.DoesNotContain(collapsedLines, l => l.Contains("body line C"));

		// Sibling is still present and has moved to a smaller line index (reflowed up).
		int siblingRowCollapsed = FirstLineContaining(collapsedLines, "SIBLING MARKER");
		Assert.True(siblingRowCollapsed >= 0, "Sibling label must remain present after collapse.");
		Assert.True(siblingRowCollapsed < siblingRowExpanded,
			$"Sibling should reflow UP on collapse: expanded row {siblingRowExpanded}, " +
			$"collapsed row {siblingRowCollapsed}.");
	}

	#endregion

	#region 4. HorizontalGrid column host — width-constrained

	[Fact]
	public void InHorizontalGridColumn_WidthConstrained()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(70, 22);
		var window = new Window(system) { Width = 70, Height = 22 };

		var panel = ControlsFactory.CollapsiblePanel("Grid Column Panel")
			.AddControl(Label("col body text"))
			.Build();

		// Two columns: the panel lives in a width-constrained left column.
		var grid = ControlsFactory.HorizontalGrid()
			.Column(c => c.Width(28).Add(panel))
			.Column(c => c.Add(Label("right column")))
			.Build();

		window.AddControl(grid);

		var lines = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n').ToList();

		// Lays out without error in a width-constrained host: header + body child both render.
		Assert.Contains(lines, l => l.Contains("Grid Column Panel"));
		Assert.Contains(lines, l => l.Contains("col body text"));
		Assert.Contains(lines, l => l.Contains("right column"));

		// The header title renders within the column width (28 cols): the line holding the
		// header must not be wider than the window and the title sits in the left portion.
		var headerLine = lines.First(l => l.Contains("Grid Column Panel"));
		int titleStart = headerLine.IndexOf("Grid Column Panel", StringComparison.Ordinal);
		Assert.True(titleStart < 28,
			$"Header title should render inside the 28-col left column (started at {titleStart}).");
	}

	#endregion

	#region 5. Stacked panels — independent toggling

	[Fact]
	public void StackedPanels_ToggleIndependently()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(50, 24);
		var window = new Window(system) { Width = 50, Height = 24 };

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid) { Width = 44 };

		var first = ControlsFactory.CollapsiblePanel("First Q")
			.WithWidth(40)
			.AddControl(Label("FIRST ANSWER BODY"))
			.Build();

		var second = ControlsFactory.CollapsiblePanel("Second Q")
			.WithWidth(40)
			.AddControl(Label("SECOND ANSWER BODY"))
			.Build();

		column.AddContent(first);
		column.AddContent(second);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Both expanded initially → both bodies visible.
		var both = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n').ToList();
		Assert.Contains(both, l => l.Contains("FIRST ANSWER BODY"));
		Assert.Contains(both, l => l.Contains("SECOND ANSWER BODY"));

		// Collapse ONLY the first — no accordion coordination.
		first.Collapse();
		var afterCollapse = StripAnsiCodes(window.RenderAndGetVisibleContent()).Split('\n').ToList();

		Assert.DoesNotContain(afterCollapse, l => l.Contains("FIRST ANSWER BODY"));
		Assert.Contains(afterCollapse, l => l.Contains("SECOND ANSWER BODY"));

		// Both headers remain present.
		Assert.Contains(afterCollapse, l => l.Contains("First Q"));
		Assert.Contains(afterCollapse, l => l.Contains("Second Q"));

		// And the second still toggles independently.
		Assert.True(second.IsExpanded);
		Assert.False(first.IsExpanded);
	}

	#endregion
}
