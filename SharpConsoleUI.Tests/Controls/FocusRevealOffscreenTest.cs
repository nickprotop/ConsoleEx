// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;
using Xunit.Abstractions;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests;

/// <summary>
/// RED reproduction for changlv's focus-scroll bug: a focusable control OFF the fold inside a
/// NON-scrolling container (a <see cref="CollapsiblePanel"/>, which is NOT an
/// <see cref="IScrollableContainer"/>) that is itself inside a scrolled outer
/// <see cref="ScrollablePanelControl"/> is NOT scrolled into view when focused via the keyboard.
///
/// Why the existing <c>ScrollRangeIntoViewMatrixTests</c> misses this: its <c>Build(nestingDepth)</c>
/// ALWAYS wraps the target in an INNER <see cref="ScrollablePanelControl"/>, so the inner SPC reveals
/// its own leaf and the bug is hidden. changlv's real case (RunDemo17a) uses a CollapsiblePanel — a
/// NON-scroll intermediate — so the OUTER SPC is the button's only scroller.
///
/// Root cause (FocusManager.cs ~83-88 / WindowEventDispatcher.BringIntoFocus): the scroll-into-view
/// walk calls <c>scrollable.ScrollChildIntoView(path[i])</c> where <c>path[i]</c> is the outer SPC's
/// DIRECT child = the whole tall CollapsiblePanel container — never the focused button's real row. The
/// tall container already intersects the viewport, so the "don't yank a spanning container to its top"
/// guard skips scrolling, and the off-fold leaf is never revealed.
///
/// This test uses a NON-scroll CollapsiblePanel as the intermediate (verified: CollapsiblePanel is
/// <c>BaseControl, IContainer, ...</c> — it does NOT implement IScrollableContainer). If the button is
/// revealed the test is GREEN (bug fixed); on current code it stays hidden → RED.
/// </summary>
public class FocusRevealOffscreenTest
{
	private readonly ITestOutputHelper _out;

	public FocusRevealOffscreenTest(ITestOutputHelper output) => _out = output;

	// Small window so the outer SPC overflows and the button sits well below the fold at offset 0.
	private const int WinWidth = 60;
	private const int WinHeight = 20;

	private static List<string> MakeLines(string tag, int n)
	{
		var l = new List<string>(n);
		for (int i = 0; i < n; i++) l.Add($"{tag} {i}");
		return l;
	}

	/// <summary>
	/// Builds: Window → outer ScrollablePanel(Fill, no border) → CollapsiblePanel (NON-scroll,
	/// Borderless header) → [tall filler markup] + [focusable Button "REVEALME"] + [trailing filler].
	/// The CollapsiblePanel is tall enough that, at outer offset 0, the button is BELOW the fold.
	/// </summary>
	private (ConsoleWindowSystem system, Window window, ScrollablePanelControl outer,
			CollapsiblePanel panel, ButtonControl button)
		BuildNonScrollIntermediate()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(WinWidth, WinHeight);
		var window = new Window(system) { Left = 0, Top = 0, Width = WinWidth, Height = WinHeight };

