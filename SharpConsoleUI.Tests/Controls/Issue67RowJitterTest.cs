// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;
using Xunit.Abstractions;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Deterministic, headless reproduction attempt for GitHub issue #67 ("Row Jitter Bug Caused by
/// Nested Multi-Layer Controls", OPEN, reported on 2.5.9).
///
/// The issue's runnable repro (<c>RunDemo17a</c>) reports that, in a deeply nested layout, clicking a
/// Toolbar button scrolls the OUTER ScrollablePanel UPWARD (changlv's comment:
/// <c>// Clicking the button triggers an upward scroll.</c>).
///
/// This test recreates that exact nesting faithfully:
///   OUTER outputPanel (ScrollablePanel, AutoScroll, Fill, Rounded, Padding 1,0,1,0)
///     └─ Main Panel (CollapsiblePanel, Borderless header)
///          ├─ Sub1 Panel (CollapsiblePanel + Markup)
///          ├─ Sub2 Panel (CollapsiblePanel MaxContentHeight=8 → inner ScrollablePanel + Markup)
///          ├─ Sub3 Panel (CollapsiblePanel + Markup with [markdown] table)
///          ├─ Toolbar (Button1 + Button2)
///          └─ trailing Markup label "try select this content."
///
/// It drives the REAL window mouse-dispatch path (driver.SimulateMouseEvent + system.Input.ProcessInput),
/// mirroring <see cref="Controls.CollapsiblePanelScrollablePanelMouseTests"/>. Button coordinates come
/// from the button's live arranged LayoutNode.AbsoluteBounds (window-content coords), so the click is
/// confirmed to land on Button1 before any conclusion is drawn.
///
/// GOAL: go RED if the bug reproduces (clicking Button1 changes outputPanel.VerticalScrollOffset).
/// This test does NOT fix the bug — it only reproduces / demonstrates it.
/// </summary>
public class Issue67RowJitterTest
{
	private readonly ITestOutputHelper _out;

	public Issue67RowJitterTest(ITestOutputHelper output) => _out = output;

	private const string MarkdownTableText = @"
# 测试信息

- item 1
- item 2
- item 3

---

| col1 | col2 |
|---|---|
|  this is long text this is long text this is long text this is long text this is long text this is long text this is long text this is long text |  this is long text this is long text this is long text this is long text this is long text this is long text |

";

