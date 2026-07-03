// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Builders = SharpConsoleUI.Builders;

namespace SharpConsoleUI.Tests;

/// <summary>
/// The hard end-to-end "real thing" test for the radio control: two <see cref="RadioGroup{T}"/>
/// instances whose members are distributed across DIFFERENT grid columns, hosted through the REAL
/// example topology — <c>CollapsiblePanel → GridControl → ScrollablePanel cells → RadioControl</c> —
/// at a boundary-stressing narrow width. It proves the group-object design end-to-end: selecting a
/// radio in one grid column unchecks a same-group peer in a DIFFERENT column (the group, not layout
/// adjacency, is the source of truth); two independent groups coexist; the selection survives
/// re-render and a collapse/expand cycle; real mouse and keyboard dispatch select correctly; and the
/// painted marker geometry (marker at content-column 2 within each radio's own bounds) holds inside
/// the real nested layout. All coordinates are derived from a first render, not hardcoded.
/// </summary>
public class RadioInGridInPanelRealThingTest
{
	private enum Opt { A1, A2, A3, A4, A5 }

	private enum Grp { B1, B2, B3 }

	/// <summary>
	/// Mouse-input X of a radio's marker cell: the window-mouse pipeline maps content-relative ActualX
	/// through the window border (window.Left + 1), landing on the marker at content-col 0.
	/// </summary>
	private static int ClickX(Window window, int actualX) => window.Left + 1 + actualX + 0;

	/// <summary>Mouse-input Y of a radio's single content row (through the window border).</summary>
	private static int ClickY(Window window, int actualY) => window.Top + 1 + actualY;

