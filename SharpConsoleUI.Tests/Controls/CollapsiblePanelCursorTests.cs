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

		var root = new ScrollablePanelControl { Height = 20 };
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
}
