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
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression coverage for the reported bug (ServerHub / PanelDemo): after PanelControl became a thin
/// facade over an inner CollapsiblePanel, a click on a widget's BODY CONTENT (its passive
/// MarkupControl) no longer fired the panel's MouseClick — only clicks on empty body space did.
/// Root cause: the passive MarkupControl was the deepest DOM hit-target (and the mouse-capture target
/// for the press→release gesture), so the click never surfaced to the hosting panel. A symmetric bug
/// dropped DOUBLE-clicks: MarkupControl consumed Button1DoubleClicked unconditionally even with no
/// subscriber.
///
/// These tests drive the REAL window mouse-dispatch pipeline (driver.SimulateMouseEvent +
/// system.Input.ProcessInput → InputCoordinator → WindowEventDispatcher.ProcessMouseEvent), mirroring
/// the real ServerHub nesting (HorizontalGrid → Column → PanelControl-facade → MarkupControl body).
/// A direct panel.ProcessMouseEvent call would bypass the capture/hit-test/bubble path where the bug
/// lives, so it would NOT reproduce the failure.
/// </summary>
public class PanelFacadeBodyClickBubbleTests
{
	/// <summary>
	/// Builds the real ServerHub widget nesting: a HorizontalGrid with one column hosting a
	/// PanelControl (the facade) whose body is passive markup content ("3 actions" — colored text, NO
	/// links, NO subscriber on the inner markup). Returns the panel and the live click counters.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, PanelControl panel,
		int[] clickCount, int[] doubleClickCount) BuildWidgetGrid()
	{
		const int width = 60;
		const int height = 16;

		var panel = ControlsFactory.Panel()
			.WithContent("CPU 42%\nMem 1.2G\n[cyan1]3 actions[/]")
			.WithHeader("Server-01")
			.Rounded()
			.WordWrap(false)
			.Build();

		// HorizontalGrid → Column → PanelControl, exactly like WidgetRenderer places widgets.
		var grid = new HorizontalGridControl();
		var col = new ColumnContainer(grid);
		col.AddContent(panel);
		grid.AddColumn(col);

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// ServerHub subscribes to BOTH events on the panel (WidgetRenderer.cs).
		var clickCount = new[] { 0 };
		var doubleClickCount = new[] { 0 };
		panel.MouseClick += (_, _) => clickCount[0]++;
		panel.MouseDoubleClick += (_, _) => doubleClickCount[0]++;

		return (system, window, panel, clickCount, doubleClickCount);
	}

	/// <summary>Absolute screen X/Y a couple cells into the panel body (past the header/border).</summary>
	private static (int x, int y) BodyContentPoint(Window window, PanelControl panel)
	{
		// panel.ActualX/Y are window-content coords; +1 for the window border. +2 down clears the
		// rounded top border + header row so we land on the markup content line.
		int x = window.Left + 1 + panel.ActualX + 3;
		int y = window.Top + 1 + panel.ActualY + 2;
		return (x, y);
	}

	private static void Dispatch(ConsoleWindowSystem system, List<MouseFlags> flags, int x, int y)
	{
		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(flags, new Point(x, y));
		system.Input.ProcessInput();
	}

	/// <summary>
	/// Real-thing test: a left click (press → release+click, the captured-gesture path) on the panel's
	/// passive markup BODY content must raise the panel's MouseClick exactly once. This is the exact
	/// path that was silently dropped before the fix.
	/// </summary>
	[Fact]
	public void RealDispatch_LeftClickOnBodyMarkup_RaisesPanelMouseClickOnce()
	{
		var (system, window, panel, clickCount, doubleClickCount) = BuildWidgetGrid();
		var (x, y) = BodyContentPoint(window, panel);

		// Press then release+click — the real terminal gesture that sets and then releases mouse
		// capture on the markup, routing the whole gesture through the capture path.
		Dispatch(system, new List<MouseFlags> { MouseFlags.Button1Pressed }, x, y);
		Dispatch(system, new List<MouseFlags> { MouseFlags.Button1Released, MouseFlags.Button1Clicked }, x, y);

		// Re-render and assert the observable end state survives a frame.
		system.Render.UpdateDisplay();

		Assert.Equal(1, clickCount[0]);
		Assert.Equal(0, doubleClickCount[0]);
	}

	/// <summary>
	/// Real-thing test: a double-click on passive markup body content must raise the panel's
	/// MouseDoubleClick. The driver emits Button1DoubleClicked as a standalone event; the passive
	/// markup (no MouseDoubleClick subscriber) must NOT swallow it but let it bubble to the panel.
	/// </summary>
	[Fact]
	public void RealDispatch_DoubleClickOnBodyMarkup_RaisesPanelMouseDoubleClick()
	{
		var (system, window, panel, clickCount, doubleClickCount) = BuildWidgetGrid();
		var (x, y) = BodyContentPoint(window, panel);

		// The real gesture: first click lands, then a standalone Button1DoubleClicked arrives.
		Dispatch(system, new List<MouseFlags> { MouseFlags.Button1Pressed }, x, y);
		Dispatch(system, new List<MouseFlags> { MouseFlags.Button1Released, MouseFlags.Button1Clicked }, x, y);
		Dispatch(system, new List<MouseFlags> { MouseFlags.Button1DoubleClicked }, x, y);

		system.Render.UpdateDisplay();

		Assert.True(doubleClickCount[0] >= 1,
			$"Double-clicking passive panel body markup must raise the panel's MouseDoubleClick " +
			$"(got {doubleClickCount[0]}). Before the fix MarkupControl swallowed Button1DoubleClicked.");
	}

	/// <summary>
	/// Guard against the regression introduced mid-fix: a single click must NOT also register as a
	/// double-click (which happened when the bubble double-dispatched the click through the panel and
	/// an outer container both handled it).
	/// </summary>
	[Fact]
	public void RealDispatch_SingleLeftClick_DoesNotDoubleFire()
	{
		var (system, window, panel, clickCount, doubleClickCount) = BuildWidgetGrid();
		var (x, y) = BodyContentPoint(window, panel);

		Dispatch(system, new List<MouseFlags> { MouseFlags.Button1Pressed }, x, y);
		Dispatch(system, new List<MouseFlags> { MouseFlags.Button1Released, MouseFlags.Button1Clicked }, x, y);

		system.Render.UpdateDisplay();

		// Exactly one click, zero double-clicks — the click was handled once, not by two controls.
		Assert.Equal(1, clickCount[0]);
		Assert.Equal(0, doubleClickCount[0]);
	}

	/// <summary>
	/// Builds the widget grid and also wires the hover events (enter/leave/move), returning their
	/// counters. Mirrors PanelDemo, which registers all six mouse events on each panel.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, PanelControl panel,
		int[] enter, int[] leave, int[] move) BuildWidgetGridWithHover()
	{
		var (system, window, panel, _, _) = BuildWidgetGrid();
		var enter = new[] { 0 };
		var leave = new[] { 0 };
		var move = new[] { 0 };
		panel.MouseEnter += (_, _) => enter[0]++;
		panel.MouseLeave += (_, _) => leave[0]++;
		panel.MouseMove += (_, _) => move[0]++;
		return (system, window, panel, enter, leave, move);
	}

	/// <summary>
	/// Real-thing test: moving the cursor onto the panel's passive body markup must raise the panel's
	/// MouseEnter (and MouseMove), and moving it off the panel entirely must raise MouseLeave. Before
	/// the fix these only reached the deepest markup leaf and never surfaced to the hosting panel.
	/// </summary>
	[Fact]
	public void RealDispatch_MoveOntoAndOffBodyMarkup_RaisesEnterMoveLeave()
	{
		var (system, window, panel, enter, leave, move) = BuildWidgetGridWithHover();
		var (bx, by) = BodyContentPoint(window, panel);

		// Start clearly OUTSIDE the panel (bottom-right empty window area), then move onto the body.
		Dispatch(system, new List<MouseFlags> { MouseFlags.ReportMousePosition }, window.Left + window.Width - 2, window.Top + window.Height - 2);
		Dispatch(system, new List<MouseFlags> { MouseFlags.ReportMousePosition }, bx, by);

		Assert.True(enter[0] >= 1, $"Moving onto body markup must raise panel MouseEnter (got {enter[0]}).");
		Assert.True(move[0] >= 1, $"Moving over body markup must raise panel MouseMove (got {move[0]}).");

		int enterBefore = enter[0];

		// Move off the panel entirely → MouseLeave, and no spurious second enter.
		Dispatch(system, new List<MouseFlags> { MouseFlags.ReportMousePosition }, window.Left + window.Width - 2, window.Top + window.Height - 2);

		Assert.True(leave[0] >= 1, $"Moving off the panel must raise panel MouseLeave (got {leave[0]}).");
		Assert.Equal(enterBefore, enter[0]); // leaving must not re-fire enter
	}

	/// <summary>
	/// Moving WITHIN the panel body (between two content cells) must NOT spuriously re-fire MouseEnter
	/// each move — enter fires once when the cursor first enters the panel's subtree. Guards the
	/// ancestor-chain hover diff (a naive deepest-control diff would re-enter on every internal move).
	/// </summary>
	[Fact]
	public void RealDispatch_MoveWithinBody_DoesNotReFireEnter()
	{
		var (system, window, panel, enter, leave, move) = BuildWidgetGridWithHover();
		var (bx, by) = BodyContentPoint(window, panel);

		Dispatch(system, new List<MouseFlags> { MouseFlags.ReportMousePosition }, window.Left + window.Width - 2, window.Top + window.Height - 2);
		Dispatch(system, new List<MouseFlags> { MouseFlags.ReportMousePosition }, bx, by);

		int enterAfterFirst = enter[0];
		Assert.True(enterAfterFirst >= 1);

		// Several moves staying inside the body.
		Dispatch(system, new List<MouseFlags> { MouseFlags.ReportMousePosition }, bx + 1, by);
		Dispatch(system, new List<MouseFlags> { MouseFlags.ReportMousePosition }, bx + 2, by);

		Assert.Equal(enterAfterFirst, enter[0]); // no extra enters while staying inside
		Assert.Equal(0, leave[0]);               // and no leave while still inside
	}
}