		var outer = new ScrollablePanelControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			AutoScroll = false
		};
		window.AddControl(outer);

		// NON-scroll intermediate container (CollapsiblePanel).
		var panel = ControlsFactory.CollapsiblePanel()
			.WithTitle("Non-Scroll Panel")
			.WithHeaderStyle(CollapsibleHeaderStyle.Borderless)
			.Build();
		outer.AddControl(panel);

		// Tall filler so the panel content extends well past the viewport.
		panel.AddControl(new MarkupControl(MakeLines("fill", 40)));

		// The focusable button we will try to focus — placed after the tall filler so it is below the fold.
		var button = new ButtonControl { Text = "REVEALME" };
		panel.AddControl(button);

		// A little trailing filler so the button is not the very last content row.
		panel.AddControl(new MarkupControl(MakeLines("tail", 5)));

		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, outer, panel, button);
	}

	/// <summary>True if <paramref name="text"/> appears on any composited-screen row.</summary>
	private static bool TextVisibleOnScreen(ConsoleWindowSystem system, string text)
	{
		var snap = system.RenderingDiagnostics?.LastBufferSnapshot;
		if (snap == null) return false;
		for (int y = 0; y < snap.Height; y++)
		{
			var sb = new StringBuilder(snap.Width);
			for (int x = 0; x < snap.Width; x++)
			{
				var s = snap.GetCell(x, y).Character.ToString();
				sb.Append(s.Length > 0 ? s[0] : ' ');
			}
			if (sb.ToString().Contains(text)) return true;
		}
		return false;
	}

	[Fact]
	public void FocusOffscreenButton_InNonScrollContainer_ScrollsItIntoView()
	{
		var (system, window, outer, panel, button) = BuildNonScrollIntermediate();

		// Confirm the intermediate is a NON-scroll container (the whole point of the repro).
		Assert.False(panel is IScrollableContainer,
			"precondition: the intermediate CollapsiblePanel must NOT be an IScrollableContainer.");

		// The outer must overflow so it is the (only) scroller.
		Assert.True(outer.TotalContentHeight > outer.ViewportHeight,
			$"precondition: outer must overflow (content={outer.TotalContentHeight} viewport={outer.ViewportHeight}).");

		// Scroll the outer to the TOP so the button is below the fold.
		outer.ScrollToTop();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int before = outer.VerticalScrollOffset;
		int viewport = outer.ViewportHeight;

		// PRECONDITION: the button is genuinely OFF-viewport. ActualY is window-content-relative and only
		// set to an on-screen row when the button is actually painted inside the viewport; a below-fold
		// button paints outside [0, viewport). We ALSO confirm the button text is NOT on the composited
		// screen — the source-of-truth visibility check.
		int btnRowBefore = button.ActualY;
		bool onScreenBefore = TextVisibleOnScreen(system, "REVEALME");
		_out.WriteLine($"before: outer offset={before} viewport={viewport} content={outer.TotalContentHeight}; " +
			$"button.ActualY={btnRowBefore}; buttonTextVisible={onScreenBefore}");

		Assert.False(onScreenBefore,
			$"precondition: the button must be OFF-screen before focusing (its text must not be composited). " +
			$"button.ActualY={btnRowBefore}, viewport={viewport}, offset={before}.");

		// Focus the button via the REAL keyboard focus path (reason=Keyboard walks IScrollableContainer
		// ancestors and calls ScrollChildIntoView on each — the exact path the bug lives in).
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		window.FocusManager.SetFocus(button, FocusReason.Keyboard);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Confirm focus actually landed on the button — a non-focus must not be reported as revealed/hidden.
		bool focusLanded = window.FocusManager.IsFocused(button);
		Assert.True(focusLanded,
			$"focus did not land on the button (Focused={window.FocusManager.FocusedControl?.GetType().Name}); " +
			$"a non-focus is NOT evidence for or against the repro.");

		int after = outer.VerticalScrollOffset;
		int btnRowAfter = button.ActualY;
		bool onScreenAfter = TextVisibleOnScreen(system, "REVEALME");
		_out.WriteLine($"after: outer offset={after} (before={before}, delta={after - before}); " +
			$"button.ActualY={btnRowAfter}; buttonTextVisible={onScreenAfter}; focusLanded={focusLanded}");

		// REPRODUCTION ASSERTION: the button must now be VISIBLE (revealed by scrolling the outer).
		// On current (buggy) code the outer does NOT scroll to reveal the off-fold leaf inside the
		// non-scroll container → the button stays hidden → this assert FAILS (RED == reproduced).
		Assert.True(onScreenAfter,
			$"BUG: focusing an off-fold button inside a NON-scroll CollapsiblePanel did not scroll the " +
			$"outer ScrollablePanel to reveal it. offset before={before} after={after}; button.ActualY={btnRowAfter}.");
	}

	[Fact]
	public void ArrowKey_ToOffscreenButton_InNonScrollContainer_ScrollsItIntoView()
	{
		// Two buttons in the non-scroll panel, both below the fold. Focus the FIRST via keyboard (which
		// should already reveal it), then confirm arrow-key navigation between them keeps focus visible.
		var system = TestWindowSystemBuilder.CreateTestSystem(WinWidth, WinHeight);
		var window = new Window(system) { Left = 0, Top = 0, Width = WinWidth, Height = WinHeight };

		var outer = new ScrollablePanelControl { VerticalAlignment = VerticalAlignment.Fill, AutoScroll = false };
		window.AddControl(outer);

		var panel = ControlsFactory.CollapsiblePanel()
			.WithTitle("Non-Scroll Panel")
			.WithHeaderStyle(CollapsibleHeaderStyle.Borderless)
			.Build();
		outer.AddControl(panel);

		panel.AddControl(new MarkupControl(MakeLines("fill", 40)));

		// A toolbar hosting two focusable buttons (Right arrow moves btn1 -> btn2).
		var btn1 = ControlsFactory.Button().WithText("BTNONE").Build();
		var btn2 = ControlsFactory.Button().WithText("BTNTWO").Build();
		var toolbar = ControlsFactory.Toolbar().WithStickyPosition(StickyPosition.None).Build();
		toolbar.AddItem(btn1);
		toolbar.AddItem(btn2);
		panel.AddControl(toolbar);
		panel.AddControl(new MarkupControl(MakeLines("tail", 5)));

		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		Assert.False(panel is IScrollableContainer);
		Assert.True(outer.TotalContentHeight > outer.ViewportHeight);

		outer.ScrollToTop();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int before = outer.VerticalScrollOffset;
		bool btn2VisibleBefore = TextVisibleOnScreen(system, "BTNTWO");
		_out.WriteLine($"arrow: before offset={before} viewport={outer.ViewportHeight}; btn2 visible before={btn2VisibleBefore}");
		Assert.False(btn2VisibleBefore, "precondition: btn2 must be off-screen before the arrow key.");

		// Land keyboard focus on btn1 (already the buggy path — may or may not reveal it).
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);
		system.Render.UpdateDisplay();
		Assert.True(window.FocusManager.IsFocused(btn1),
			$"precondition: btn1 must be focused before the arrow (Focused={window.FocusManager.FocusedControl?.GetType().Name}).");

		// Right arrow via the REAL input dispatch → toolbar navigation → SetFocus(btn2, Keyboard).
		var right = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false);
		system.InputStateService.EnqueueKey(right);
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		bool movedToBtn2 = window.FocusManager.IsFocused(btn2);
		Assert.True(movedToBtn2,
			$"the Right arrow did not move focus btn1 -> btn2 (Focused={window.FocusManager.FocusedControl?.GetType().Name}); " +
			$"a non-registered move is NOT evidence about the bug.");

		int after = outer.VerticalScrollOffset;
		bool btn2VisibleAfter = TextVisibleOnScreen(system, "BTNTWO");
		_out.WriteLine($"arrow: after offset={after} (before={before}, delta={after - before}); btn2 visible after={btn2VisibleAfter}; movedToBtn2={movedToBtn2}");

		// The now-focused btn2 must be revealed. On current code it stays hidden → RED.
		Assert.True(btn2VisibleAfter,
			$"BUG: arrow-navigating to an off-fold button inside a NON-scroll CollapsiblePanel did not scroll " +
			$"the outer to reveal it. offset before={before} after={after}.");
	}

	/// <summary>
	/// changlv's REAL topology (RunDemo17a): the focusable button is inside a TOOLBAR inside the
	/// CollapsiblePanel — one level deeper than the direct-in-panel case above. Focusing an off-fold
	/// toolbar button must scroll the OUTER panel to reveal it. Reuses <c>Issue67RowJitterTest.BuildDemo17a</c>
	/// so the nesting matches the demo exactly.
	/// </summary>
	[Fact]
	public void FocusToolbarButton_Offscreen_ScrollsOuterToRevealIt()
	{
		var (system, window, outputPanel, btn1, btn2, _label) = Issue67RowJitterTest.BuildDemo17a(100, 24);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		while (outputPanel.VerticalScrollOffset > 0) outputPanel.ScrollVerticalBy(-1);  // to top; toolbar btns below fold
		system.Render.UpdateDisplay();
		int before = outputPanel.VerticalScrollOffset;
		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);
		system.Render.UpdateDisplay();
		Assert.True(window.FocusManager.IsFocused(btn1), "btn1 must be focused");
		Assert.True(outputPanel.VerticalScrollOffset > before,
			$"focusing an off-screen TOOLBAR button must scroll the outer panel to reveal it (before={before} after={outputPanel.VerticalScrollOffset})");
	}
}