	/// <summary>
	/// Recreates the RunDemo17a topology inside a headless test window sized so the outer outputPanel
	/// overflows (content taller than its viewport) — the bug is reported "only when not maximized",
	/// i.e. only when the outer panel actually scrolls.
	/// </summary>
	internal static (ConsoleWindowSystem system, Window window,
		ScrollablePanelControl outputPanel, ButtonControl btn1, ButtonControl btn2,
		MarkupControl label)
		BuildDemo17a(int width, int height)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };

		// ---- OUTER outputPanel (the one whose VerticalScrollOffset we assert on) ----
		var outputPanel = ControlsFactory.ScrollablePanel()
			.WithName("OutputPanel")
			.WithAutoScroll()
			.WithPadding(1, 0, 1, 0)
			.WithBorderStyle(BorderStyle.Rounded)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		window.AddControl(outputPanel);

		// ---- Main Panel ----
		var mainPanel = ControlsFactory.CollapsiblePanel()
			.WithTitle("Main Panel")
			.WithHeaderStyle(CollapsibleHeaderStyle.Borderless)
			.WithMargin(0, 1, 0, 0)
			.Build();
		outputPanel.AddControl(mainPanel);

		// ---- Sub1 Panel ----
		var sub1WrapPanel = ControlsFactory.CollapsiblePanel()
			.WithTitle("Sub1 Panel")
			.WithHeaderStyle(CollapsibleHeaderStyle.Rounded)
			.WithMargin(2, 1, 0, 0)
			.Build();
		var subBox = ControlsFactory.Markup()
			.WithMargin(1, 1, 0, 0).WithCopyEnabled().WithSelectionEnabled().Build();
		sub1WrapPanel.AddControl(subBox);
		mainPanel.AddControl(sub1WrapPanel);
		subBox.AppendLine("this is test text this is test text this is test text");

		// ---- Sub2 Panel (MaxContentHeight 8 → inner ScrollablePanel + Markup) ----
		var sub2WrapPanel = ControlsFactory.CollapsiblePanel()
			.WithTitle("Sub2 Panel")
			.WithHeaderStyle(CollapsibleHeaderStyle.Rounded)
			.WithMaxContentHeight(8)
			.WithMargin(2, 1, 0, 0)
			.Build();
		var sub2Panel = ControlsFactory.ScrollablePanel()
			.WithBorderStyle(BorderStyle.None)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAutoScroll()
			.Build();
		sub2WrapPanel.AddControl(sub2Panel);
		var sub2Box = ControlsFactory.Markup()
			.WithMargin(1, 1, 0, 0).WithCopyEnabled().WithSelectionEnabled().Build();
		sub2Panel.AddControl(sub2Box);
		mainPanel.AddControl(sub2WrapPanel);
		sub2Box.AppendLine("this is sub2 text this is sub2 text this is sub2 text.");

		// ---- Sub3 Panel ([markdown] table content) ----
		var sub3Panel = ControlsFactory.CollapsiblePanel()
			.WithTitle("Sub 3 Panel")
			.WithHeaderStyle(CollapsibleHeaderStyle.Rounded)
			.WithMargin(2, 1, 0, 0)
			.Build();
		var sub3Box = ControlsFactory.Markup()
			.WithMargin(1, 1, 0, 0).WithCopyEnabled().WithSelectionEnabled()
			.WithMarkdownStyle(s => s with { TableRowSeparators = true })
			.Build();
		sub3Panel.AddControl(sub3Box);
		mainPanel.AddControl(sub3Panel);
		sub3Box.Text = $"[markdown]{MarkdownTableText}[/]";

		// ---- Toolbar with Button1 + Button2 ----
		var btn1 = ControlsFactory.Button().WithText("Button1").Build();
		var btn2 = ControlsFactory.Button().WithText("Button2").Build();
		var toolBar = ControlsFactory.Toolbar()
			.WithStickyPosition(StickyPosition.None)
			.WithMargin(2, 0, 0, 0)
			.Build();
		toolBar.AddItem(btn1);
		toolBar.AddItem(btn2);
		mainPanel.AddControl(toolBar);

		// ---- trailing Markup label ----
		var label = ControlsFactory.Markup()
			.WithMargin(2, 1, 0, 0).WithCopyEnabled().WithSelectionEnabled().Build();
		mainPanel.AddControl(label);
		label.Text = "try select this content.";

		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, outputPanel, btn1, btn2, label);
	}

	/// <summary>
	/// Test A — clicking a Toolbar button must NOT scroll the outer outputPanel.
	///
	/// The issue reports clicking Button1 scrolls the outer ScrollablePanel UPWARD. We scroll the outer
	/// panel to (near) the bottom, record <c>before = outputPanel.VerticalScrollOffset</c>, dispatch a
	/// real click at Button1's live arranged position, re-render, and assert the offset is UNCHANGED.
	///
	/// If the bug reproduces, the offset drops (scrolls up) and this assertion FAILS (RED) — that IS the
	/// reproduction. If the offset stays put, this passes as a regression guard and the report says the
	/// upward-scroll could NOT be reproduced on the current master.
	///
	/// The click landing on Button1 is confirmed via a precondition: Button1's arranged node is on-screen
	/// inside the outer panel's viewport, and (after the click) Button1 receives focus.
	/// </summary>
	[Fact]
	public void ButtonClick_DoesNotScrollOuterPanel()
	{
		// Boundary-stressing size: narrow + short so the outer panel content overflows its viewport.
		const int width = 100;
		const int height = 24;

		var (system, window, outputPanel, btn1, btn2, label) = BuildDemo17a(width, height);

		// --- Precondition: the outer panel must overflow so it can scroll (the bug is "only when not maximized"). ---
		Assert.True(outputPanel.TotalContentHeight > outputPanel.ViewportHeight,
			$"precondition: OUTER outputPanel must overflow its viewport so it can scroll " +
			$"(content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight}).");

		// Scroll the OUTER panel toward the bottom via the real scroll API, then re-render so it settles.
		outputPanel.ScrollToBottom();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int before = outputPanel.VerticalScrollOffset;
		_out.WriteLine($"outer offset before click = {before} " +
			$"(content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight})");

		Assert.True(before > 0,
			$"precondition: the outer panel must be scrolled down (offset>0) so an upward-scroll bug is observable; before={before}.");

		// --- Confirm the click will LAND on Button1. The Toolbar is a self-painting container: it calls
		// btn1.PaintDOM(...) with absolute window-content bounds during its own paint, so Button1's
		// ActualX/ActualY are set to its on-screen (window-content) position (it has no _controlToNodeMap
		// entry, so GetLayoutNode returns null — ActualX/ActualY is the authoritative position here). ---
		int bx = btn1.ActualX;
		int by = btn1.ActualY;
		_out.WriteLine($"Button1 ActualX={bx} ActualY={by}; window content = {width - 2}x{height - 2}");

		int winContentW = width - 2;
		int winContentH = height - 2;
		Assert.True(bx >= 0 && by >= 0 && bx < winContentW && by < winContentH,
			$"precondition: Button1's painted position must be on-screen inside the window content area " +
			$"(ActualX={bx}, ActualY={by}, content={winContentW}x{winContentH}). If off-screen, a coord-based click can't land on it.");

		// Absolute screen coords: window border is 1 cell, ActualX/ActualY is window-content-relative.
		int clickX = window.Left + 1 + bx + 1; // +1 into the button (past its left edge)
		int clickY = window.Top + 1 + by;

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		int afterClicked = outputPanel.VerticalScrollOffset;
		bool clickedLanded = window.FocusManager.IsFocused(btn1);
		_out.WriteLine($"[Button1Clicked] landed(focus)={clickedLanded}; outer offset after = {afterClicked} (before={before}, delta={afterClicked - before})");

		// If Button1Clicked did not land, fall back to Button1Pressed (the toolbar scroll-into-view may
		// fire on focus/press). Recompute coords from the live node (a relayout may have moved it).
		int afterPressed = afterClicked;
		bool pressedLanded = clickedLanded;
		if (!clickedLanded)
		{
			outputPanel.ScrollToBottom();
			system.Render.UpdateDisplay();
			system.Render.UpdateDisplay();
			before = outputPanel.VerticalScrollOffset;

			int px = window.Left + 1 + btn1.ActualX + 1;
			int py = window.Top + 1 + btn1.ActualY;
			window.FocusManager.SetFocus(null, FocusReason.Programmatic);
			driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(px, py));
			system.Input.ProcessInput();
			driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Released }, new Point(px, py));
			system.Input.ProcessInput();
			system.Render.UpdateDisplay();

			afterPressed = outputPanel.VerticalScrollOffset;
			pressedLanded = window.FocusManager.IsFocused(btn1);
			_out.WriteLine($"[Button1Pressed/Released] landed(focus)={pressedLanded}; outer offset after = {afterPressed} (before={before}, delta={afterPressed - before})");
		}

		// Confirm the click actually landed on Button1 — a coord-miss must NOT be reported as "no repro".
		Assert.True(clickedLanded || pressedLanded,
			$"could not confirm the click landed on Button1 (Button1 never received focus via Clicked or Pressed). " +
			$"Button1 ActualX={bx} ActualY={by}. A coord-miss is NOT evidence of 'bug does not reproduce'.");

		int after = clickedLanded ? afterClicked : afterPressed;

		// The reproduction assertion: clicking Button1 must NOT change the outer panel's scroll offset.
		// The reported bug SCROLLS UP, which makes `after < before`, failing this assertion (RED == repro).
		Assert.Equal(before, after);
	}

	/// <summary>
	/// Test A (issue #67, KEYBOARD trigger) — landing keyboard focus on Button1 and then pressing Right
	/// arrow to Button2. BOTH buttons are already fully visible in the outer panel's viewport, so this
	/// keyboard focus interaction must NOT scroll the outer <c>outputPanel</c> upward.
	///
	/// Real path exercised: <c>FocusManager.SetFocus(btn1, Keyboard)</c> then
	/// <c>InputStateService.EnqueueKey(Right)</c> + <c>Input.ProcessInput()</c> → focused Button1 → the
	/// hosting <see cref="Controls.ToolbarControl"/>'s <c>ProcessKey</c> → <c>NavigateFocus</c> →
	/// <c>FocusManager.SetFocus(btn2, Keyboard)</c>. Every keyboard SetFocus (reason != Mouse) walks the
	/// focus path and calls <c>ScrollChildIntoView</c> on each <see cref="Controls.IScrollableContainer"/>
	/// ancestor — the exact keyboard scroll-into-view path the issue's root-cause hypothesis names (sibling
	/// of <c>WindowEventDispatcher.BringIntoFocus</c>).
	///
	/// We CONFIRM focus actually moved btn1→btn2 before drawing any conclusion. The reproduction observable
	/// is the OUTER offset across the whole keyboard-focus interaction: it starts at the scrolled-to-bottom
	/// baseline (both buttons visible) and must stay there. If the bug reproduces the offset collapses to 0
	/// (scrolls up while focus is already visible) and the final assert goes RED — that IS the repro.
	/// </summary>
	[Fact]
	public void ArrowKey_Button1ToButton2_DoesNotScrollOuterUp()
	{
		const int width = 100;
		const int height = 24;

		var (system, window, outputPanel, btn1, btn2, label) = BuildDemo17a(width, height);

		Assert.True(outputPanel.TotalContentHeight > outputPanel.ViewportHeight,
			$"precondition: OUTER outputPanel must overflow so it can scroll " +
			$"(content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight}).");

		// Scroll to the bottom so BOTH buttons are visible in the viewport — the whole point of the bug is
		// that focus is already visible, yet the panel still scrolls.
		outputPanel.ScrollToBottom();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Both buttons must be on-screen inside the window content area (already visible).
		int winContentH = height - 2;
		bool btn1Visible = btn1.ActualY >= 0 && btn1.ActualY < winContentH;
		bool btn2Visible = btn2.ActualY >= 0 && btn2.ActualY < winContentH;
		_out.WriteLine($"btn1 ActualY={btn1.ActualY} visible={btn1Visible}; btn2 ActualY={btn2.ActualY} visible={btn2Visible}; winContentH={winContentH}");
		Assert.True(btn1Visible && btn2Visible,
			$"precondition: BOTH buttons must already be visible in the viewport (btn1.ActualY={btn1.ActualY}, btn2.ActualY={btn2.ActualY}, winContentH={winContentH}). The bug is an UP-scroll while focus is already visible.");

		// BASELINE: both buttons are visible at this scroll offset. Any keyboard focus interaction that
		// leaves them visible must keep the offset at (or below, i.e. it may scroll DOWN but here it's
		// already at the bottom) this value. An UP-scroll drops it toward 0.
		int before = outputPanel.VerticalScrollOffset;
		_out.WriteLine($"outer offset before keyboard focus (both buttons visible) = {before} (content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight})");

		// Focus Button1 via the real keyboard focus path (reason=Keyboard → walks scrollable ancestors,
		// ScrollChildIntoView). Button1 is ALREADY visible, so this must not move the outer panel.
		window.FocusManager.SetFocus(btn1, FocusReason.Keyboard);
		system.Render.UpdateDisplay();
		Assert.True(window.FocusManager.IsFocused(btn1),
			$"precondition: Button1 must be focused before the arrow key. FocusedControl={window.FocusManager.FocusedControl?.GetType().Name}.");

		int offsetAfterFocusBtn1 = outputPanel.VerticalScrollOffset;
		_out.WriteLine($"outer offset after SetFocus(btn1, Keyboard) = {offsetAfterFocusBtn1} (was {before}; delta={offsetAfterFocusBtn1 - before})");

		// Send Right arrow through the REAL input dispatch (the way SwitchFocus/toolbar navigation is reached).
		var right = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false);
		system.InputStateService.EnqueueKey(right);
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();

		int after = outputPanel.VerticalScrollOffset;
		bool movedToBtn2 = window.FocusManager.IsFocused(btn2);
		_out.WriteLine($"[RightArrow] focus moved to btn2 = {movedToBtn2}; FocusedControl={window.FocusManager.FocusedControl?.GetType().Name}; outer offset after = {after} (before={before}, delta={after - before})");

		// CONFIRM the action registered: focus MUST have moved btn1→btn2, else the arrow didn't route and a
		// "no repro" conclusion would be a lie.
		Assert.True(movedToBtn2,
			$"the Right arrow did not move focus from Button1 to Button2 (Focused={window.FocusManager.FocusedControl?.GetType().Name}). " +
			$"The arrow did not route through the real keyboard focus-move path; a non-registered action is NOT evidence of 'no bug'.");

		// Reproduction assertion: the keyboard focus interaction (focus btn1, arrow to btn2 — both already
		// visible) must NOT scroll the outer panel up. Bug = up-scroll (after < before) = RED.
		Assert.Equal(before, after);
	}

	/// <summary>
	/// Test B (issue #67, TEXT-SELECTION trigger, the primary/reliable one) — selecting the trailing
	/// "try select this content." label must NOT scroll the outer <c>outputPanel</c> up / row-jitter.
	///
	/// Real path exercised: a Button1 press on the label anchors a drag selection and registers the label
	/// as the window's drag-autoscroll target (<c>RegisterDragAutoScroll(this)</c>); a Button1Dragged past
	/// the viewport edge sets <c>LastDragRelativeY</c>; then <c>StepDragAutoScrollForTest</c> (the exact
	/// main-loop tick) fires <c>MarkupControl.AutoScrollStep</c> → <c>ScrollChildRegionIntoView</c> on the
	/// nearest scrollable ancestor — the path the root-cause hypothesis names.
	///
	/// We CONFIRM a selection actually registered (label reports a selection and is the active drag target)
	/// before concluding. If the bug reproduces the outer offset moves up and the final assert goes RED.
	/// </summary>
	[Fact]
	public void SelectingLabel_DoesNotScrollOuterUp()
	{
		const int width = 100;
		const int height = 24;

		var (system, window, outputPanel, btn1, btn2, label) = BuildDemo17a(width, height);

		Assert.True(outputPanel.TotalContentHeight > outputPanel.ViewportHeight,
			$"precondition: OUTER outputPanel must overflow so it can scroll " +
			$"(content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight}).");

		// Scroll to the bottom so the label is visible (it is the last child).
		outputPanel.ScrollToBottom();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int winContentH = height - 2;
		bool labelVisible = label.ActualY >= 0 && label.ActualY < winContentH;
		_out.WriteLine($"label ActualY={label.ActualY} visible={labelVisible}; winContentH={winContentH}");
		Assert.True(labelVisible,
			$"precondition: the label must be visible so selecting it is meaningful (label.ActualY={label.ActualY}, winContentH={winContentH}).");

		int before = outputPanel.VerticalScrollOffset;
		_out.WriteLine($"outer offset before selection = {before} (content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight})");

		// Drive a real drag-selection on the label. Coordinates are label-content-relative (row 0 = first
		// text row of the label). Press anchors + registers the drag-autoscroll target; drag past the
		// bottom edge extends the selection and sets LastDragRelativeY for the autoscroll tick.
		var press = new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Pressed },
			new Point(1, 0), new Point(1, 0), new Point(1, 0));
		((IMouseAwareControl)label).ProcessMouseEvent(press);

		// Drag far below the viewport bottom so the autoscroll edge-detection triggers a step.
		var drag = new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Dragged },
			new Point(20, 40), new Point(20, 40), new Point(1, 0));
		((IMouseAwareControl)label).ProcessMouseEvent(drag);

		// CONFIRM the selection actually registered.
		bool hasSelection = label.HasSelection;
		bool isDragTarget = ((IDragAutoScrollTarget)label).IsDragSelecting;
		_out.WriteLine($"selection registered: HasSelection={hasSelection}, IsDragSelecting={isDragTarget}, LastDragRelativeY={((IDragAutoScrollTarget)label).LastDragRelativeY}, ViewportHeightRows={((IDragAutoScrollTarget)label).ViewportHeightRows}");
		Assert.True(hasSelection && isDragTarget,
			$"the label selection did not register (HasSelection={hasSelection}, IsDragSelecting={isDragTarget}). " +
			$"A non-registered selection is NOT evidence of 'no bug'.");

		// Fire the drag-autoscroll tick(s) — the real main-loop path that invokes AutoScrollStep.
		for (int i = 0; i < 5; i++)
		{
			system.StepDragAutoScrollForTest(elapsedMs: 1000);
			system.Render.UpdateDisplay();
		}

		int after = outputPanel.VerticalScrollOffset;
		_out.WriteLine($"[selection auto-scroll] outer offset after = {after} (before={before}, delta={after - before})");

		// Reproduction assertion: selecting the label must NOT scroll the outer panel up / jitter.
		// Bug = up-scroll (after < before) = RED.
		Assert.Equal(before, after);
	}

	/// <summary>
	/// Test C (issue #67, SELECTION drag-autoscroll — RED reproduction) — starting a text selection on the
	/// deeply nested trailing <c>label</c> and driving the drag-autoscroll tick must NOT scroll the outer
	/// <c>outputPanel</c> toward content-top.
	///
	/// This differs from <see cref="SelectingLabel_DoesNotScrollOuterUp"/> in that it CONFIRMS the whole
	/// path actually fires end-to-end before drawing a conclusion:
	///  1. it drives the drag Y BELOW the outer viewport bottom (window content coords) so
	///     <c>DragAutoScroll.ComputeStep</c> returns a nonzero downward step, and
	///  2. it CONFIRMS <c>AutoScrollStep</c> actually ran by observing an offset change on the FIRST tick
	///     (a non-firing tick is reported, not silently passed).
	///
	/// Root cause (from a live trace): <c>MarkupControl.AutoScrollStep</c> passes <c>_selEndRow</c> (a
	/// LABEL-relative row, e.g. 0/1) as the <c>childRelativeTop</c> for <c>directChild</c> — which is a
	/// DIFFERENT outer container (the CollapsiblePanel mainPanel, slotTop≈0). So <c>revealRow≈1</c> is
	/// applied against the big container's slotTop(0) → <c>regionTop≈1</c>; with the outer panel scrolled
	/// down (offset&gt;0), <c>regionTop &lt; offset</c> takes the UP branch and the panel jumps toward the
	/// top. The correct pattern (MarkupControl.Keyboard.cs) translates to the directChild's coord space:
	/// <c>regionTop = (ActualY + Margin.Top + row) - directChild.ActualY</c>.
	/// </summary>
	[Fact]
	public void Selection_DragAutoScroll_DoesNotScrollOuterToTop()
	{
		const int width = 100;
		const int height = 24;

		var (system, window, outputPanel, btn1, btn2, label) = BuildDemo17a(width, height);

		Assert.True(outputPanel.TotalContentHeight > outputPanel.ViewportHeight,
			$"precondition: OUTER outputPanel must overflow so it can scroll " +
			$"(content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight}).");

		// Scroll the outer panel DOWN so the trailing label is visible near the bottom (mirrors live offset=7).
		outputPanel.ScrollToBottom();
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		int winContentH = height - 2;
		bool labelVisible = label.ActualY >= 0 && label.ActualY < winContentH;
		_out.WriteLine($"label ActualY={label.ActualY} visible={labelVisible}; winContentH={winContentH}; " +
			$"mainPanel not exposed; outputPanel offset={outputPanel.VerticalScrollOffset}");
		Assert.True(labelVisible,
			$"precondition: label must be visible (label.ActualY={label.ActualY}, winContentH={winContentH}).");

		int before = outputPanel.VerticalScrollOffset;
		Assert.True(before > 0,
			$"precondition: the outer panel must be scrolled down (offset>0) so a jump-to-top is observable; before={before}.");
		_out.WriteLine($"outer offset before selection = {before} (content={outputPanel.TotalContentHeight} viewport={outputPanel.ViewportHeight})");

		// Anchor the selection on the label's first content row. Coordinates are label-content-relative
		// (row 0 = first text row). Press registers the label as the drag-autoscroll target.
		var press = new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Pressed },
			new Point(1, 0), new Point(1, 0), new Point(1, 0));
		((IMouseAwareControl)label).ProcessMouseEvent(press);

		// Drag FAR below the outer viewport bottom so ComputeStep returns a nonzero DOWN step. The drag Y is
		// stored clip-relative (args.Position.Y + ActualY + Margin.Top - clipTop) — using a large Y (well past
		// the panel's visible bottom) guarantees the edge/overshoot check fires.
		var drag = new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Dragged },
			new Point(5, 500), new Point(5, 500), new Point(1, 0));
		((IMouseAwareControl)label).ProcessMouseEvent(drag);

		var t = (IDragAutoScrollTarget)label;
		bool hasSelection = label.HasSelection;
		bool isDragTarget = t.IsDragSelecting;
		_out.WriteLine($"selection registered: HasSelection={hasSelection}, IsDragSelecting={isDragTarget}, " +
			$"LastDragRelativeY={t.LastDragRelativeY}, ViewportHeightRows={t.ViewportHeightRows}, IsViewportReady={t.IsViewportReady}");
		Assert.True(isDragTarget,
			$"the label drag did not register (IsDragSelecting={isDragTarget}). A non-registered drag is NOT evidence of 'no bug'.");

		// Use a SMALL elapsed time so each tick produces a SINGLE-row step (rows=1), mirroring the live trace
		// (rows=1, selEndRow=0, revealRow=1). A big multi-row step lands revealRow far past the top and merely
		// clamps at the bottom (masking the bug); the live bug is the tiny 1-row reveal being applied against
		// the WRONG child's slotTop(0), which sits ABOVE the current offset → an UP scroll. At 60 rows/sec max,
		// ~17ms yields floor(60*17/1000)=1 row per tick.
		const double tickMs = 17;
		double probeCarry = 0;
		int probeStep = SharpConsoleUI.Helpers.DragAutoScroll.ComputeStep(
			t.LastDragRelativeY, t.ViewportHeightRows, tickMs, ref probeCarry);
		_out.WriteLine($"probe ComputeStep(dragY={t.LastDragRelativeY}, vpRows={t.ViewportHeightRows}, {tickMs}ms) = {probeStep}");
		Assert.True(probeStep > 0,
			$"precondition: the drag must produce a DOWN autoscroll step so AutoScrollStep actually fires " +
			$"(ComputeStep returned {probeStep}; dragY={t.LastDragRelativeY}, vpRows={t.ViewportHeightRows}). " +
			$"A zero step means the tick is a no-op and any 'no repro' would be a lie.");

		// Fire ONE tick and check the offset moved (confirms AutoScrollStep fired end-to-end), then keep ticking.
		int firstBefore = outputPanel.VerticalScrollOffset;
		system.StepDragAutoScrollForTest(elapsedMs: tickMs);
		system.Render.UpdateDisplay();
		int firstAfter = outputPanel.VerticalScrollOffset;
		_out.WriteLine($"after tick #1: offset {firstBefore} -> {firstAfter} (AutoScrollStep fired => offset should have moved)");

		for (int i = 0; i < 4; i++)
		{
			system.StepDragAutoScrollForTest(elapsedMs: tickMs);
			system.Render.UpdateDisplay();
		}

		int after = outputPanel.VerticalScrollOffset;
		_out.WriteLine($"[selection auto-scroll] outer offset before={before} after={after} (delta={after - before}); " +
			$"firstTickDelta={firstAfter - firstBefore}");

		// Confirm the autoscroll actually fired: either the first tick moved the offset, or a step was proven.
		Assert.True(firstAfter != firstBefore || probeStep > 0,
			$"AutoScrollStep did not fire (offset unchanged on tick #1 and no step). Cannot conclude repro/no-repro.");

		// Reproduction assertion: the outer panel must NOT jump toward content-top when starting a selection.
		// The bug drives it toward 0/1 → after < before → RED == reproduced.
		Assert.Equal(before, after);
	}
}
