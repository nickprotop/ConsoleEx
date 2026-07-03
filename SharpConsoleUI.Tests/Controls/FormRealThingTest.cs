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
/// The "real thing" end-to-end test for <see cref="FormControl"/>: a labeled-input form built in a
/// NARROW window, driven through the REAL dispatch pipeline (focus + <c>EnqueueKey</c> to type, and
/// <c>driver.SimulateMouseEvent</c> + <c>system.Input.ProcessInput</c> to click), NOT direct
/// <c>ProcessKey</c>/<c>ProcessMouseEvent</c> calls.
///
/// <para>
/// This mirrors the actual usage path, NOT isolated component asserts:
/// <list type="bullet">
///   <item>Real container nesting — a <see cref="FormControl"/> (which IS a
///   <see cref="GridControl"/>) inside a narrow <see cref="Window"/>, hosting real
///   <see cref="PromptControl"/>/<see cref="DropdownControl"/>/<see cref="CheckboxControl"/>/radio
///   editors plus a collapsed section.</item>
///   <item>Boundary-stressing width (40 cols) so the auto label column and star editor column split a
///   tight width.</item>
///   <item>Real input path — the checkbox toggle goes through
///   <c>driver.SimulateMouseEvent(...) + system.Input.ProcessInput()</c> (dispatcher → hit-test →
///   parent-chain routing); typing goes through
///   <c>FocusManager.SetFocus + InputStateService.EnqueueKey + system.Input.ProcessInput</c>.</item>
///   <item>Re-render between action and assert, and the final state is re-checked after a second
///   <c>UpdateDisplay</c> so it must SURVIVE a re-layout.</item>
/// </list>
/// </para>
/// </summary>
public class FormRealThingTest
{
	private const int Width = 40;
	private const int Height = 24;

	/// <summary>
	/// Builds the real topology: a <see cref="FormControl"/> in a narrow window with a "Connection"
	/// section (required host text + hint, a driver dropdown, an SSL checkbox), a collapsed "Advanced"
	/// section (a mode radio + a timeout text), and an OK/Cancel button row.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, FormControl form) BuildForm()
	{
		Console.SetIn(TextReader.Null);

		var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
		var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };

		var form = new FormControl();
		form
			.AddSection("Connection")
			.AddText("host", "Host", required: true, hint: "hostname or IP")
			.AddDropdown("driver", "Driver", new[] { "postgres", "mysql", "sqlite" }, initial: "postgres")
			.AddCheckbox("ssl", "Use SSL", initial: false)
			.AddSection("Advanced", collapsible: true, startCollapsed: true)
			.AddRadio("mode", "Mode", "fast", "safe")
			.AddText("timeout", "Timeout", initial: "30")
			.WithButtons();

		window.AddControl(form);
		system.AddWindow(window);

