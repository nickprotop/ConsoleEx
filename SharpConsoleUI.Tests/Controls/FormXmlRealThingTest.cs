// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Controls.Forms;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// The "real thing" end-to-end test for the <see cref="FormXml"/> loader: a connection-form built from
/// an XML STRING via <see cref="FormXml.FromXml(string, IReadOnlyDictionary{string, Func{string?, string?}}?)"/>,
/// placed in a NARROW window and driven through the REAL dispatch pipeline. It proves the XML-loaded form
/// is behaviorally IDENTICAL to an imperative one — validation gates submit, real keyboard input works,
/// and submit carries the correct value snapshot — the XML-loaded analog of <see cref="FormRealThingTest"/>.
///
/// <para>
/// This mirrors the actual usage path, NOT isolated component asserts:
/// <list type="bullet">
///   <item>Real container nesting — a <see cref="FormControl"/> built by the loader (which IS a
///   <see cref="GridControl"/>) inside a narrow <see cref="Window"/>, hosting a real required host
///   <see cref="PromptControl"/> plus a collapsed section field and an OK/Cancel button row.</item>
///   <item>Boundary-stressing width (40 cols) so the auto label column and star editor column split a
///   tight width.</item>
///   <item>Real input path — typing goes through
///   <c>FocusManager.SetFocus + InputStateService.EnqueueKey + system.Input.ProcessInput</c>; submit goes
///   through the OK button's click seam (the same delegate the real button fires).</item>
///   <item>Re-render between action and assert, and the final state is re-checked after a second
///   <c>UpdateDisplay</c> so it must SURVIVE a re-layout.</item>
/// </list>
/// </para>
/// </summary>
public class FormXmlRealThingTest
{
	private const int Width = 40;
	private const int Height = 24;

	private const string ConnectionFormXml = @"
<form>
  <text name='host' label='Host' required='true' hint='hostname or IP' />
  <section title='Advanced' collapsible='true' collapsed='true'>
    <text name='timeout' label='Timeout' initial='30' />
  </section>
  <buttons />
</form>";

	private static void Render(ConsoleWindowSystem system)
	{
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
	}

	/// <summary>
	/// The full end-to-end story for an XML-loaded form: an empty required host blocks
	/// <see cref="FormControl.Submitted"/> and shows the error; typing the host through the REAL keyboard
	/// pipeline makes the form valid; submit then fires ONCE with the correct value snapshot; and every
	/// observable end state survives a re-render.
	/// </summary>
	[Fact]
	public void FormXml_Validate_Submit_RealThing()
	{
		Console.SetIn(TextReader.Null);

		var system = TestWindowSystemBuilder.CreateTestSystem(Width, Height);
		var window = new Window(system) { Left = 0, Top = 0, Width = Width, Height = Height };

		// Build the form from the XML STRING — this is the loader under test.
		var form = FormXml.FromXml(ConnectionFormXml);

		window.AddControl(form);
		system.AddWindow(window);

		// First renders establish the arranged geometry of every editor.
		Render(system);

		var hostPrompt = (PromptControl)form.GetEditor("host");
		var timeoutPrompt = (PromptControl)form.GetEditor("timeout");

		// --- 1. The collapsed "Advanced" section starts hidden --------------------------------------
		Assert.False(timeoutPrompt.Visible,
			"the collapsed Advanced section's timeout editor must start hidden.");
		Assert.False(form.HasErrorForTest("host"), "no validation should have run yet.");

		// --- 2. Validation gates submit: host empty → no Submitted, error shown ---------------------
		int submittedCount = 0;
		IReadOnlyDictionary<string, string?>? submittedValues = null;
		form.Submitted += (_, values) =>
		{
			submittedCount++;
			submittedValues = values;
		};

		Assert.Equal(string.Empty, hostPrompt.Input);

		form.ClickOkForTest();
		Render(system);

		Assert.Equal(0, submittedCount);
		Assert.True(form.HasErrorForTest("host"),
			"an empty required host must show a validation error.");
		Assert.Equal("Required", form.ErrorTextForTest("host"));
		Assert.Null(submittedValues);

		// --- 3. Type the host through the REAL input pipeline ---------------------------------------
		window.FocusManager.SetFocus(hostPrompt, FocusReason.Programmatic);
		Assert.True(hostPrompt.HasFocus, "the host prompt must accept programmatic focus before typing.");

		foreach (char c in "db.local")
		{
			system.InputStateService.EnqueueKey(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false));
			system.Input.ProcessInput();
		}
		Render(system);

		Assert.Equal("db.local", hostPrompt.Input);

		// --- 4. Submit now passes and fires ONCE with the correct snapshot --------------------------
		form.ClickOkForTest();
		Render(system);

		Assert.Equal(1, submittedCount);
		Assert.False(form.HasErrorForTest("host"), "the host error must clear once it is filled.");
		Assert.NotNull(submittedValues);
		Assert.Equal("db.local", submittedValues!["host"]);
		Assert.Equal("db.local", form.GetValues()["host"]);

		// --- 5. Every observable end state SURVIVES a second re-render ------------------------------
		Render(system);
		Assert.Equal(1, submittedCount);
		Assert.Equal("db.local", hostPrompt.Input);
		Assert.False(form.HasErrorForTest("host"));
		Assert.Equal("db.local", form.GetValues()["host"]);
	}
}