	[Fact]
	public void Radios_InGrid_InCollapsiblePanel_CrossColumnGrouping_And_Geometry()
	{
		Console.SetIn(TextReader.Null);
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 20);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 20 };

		var groupA = new RadioGroup<Opt>();
		var groupB = new RadioGroup<Grp>();

		// col-0 panel: A1,A2,A3 ; col-1 panel: A4,A5 — SAME group A across two grid columns.
		var a1 = new RadioControl<Opt>(groupA, Opt.A1, "A1");
		var a2 = new RadioControl<Opt>(groupA, Opt.A2, "A2");
		var a3 = new RadioControl<Opt>(groupA, Opt.A3, "A3");
		var colA0 = Builders.Controls.ScrollablePanel()
			.AddControl(a1).AddControl(a2).AddControl(a3)
			.Build();

		var a4 = new RadioControl<Opt>(groupA, Opt.A4, "A4");
		var a5 = new RadioControl<Opt>(groupA, Opt.A5, "A5");
		var colA1 = Builders.Controls.ScrollablePanel()
			.AddControl(a4).AddControl(a5)
			.Build();

		// row-1 panel (spans both columns): B1,B2,B3 — a second, independent group B.
		var b1 = new RadioControl<Grp>(groupB, Grp.B1, "B1");
		var b2 = new RadioControl<Grp>(groupB, Grp.B2, "B2");
		var b3 = new RadioControl<Grp>(groupB, Grp.B3, "B3");
		var bPanel = Builders.Controls.ScrollablePanel()
			.AddControl(b1).AddControl(b2).AddControl(b3)
			.Build();

		var grid = Builders.Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Auto())
			.Place(colA0, 0, 0)
			.Place(colA1, 0, 1)
			.Place(bPanel, 1, 0, colSpan: 2)
			.Build();

		var panel = new CollapsiblePanel { Title = "Options" };
		panel.AddControl(grid);
		panel.Expand();
		window.AddControl(panel);
		system.AddWindow(window);
		for (int i = 0; i < 4; i++) system.Render.UpdateDisplay();

		// ── THE KILLER ASSERTION: cross-column grouping ─────────────────────────────────────────────
		// Select A5 (col 1), then A1 (col 0). A1 becomes the selection and A5 — a radio in a DIFFERENT
		// grid column — is unchecked. This can only hold if the GROUP object (not layout adjacency) owns
		// the single-selection invariant.
		a5.Select();
		Assert.True(a5.Checked);
		a1.Select();
		Assert.True(a1.Checked);
		Assert.False(a5.Checked);            // cross-column peer cleared — group is the source of truth
		Assert.False(groupB.HasSelection);   // the other group is untouched

		// ── Two independent groups coexist ──────────────────────────────────────────────────────────
		a2.Select();
		b3.Select();
		Assert.Equal(Opt.A2, groupA.SelectedValue);
		Assert.Equal(Grp.B3, groupB.SelectedValue);
		Assert.True(a2.Checked);
		Assert.False(a1.Checked);
		Assert.True(b3.Checked);
		Assert.False(b1.Checked);
		Assert.False(b2.Checked);

		// ── Selection survives a re-render (Checked is group-derived, not stored per-control) ───────
		for (int i = 0; i < 2; i++) system.Render.UpdateDisplay();
		Assert.True(a2.Checked);
		Assert.True(b3.Checked);
		Assert.Equal(Opt.A2, groupA.SelectedValue);
		Assert.Equal(Grp.B3, groupB.SelectedValue);

		// ── Collapse hides the body, Expand restores it — selection state is preserved throughout ──
		panel.Collapse();
		Assert.False(panel.IsExpanded);
		for (int i = 0; i < 2; i++) system.Render.UpdateDisplay();
		Assert.True(a2.Checked);   // group state independent of visibility
		Assert.True(b3.Checked);

		panel.Expand();
		Assert.True(panel.IsExpanded);
		for (int i = 0; i < 2; i++) system.Render.UpdateDisplay();
		Assert.True(a2.Checked);
		Assert.True(b3.Checked);
		Assert.Equal(Opt.A2, groupA.SelectedValue);
		Assert.Equal(Grp.B3, groupB.SelectedValue);

		// ── Geometry inside the REAL nested layout ─────────────────────────────────────────────────
		// After a render, each radio's painted marker glyph must sit at content-column 0 within its own
		// arranged bounds (the "mark " prefix: marker col 0, trailing space col 1). In the composited
		// buffer a control's ActualX/ActualY ARE its content-origin coordinates, so we assert the marker
		// glyph directly at ActualX — proving the geometry contract holds through grid + panel nesting.
		// Force a dirty frame so the diagnostics layer captures a snapshot for this frame number
		// (a clean no-op frame produces no snapshot).
		panel.Invalidate(Invalidation.Relayout);
		system.Render.UpdateDisplay();
		var snap = system.RenderingDiagnostics?.LastBufferSnapshot;
		Assert.NotNull(snap);

		// a2 is selected → '●' ; a1 is not → '○'. Read both at their derived marker cells.
		AssertMarkerGlyph(snap!, a2, '●');
		AssertMarkerGlyph(snap!, a1, '○');
		AssertMarkerGlyph(snap!, a4, '○');   // col-1 group-A radio, unselected
		AssertMarkerGlyph(snap!, b3, '●');   // group-B selected
		AssertMarkerGlyph(snap!, b1, '○');

		// ── Real MOUSE dispatch: click B2's marker cell → B2 selected, B1/B3 not; group A untouched ─
		// Clear focus so the click is the only thing that could select.
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		int b2ClickX = ClickX(window, b2.ActualX);
		int b2ClickY = ClickY(window, b2.ActualY);
		Assert.True(b2.ActualX >= 0 && b2.ActualY >= 0, "B2 must have arranged bounds after render.");

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(b2ClickX, b2ClickY));
		system.Input.ProcessInput();

		Assert.True(b2.Checked);
		Assert.False(b1.Checked);
		Assert.False(b3.Checked);
		Assert.Equal(Grp.B2, groupB.SelectedValue);
		// Group A is a different object — a click in group B must not disturb it.
		Assert.Equal(Opt.A2, groupA.SelectedValue);
		Assert.True(a2.Checked);

		// A click on an A-group radio does not change group B.
		for (int i = 0; i < 2; i++) system.Render.UpdateDisplay();
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		int a3ClickX = ClickX(window, a3.ActualX);
		int a3ClickY = ClickY(window, a3.ActualY);
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(a3ClickX, a3ClickY));
		system.Input.ProcessInput();

		Assert.True(a3.Checked);
		Assert.False(a2.Checked);
		Assert.Equal(Opt.A3, groupA.SelectedValue);
		Assert.Equal(Grp.B2, groupB.SelectedValue);   // group B unchanged by the group-A click

		// ── Real KEYBOARD dispatch: focus a col-1 group-A radio, press Space → selects it, unchecking
		//    its cross-column peer (a5 in col 1 selected, a3 in col 0 cleared). ───────────────────────
		window.FocusManager.SetFocus(a5, FocusReason.Programmatic);
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false));
		system.Input.ProcessInput();

		Assert.True(a5.Checked);
		Assert.False(a3.Checked);   // cross-column peer cleared by keyboard-driven selection
		Assert.Equal(Opt.A5, groupA.SelectedValue);
		Assert.Equal(Grp.B2, groupB.SelectedValue);   // group B still untouched
	}

	/// <summary>Reads one composited-screen character, or '\0' if out of bounds.</summary>
	private static char CharAt(CharacterBufferSnapshot snap, int x, int y)
	{
		if (x < 0 || y < 0 || x >= snap.Width || y >= snap.Height) return '\0';
		return snap.GetCell(x, y).Character.ToString() is { Length: > 0 } s ? s[0] : '\0';
	}

	/// <summary>
	/// Verifies a radio's painted marker geometry inside the real nested layout: on the radio's arranged
	/// content row, the marker glyph sits at content-column 0 (i.e. at <c>ActualX</c>) and is followed
	/// by a space at content-column 1. The new prefix is <c>"mark "</c> — glyph + trailing space, no
	/// surrounding parentheses. Anchoring on the radio's <c>ActualX</c> in the composited buffer and
	/// asserting the glyph there proves the marker-at-col-0 contract through the real nested layout.
	/// </summary>
	private static void AssertMarkerGlyph<T>(CharacterBufferSnapshot snap, RadioControl<T> radio, char expected)
	{
		// In the composited window/desktop buffer, a control's arranged ActualX/ActualY ARE the buffer
		// coordinates of its content origin (this window is at 0,0). The "mark " prefix lays out
		// the marker glyph at ActualX (content-col 0) and a trailing space at ActualX+1.
		int y = radio.ActualY;
		Assert.True(y >= 0 && y < snap.Height,
			$"Radio '{radio.Label}' content row (buffer y={y}) is outside the {snap.Width}x{snap.Height} buffer.");

		int markerX = radio.ActualX;
		char marker = CharAt(snap, markerX, y);
		char space = CharAt(snap, markerX + 1, y);
		Assert.True(marker == expected,
			$"Radio '{radio.Label}' expected marker '{expected}' at content-col 0 (screen {markerX},{y}) " +
			$"but found '{marker}' (row='{Row(snap, y)}').");
		Assert.True(space == ' ',
			$"Radio '{radio.Label}' expected trailing space at screen {markerX + 1},{y} but found '{space}' " +
			$"(row='{Row(snap, y)}').");
	}

	/// <summary>Renders a composited-screen row as a string for diagnostics.</summary>
	private static string Row(CharacterBufferSnapshot snap, int y)
	{
		if (y < 0 || y >= snap.Height) return string.Empty;
		var sb = new System.Text.StringBuilder(snap.Width);
		for (int x = 0; x < snap.Width; x++) sb.Append(CharAt(snap, x, y));
		return sb.ToString();
	}
}