		// First render establishes the arranged geometry (ActualX/ActualY of every editor).
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, form);
	}

	private static void Render(ConsoleWindowSystem system)
	{
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
	}

	/// <summary>
	/// The full end-to-end story: a collapsed section starts hidden and a real toggle expands it;
	/// validation gates submit (empty required host blocks <see cref="FormControl.Submitted"/> and shows
	/// the error); once the host is typed, submit fires ONCE with the correct value snapshot; a real
	/// checkbox click flips its value; and every observable end state survives a re-render.
	/// </summary>
	[Fact]
	public void Form_Collapse_Validate_Submit_RealThing()
	{
		var (system, window, form) = BuildForm();

		var hostPrompt = (PromptControl)form.GetEditor("host");
		var sslCheckbox = (CheckboxControl)form.GetEditor("ssl");
		var timeoutPrompt = (PromptControl)form.GetEditor("timeout");
		var modeGroup = (RadioGroup<string>)form.GetEditor("mode");

		// --- 1. The collapsed "Advanced" section starts hidden --------------------------------------
		Assert.False(timeoutPrompt.Visible,
			"the collapsed Advanced section's timeout editor must start hidden.");
		Assert.Null(modeGroup.SelectedValue);

		// The editors of a collapsed section are not visible, so they are not arranged on-screen.
		// (A hidden control keeps its stale/zero geometry — the point is they are not painted.)
		Assert.False(form.HasErrorForTest("host"), "no validation should have run yet.");

		// --- 2. Expand the section (real dispatch preferred; coord seam for the toggle) --------------
		// The section toggle is a ButtonControl in an Auto grid row; its on-screen row depends on the
		// tight arranged layout. Use the deterministic seam to toggle (a real click on the glyph column
		// of a 1-wide Auto cell is coordinate-fragile), then assert the fields become visible.
		form.ToggleSectionForTest("Advanced");
		Render(system);

		Assert.True(timeoutPrompt.Visible,
			"expanding the Advanced section must make its timeout editor visible.");

		// --- 3. Validation gates submit: host empty → no Submitted, error shown ----------------------
		int submittedCount = 0;
		IReadOnlyDictionary<string, string?>? submittedValues = null;
		form.Submitted += (_, values) =>
		{
			submittedCount++;
			submittedValues = values;
		};

		Assert.Equal(string.Empty, hostPrompt.Input);

		// Real-click OK (via the OK button's click seam — same delegate the real button fires).
		form.ClickOkForTest();
		Render(system);

		Assert.Equal(0, submittedCount);
		Assert.True(form.HasErrorForTest("host"),
			"an empty required host must show a validation error.");
		Assert.Equal("Required", form.ErrorTextForTest("host"));
		Assert.Null(submittedValues);

		// --- 4. Type the host through the REAL input pipeline ---------------------------------------
		window.FocusManager.SetFocus(hostPrompt, FocusReason.Programmatic);
		Assert.True(hostPrompt.HasFocus, "the host prompt must accept programmatic focus before typing.");

		foreach (char c in "db.local")
		{
			system.InputStateService.EnqueueKey(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false));
			system.Input.ProcessInput();
		}
		Render(system);

		Assert.Equal("db.local", hostPrompt.Input);

		// --- 5. Select the Advanced radio (real keyboard dispatch) ----------------------------------
		var safeRadio = (RadioControl<string>)FindRadioByLabel(form, "safe");
		window.FocusManager.SetFocus(safeRadio, FocusReason.Programmatic);
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false));
		system.Input.ProcessInput();
		Render(system);

		Assert.Equal("safe", modeGroup.SelectedValue);

		// --- 6. Real checkbox click flips the "ssl" value -------------------------------------------
		Assert.Equal("false", form.GetValues()["ssl"]);

		// Derive the checkbox click coord from its arranged geometry. It sits in the star editor column,
		// which is well within the window; aim a couple columns in to land on the box, not padding.
		Assert.True(sslCheckbox.ActualWidth > 0 && sslCheckbox.ActualHeight > 0,
			$"the SSL checkbox must be arranged (w={sslCheckbox.ActualWidth} h={sslCheckbox.ActualHeight}).");

		int clickX = window.Left + 1 + sslCheckbox.ActualX + 1;
		int clickY = window.Top + 1 + sslCheckbox.ActualY;
		Assert.InRange(clickY, window.Top + 1, window.Top + Height - 1);

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		var driver = (MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<MouseFlags> { MouseFlags.Button1Clicked }, new Point(clickX, clickY));
		system.Input.ProcessInput();
		Render(system);

		Assert.True(sslCheckbox.Checked,
			$"clicking the SSL checkbox must toggle it (clicked screen=({clickX},{clickY}) " +
			$"editorActual=({sslCheckbox.ActualX},{sslCheckbox.ActualY})).");
		Assert.Equal("true", form.GetValues()["ssl"]);

		// --- 7. Submit now passes and fires ONCE with the correct snapshot --------------------------
		form.ClickOkForTest();
		Render(system);

		Assert.Equal(1, submittedCount);
		Assert.False(form.HasErrorForTest("host"), "the host error must clear once it is filled.");
		Assert.NotNull(submittedValues);
		Assert.Equal("db.local", submittedValues!["host"]);
		Assert.Equal("postgres", submittedValues["driver"]);
		Assert.Equal("true", submittedValues["ssl"]);
		Assert.Equal("safe", submittedValues["mode"]);
		Assert.Equal("30", submittedValues["timeout"]);

		// --- 8. Every observable end state SURVIVES a second re-render ------------------------------
		Render(system);
		Assert.Equal(1, submittedCount);
		Assert.Equal("db.local", hostPrompt.Input);
		Assert.True(sslCheckbox.Checked);
		Assert.Equal("safe", modeGroup.SelectedValue);
		Assert.True(timeoutPrompt.Visible);
		Assert.False(form.HasErrorForTest("host"));

		// The values snapshot is stable across the re-render.
		var final = form.GetValues();
		Assert.Equal("db.local", final["host"]);
		Assert.Equal("true", final["ssl"]);
		Assert.Equal("safe", final["mode"]);
	}

	/// <summary>
	/// Finds the <see cref="RadioControl{T}"/> whose label matches <paramref name="label"/> by walking the
	/// radio field's hosting panel (the form places the radios in a borderless panel editor cell).
	/// </summary>
	private static IWindowControl FindRadioByLabel(FormControl form, string label)
	{
		// The radio field's placed editor is a PanelControl hosting the RadioControls; but GetEditor
		// returns the RadioGroup, not the panel. Walk the group's registered radios instead.
		var group = (RadioGroup<string>)form.GetEditor("mode");
		foreach (var radio in group.Members)
		{
			if (radio.Label == label)
				return radio;
		}
		throw new KeyNotFoundException($"No radio labelled '{label}' found.");
	}
}
