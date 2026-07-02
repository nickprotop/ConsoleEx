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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Real-input-path coverage for <see cref="RadioControl{T}"/>: keyboard selection and mouse
/// selection driven through the REAL dispatch pipeline (driver.SimulateMouseEvent /
/// InputStateService.EnqueueKey → system.Input.ProcessInput → InputCoordinator →
/// WindowEventDispatcher), NOT direct <c>ProcessKey</c>/<c>ProcessMouseEvent</c> calls. Also covers
/// the disabled-state gate: programmatic selection ignores <see cref="RadioControl{T}.IsEnabled"/>
/// (the group is the source of truth and <see cref="RadioControl{T}.Checked"/> is computed
/// independently), while real user input to a disabled radio is refused.
/// </summary>
public class RadioInputTests
{
	private enum Size { Small, Large }

	/// <summary>
	/// Scans a painted buffer for a literal glyph. Used to confirm a disabled-but-selected radio still
	/// paints its selected marker without throwing.
	/// </summary>
	private static bool BufferContains(CharacterBuffer buffer, int width, int height, string glyph)
	{
		var rune = System.Text.Rune.GetRuneAt(glyph, 0);
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
				if (buffer.GetCell(x, y).Character == rune)
					return true;
		return false;
	}

	/// <summary>
	/// Real keyboard dispatch: focus a radio, press Space through the real input pipeline, and the
	/// focused radio becomes the group's selection while its peer is cleared. The selection must
	/// survive a subsequent measure/arrange re-layout (Checked is computed from the group, so a
	/// re-render must not drop it).
	/// </summary>
	[Fact]
	public void SpaceKey_SelectsFocusedRadio_RealDispatch()
	{
		Console.SetIn(TextReader.Null);
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
		var g = new RadioGroup<Size>();
		var a = new RadioControl<Size>(g, Size.Small, "Small");
		var b = new RadioControl<Size>(g, Size.Large, "Large");
		window.AddControl(a);
		window.AddControl(b);
		system.AddWindow(window);
		for (int i = 0; i < 3; i++) system.Render.UpdateDisplay();

		// Nothing selected yet.
		Assert.False(a.Checked);
		Assert.False(b.Checked);

		window.FocusManager.SetFocus(b, FocusReason.Programmatic);
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false));
		system.Input.ProcessInput();

		Assert.True(b.Checked);
		Assert.False(a.Checked);

		// Survives re-render (Checked is group-derived, not stored per-control).
		for (int i = 0; i < 2; i++) system.Render.UpdateDisplay();
		Assert.True(b.Checked);
		Assert.False(a.Checked);
		Assert.Equal(Size.Large, g.SelectedValue);
	}

	/// <summary>
	/// Real mouse dispatch: render two stacked radios, then click on radio B's row through the real
	/// window mouse pipeline (driver.SimulateMouseEvent + system.Input.ProcessInput). B becomes the
	/// selection and A is cleared. Click coordinates are derived from B's arranged ActualX/ActualY
	/// after a first render.
	/// </summary>
	[Fact]
	public void Click_SelectsRadio_RealDispatch()
	{
		Console.SetIn(TextReader.Null);
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
		var g = new RadioGroup<Size>();
		var a = new RadioControl<Size>(g, Size.Small, "Small");
		var b = new RadioControl<Size>(g, Size.Large, "Large");
		window.AddControl(a);
		window.AddControl(b);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		Assert.False(a.Checked);
		Assert.False(b.Checked);

		// Clear focus so the click is the only thing that could select B.
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Translate B's window-content coordinates to absolute screen coords via the window border.
		// Aim a couple columns in so we land on the marker, not the leading padding column.
		int clickX = window.Left + 1 + b.ActualX + 2;
		int clickY = window.Top + 1 + b.ActualY;

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
		system.Input.ProcessInput();

		Assert.True(b.Checked);
		Assert.False(a.Checked);
		Assert.Equal(Size.Large, g.SelectedValue);
	}

	/// <summary>
	/// Real mouse dispatch on a WRAPPED radio: a radio with <c>Wrap = true</c> and a long label that
	/// wraps to two rows is clicked on its SECOND (continuation) row through the real window mouse
	/// pipeline. The whole control is interactive — a click on any of its rows (not just the marker
	/// row) selects it. Before the multi-row hit-test fix, only row 0 was clickable and this failed.
	/// </summary>
	[Fact]
	public void WrappedRadio_ClickOnSecondRow_Selects_RealDispatch()
	{
		Console.SetIn(TextReader.Null);
		// Narrow window forces the long label to wrap onto multiple rows.
		var system = TestWindowSystemBuilder.CreateTestSystem(20, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 20, Height = 12 };
		var g = new RadioGroup<Size>();
		var a = new RadioControl<Size>(g, Size.Small, "Small");
		var b = new RadioControl<Size>(g, Size.Large, "Use the system default color option here")
		{
			Wrap = true
		};
		window.AddControl(a);
		window.AddControl(b);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// The wrapped radio must span at least two rows for this test to be meaningful.
		Assert.True(b.ActualHeight >= 2,
			$"Test setup: wrapped radio should span >= 2 rows but spans {b.ActualHeight}.");

		Assert.False(a.Checked);
		Assert.False(b.Checked);

		// Clear focus so the click is the only thing that could select B.
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Click on B's SECOND row (a continuation line), a couple columns in so we land on the
		// wrapped label text, not the leading padding column.
		int clickX = window.Left + 1 + b.ActualX + 2;
		int clickY = window.Top + 1 + b.ActualY + 1; // +1 → second row of the radio

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
		system.Input.ProcessInput();

		Assert.True(b.Checked, "Clicking the continuation row of a wrapped radio must select it.");
		Assert.False(a.Checked);
		Assert.Equal(Size.Large, g.SelectedValue);
	}

	/// <summary>
	/// Disabled-but-selected: a disabled radio whose value equals the group's selection still reports
	/// <see cref="RadioControl{T}.Checked"/> == true (Checked is computed from the group, independent
	/// of IsEnabled) and paints its selected marker without throwing.
	/// </summary>
	[Fact]
	public void DisabledRadio_ThatIsSelected_ShowsCheckedAndPaints()
	{
		var g = new RadioGroup<Size>();
		var a = new RadioControl<Size>(g, Size.Small, "Small");
		var b = new RadioControl<Size>(g, Size.Large, "Large") { IsEnabled = false };

		// Select the disabled radio's value programmatically (group is the source of truth).
		g.SelectedValue = Size.Large;

		Assert.True(b.Checked);
		Assert.False(a.Checked);

		// Paint the disabled-but-selected radio into a standalone buffer: must not throw and must
		// render its selected marker glyph.
		const int w = 30, h = 3;
		var buffer = new CharacterBuffer(w, h, Color.Black);
		b.MeasureDOM(new LayoutConstraints(0, w, 0, h));
		var bounds = new LayoutRect(0, 0, w, 1);
		var ex = Record.Exception(() => b.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black));
		Assert.Null(ex);
		Assert.True(BufferContains(buffer, w, h, "●"),
			"A disabled-but-selected radio must still paint its selected marker.");
	}

	/// <summary>
	/// Input gate: a real click on a DISABLED radio is refused — the group selection is unchanged.
	/// (If input were not gated by IsEnabled, the click would flip the selection to the disabled
	/// radio's value; this asserts it does not.)
	/// </summary>
	[Fact]
	public void DisabledRadio_RealClick_DoesNotSelect()
	{
		Console.SetIn(TextReader.Null);
		var system = TestWindowSystemBuilder.CreateTestSystem(40, 12);
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
		var g = new RadioGroup<Size>();
		var a = new RadioControl<Size>(g, Size.Small, "Small");
		var b = new RadioControl<Size>(g, Size.Large, "Large") { IsEnabled = false };
		window.AddControl(a);
		window.AddControl(b);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Start with A selected so we can prove the disabled click doesn't steal the selection.
		g.SelectedValue = Size.Small;
		Assert.True(a.Checked);
		Assert.False(b.Checked);

		int clickX = window.Left + 1 + b.ActualX + 2;
		int clickY = window.Top + 1 + b.ActualY;

		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
		system.Input.ProcessInput();

		// Selection unchanged: the disabled radio refused the click.
		Assert.Equal(Size.Small, g.SelectedValue);
		Assert.True(a.Checked);
		Assert.False(b.Checked);
	}

	/// <summary>
	/// Programmatic selection ignores the input gate: setting the group's <c>SelectedValue</c> to a
	/// DISABLED radio's value selects it (code can select a disabled option; only user input is gated).
	/// </summary>
	[Fact]
	public void DisabledRadio_ProgrammaticSelect_DoesSelect()
	{
		var g = new RadioGroup<Size>();
		var a = new RadioControl<Size>(g, Size.Small, "Small");
		var b = new RadioControl<Size>(g, Size.Large, "Large") { IsEnabled = false };

		g.SelectedValue = b.Value;

		Assert.True(b.Checked);
		Assert.False(a.Checked);
		Assert.Equal(Size.Large, g.SelectedValue);
	}
}
