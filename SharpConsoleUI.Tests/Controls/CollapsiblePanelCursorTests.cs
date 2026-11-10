// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// End-to-end tests for the terminal-cursor visibility/position contract of a focusable,
/// cursor-providing child (PromptControl) hosted inside a <see cref="CollapsiblePanel"/> body.
///
/// These exercise the SAME window-level path the running app uses
/// (<see cref="WindowEventDispatcher.HasInteractiveContent"/> →
/// <c>TranslateLogicalCursorToWindow</c>), so they reproduce the real DemoApp scenario where the
/// prompt sits below the panel header and several sibling controls. The panel must report the
/// cursor in its OWN content space (accumulating the body child's offset), otherwise the
/// window-level logic places the terminal cursor on the wrong row and the cursor disappears.
/// </summary>
public class CollapsiblePanelCursorTests
{
	private readonly ITestOutputHelper _out;

	public CollapsiblePanelCursorTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	private static bool CursorVisible(Window window, out System.Drawing.Point pos)
		=> window.EventDispatcher!.HasInteractiveContent(out pos);

	/// <summary>
	/// The on-screen row (window coordinates) where the prompt actually paints, taken from the
	/// prompt's own DOM node AbsoluteBounds (+1 for the window border).
	/// </summary>
	private static int ExpectedPromptWindowRow(Window window, PromptControl prompt)
	{
		var node = window.GetLayoutNode(prompt);
		Assert.NotNull(node);
		return node!.AbsoluteBounds.Y + 1;
	}

	// ---------------------------------------------------------------------
	// Case A: Window → CollapsiblePanel → siblings + Prompt  (no SPC host)
	// Isolates whether the panel itself composes the cursor correctly.
	// ---------------------------------------------------------------------

	[Fact]
	public void Cursor_Visible_AndOnPromptRow_PanelWithoutScrollHost()
	{
		var panel = new CollapsiblePanel { Title = "Body" };
		panel.AddControl(new MarkupControl(new List<string> { "above0" }));
		panel.AddControl(new MarkupControl(new List<string> { "above1" }));
		var prompt = new PromptControl { Prompt = "> " };
		panel.AddControl(prompt);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		int expectedRow = ExpectedPromptWindowRow(window, prompt);
		_out.WriteLine($"[no-SPC] visible={visible} pos={pos} expectedRow={expectedRow}");

		Assert.True(visible, "Cursor must be visible when the focused prompt is in the panel body.");
		Assert.Equal(expectedRow, pos.Y);
	}

	// ---------------------------------------------------------------------
	// Case B (THE FAILING CASE): Window → SPC → CollapsiblePanel → siblings + Prompt
	// Mirrors the DemoApp "Interactive body" panel inside the root scroll panel.
	// ---------------------------------------------------------------------

	[Fact]
	public void Cursor_Visible_AndOnPromptRow_PanelInsideScrollPanel()
	{
		var panel = new CollapsiblePanel { Title = "Interactive body" };
		// 4 siblings above the prompt, matching the demo (status, buttonRow, toggleMe, verboseLogging).
		for (int i = 0; i < 4; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"sibling{i}" }));
		var prompt = new PromptControl { Prompt = "search > " };
		panel.AddControl(prompt);

		var root = new ScrollablePanelControl { Height = 20 };
		// A couple of rows above the panel inside the scroll panel.
		root.AddControl(new MarkupControl(new List<string> { "intro" }));
		root.AddControl(panel);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(root);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);

		// Where the prompt actually paints, in window rows:
		//   window border (1)
		//   + the panel's screen row inside the window content (panel.ActualY)
		//   + the panel's internal offset to the prompt: header (1) + the 4 single-row siblings (4).
		// Inside a self-painting ScrollablePanelControl the prompt has no usable own layout node, so
		// this is derived from the panel's actual position plus its known body layout.
		int promptOffsetInPanel = 1 /* header */ + 4 /* siblings above the prompt */;
		int expectedRow = 1 + panel.ActualY + promptOffsetInPanel;
		_out.WriteLine($"[SPC] visible={visible} pos={pos} expectedRow={expectedRow} " +
			$"panelActualY={panel.ActualY} scroll={root.VerticalScrollOffset}");

		Assert.True(visible, "Cursor must be visible when the focused prompt is in the panel body inside a scroll panel.");
		Assert.Equal(expectedRow, pos.Y);
		// X = window border (1) + prompt's prompt-text width ("search > " = 9 cols), at panel left.
		Assert.Equal(1 + 9, pos.X);
	}

	// ---------------------------------------------------------------------
	// Case C: collapsed panel hides the cursor entirely.
	// ---------------------------------------------------------------------

	[Fact]
	public void Cursor_Hidden_WhenPanelCollapsed()
	{
		var panel = new CollapsiblePanel { Title = "Body", IsExpanded = true };
		var prompt = new PromptControl { Prompt = "> " };
		panel.AddControl(prompt);

		// The real detail pane's inset from the rail divider: a LEFT MARGIN on
		// the scroll panel itself. The cursor path reads the host's layout node
		// and adds the panel's own reported offset, so a margin on the host is
		// the one place the two can disagree.
		var root = new ScrollablePanelControl { Height = 20, Margin = new Margin(1, 0, 0, 0) };
		root.AddControl(panel);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(root);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();
		Assert.True(CursorVisible(window, out _), "precondition: visible when expanded");

		panel.IsExpanded = false;
		window.RenderAndGetVisibleContent();

		Assert.False(CursorVisible(window, out _), "Cursor must be hidden when the panel is collapsed.");
	}

	// ---------------------------------------------------------------------
	// Case D: the prompt is not a direct child of the panel but sits in a
	// container inside it.
	//
	// This is cxagent's F8 settings dialog, Providers tab: every field is a
	// label/control pair in a GridControl, that grid is the panel's only
	// child, and the panel is one section inside a ScrollablePanelControl.
	// The endpoint field took keys and painted them, with no cursor.
	//
	// The panel looked for the focused control among its OWN children and
	// asked whether each one was focused. A grid is never focused itself --
	// it is a transparent focus scope whose cells hold the focus -- so the
	// search found nothing, the panel reported no cursor, and the terminal
	// hid it. Input routing is a separate path, which is why the field still
	// accepted typing and the bug read as cosmetic.
	//
	// GridControl and ScrollablePanelControl both answer this by asking
	// whether a child is on the focus PATH, not whether it is the focus.
	// ---------------------------------------------------------------------

	[Fact]
	public void Cursor_Visible_WhenPromptIsNestedInAContainerInsideThePanel()
	{
		// EXACTLY what cxagent's ListDetail.Input builds: an empty label plus
		// one styled padding space, and Stretch so the field fills its cell.
		var prompt = new PromptControl
		{
			Prompt = "[on rgb(68,68,68)] [/]",
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};

		// The field grid cxagent's settings form actually builds: one starred
		// column and an Auto row per control, holding a bold label, a muted
		// description, then the editor. The row definitions matter -- a grid
		// with none measures to zero height, nothing inside it is arranged,
		// and the cursor is then missing for an unrelated reason.
		var fields = new GridControl { HorizontalAlignment = HorizontalAlignment.Stretch };
		fields.ColumnDefinitions.Add(GridLength.Star(1, min: 24));
		var cells = new IWindowControl[]
		{
			new MarkupControl(new List<string> { "[bold]Endpoint[/]" }),
			new MarkupControl(new List<string> { "The server's base URL, usually ending in /v1." }) { Wrap = true },
			prompt,
		};
		for (int row = 0; row < cells.Length; row++)
		{
			fields.RowDefinitions.Add(GridLength.Auto());
			fields.Place(cells[row], row, 0);
		}

		// A BORDERED panel, as the real dialog builds it (ListDetail.Section
		// uses PanelControl with BorderStyle.Rounded). The border matters: it
		// insets the body by one column, so a cursor that ignores the inset is
		// one column off -- invisible in a borderless panel, wrong here.
		var panel = new PanelControl
		{
			BorderStyle = BorderStyle.Rounded,
			Header = " Connection ",
			// The real section's padding: one column inside the border.
			Padding = new Padding(1, 1, 1, 0),
		};
		panel.AddControl(fields);

		// The real detail pane's inset from the rail divider: a LEFT MARGIN on
		// the scroll panel itself. The cursor path reads the host's layout node
		// and adds the panel's own reported offset, so a margin on the host is
		// the one place the two can disagree.
		var root = new ScrollablePanelControl { Height = 20, Margin = new Margin(1, 0, 0, 0) };
		root.AddControl(panel);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(root);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		bool visible = CursorVisible(window, out var pos);
		_out.WriteLine($"[nested] visible={visible} pos={pos} panelActualY={panel.ActualY} "
			+ $"gridH={fields.ActualHeight} promptH={prompt.ActualHeight}");

		// Guard the test itself: if the grid collapsed to nothing, a hidden
		// cursor says nothing about the panel's focused-child lookup.
		Assert.True(fields.ActualHeight > 0, "precondition: the field grid must have height");

		Assert.True(visible,
			"Cursor must be visible when the focused prompt sits in a container inside the panel body, "
			+ "not only when it is a direct child of the panel.");

		// The cursor belongs on the prompt's own row: the panel header, then
		// the label and description rows above the editor. Asserting the row
		// keeps a cursor that merely reappears somewhere from passing.
		int expectedRow = 1 /* window border */ + panel.ActualY + 1 /* header */
			+ 1 /* panel top padding */ + 2 /* label, description */;
		Assert.Equal(expectedRow, pos.Y);

		// THE EMPTY FIELD, before anything is typed: the caret belongs on the
		// first column a character will occupy, which is one past the prompt
		// (here the styled padding space the real dialog uses to make an empty
		// field visible). Pinned separately because a caret that is correct
		// once text exists can still start in the wrong place.
		int groundStart = -1;
		var emptyRows = window.RenderAndGetVisibleContent();
		{
			var plain = System.Text.RegularExpressions.Regex.Replace(
				emptyRows[expectedRow - 1], "\\e\\[[0-9;]*m", "");
			// Content column of the prompt's own cell: panel border, panel
			// padding and the scroll panel's margin all sit left of it.
			groundStart = root.Margin.Left + 1 /* panel border */ + 1 /* panel padding */;
			Assert.True(groundStart < plain.Length, "precondition: the input row must be wide enough");
		}
		int promptWidth = SharpConsoleUI.Parsing.MarkupParser.StripLength(prompt.Prompt!);
		Assert.Equal(groundStart + promptWidth + 1 /* window border */, pos.X);
		// THE HORIZONTAL HALF, which a visibility-only assert cannot see: the
		// cursor must sit on its own text, not one column off it. The scroll
		// panel's left margin is what made the two disagree -- content paints
		// at Margin.Left + ContentInsetLeft, while the cursor counted only the
		// inset.
		prompt.Input = "XYZ";
		window.RenderAndGetVisibleContent();
		Assert.True(CursorVisible(window, out var typed), "cursor must stay visible once the field has text");

		var rendered = window.RenderAndGetVisibleContent();
		int textRow = -1, textCol = -1;
		for (int i = 0; i < rendered.Count; i++)
		{
			var plain = System.Text.RegularExpressions.Regex.Replace(rendered[i], "\\e\\[[0-9;]*m", "");
			int at = plain.IndexOf("XYZ", System.StringComparison.Ordinal);
			if (at >= 0) { textRow = i; textCol = at; break; }
		}
		Assert.True(textRow >= 0, "precondition: the typed text must be painted somewhere");

		// Content rows/cols are inside the window border, so window = content + 1.
		// The caret belongs just past the text it follows.
		Assert.Equal(textRow + 1, typed.Y);
		Assert.Equal(textCol + 1 + 3, typed.X);
	}
}
