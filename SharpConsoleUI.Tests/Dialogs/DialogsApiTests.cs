// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using DialogsApi = SharpConsoleUI.Dialogs.Dialogs;

namespace SharpConsoleUI.Tests.Dialogs;

/// <summary>
/// Verifies the additive public dialog API layer: <c>MessageAsync</c> (one-button info dialog),
/// <c>ShowAsync</c> (arbitrary <see cref="FlowButton"/> set → clicked <see cref="FlowVerdict"/>), and the
/// <see cref="FlowButtons"/>-preset <c>ConfirmAsync</c> overload. Driven headlessly via the modal/TCS path:
/// the dialog is shown, the built <see cref="ButtonControl"/> is located in the modal window and clicked.
/// </summary>
public class DialogsApiTests
{
	// The dialog runs as a modal window; locate its single window and click the named button.
	private static ButtonControl FindButton(ConsoleWindowSystem sys, string name)
	{
		var win = sys.Windows.Values.Single();
		var btn = win.FindControl<ButtonControl>(name);
		Assert.NotNull(btn);
		return btn!;
	}

	[Fact]
	public async Task MessageAsync_Resolves_OnOkClick()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var task = DialogsApi.MessageAsync(sys, "Info", "Hello");
		sys.Render.UpdateDisplay();

		var ok = FindButton(sys, "flow-message-ok");
		ok.PerformClickForTest();

		await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.True(task.IsCompleted);
	}

	[Fact]
	public async Task MessageAsync_Resolves_OnDismiss()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var task = DialogsApi.MessageAsync(sys, "Info", "Hello");
		sys.Render.UpdateDisplay();

		// Dismiss the window (Esc / title-bar close path) — resolves the content.
		sys.Windows.Values.Single().Close();

		await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.True(task.IsCompleted);
	}

	[Fact]
	public async Task MessageAsync_Literal_EscapesBody()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var task = DialogsApi.MessageAsync(sys, "Info", "[green]hi[/]", literal: true);
		sys.Render.UpdateDisplay();

		var win = sys.Windows.Values.Single();
		var markup = win.GetControlsByType<ScrollablePanelControl>()
			.SelectMany(spc => spc.Children.OfType<MarkupControl>())
			.First(m => m.Text.Contains("green"));
		var text = string.Concat(MarkupParser.Parse(markup.Text, Color.White, Color.Black)
			.Select(c => c.Character.ToString()));
		Assert.Contains("[green]", text);

		FindButton(sys, "flow-message-ok").PerformClickForTest();
		await task.WaitAsync(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task ShowAsync_ReturnsClickedVerdict()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var buttons = new List<FlowButton>
		{
			new("Retry", FlowVerdict.Retry),
			new("Skip", FlowVerdict.Ignore),
		};

		var task = DialogsApi.ShowAsync(sys, "Problem", "Failed", buttons);
		sys.Render.UpdateDisplay();

		// First button.
		FindButton(sys, "flow-show-0").PerformClickForTest();

		var verdict = await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(FlowVerdict.Retry, verdict);
	}

	[Fact]
	public async Task ShowAsync_ReturnsNone_OnDismiss()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var buttons = new List<FlowButton>
		{
			new("Retry", FlowVerdict.Retry),
			new("Skip", FlowVerdict.Ignore),
		};

		var task = DialogsApi.ShowAsync(sys, "Problem", "Failed", buttons);
		sys.Render.UpdateDisplay();

		sys.Windows.Values.Single().Close();

		var verdict = await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(FlowVerdict.None, verdict);
	}

	[Fact]
	public async Task ConfirmAsync_YesNoPreset_ReturnsTrue_OnFirstButton()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var task = DialogsApi.ConfirmAsync(sys, "Q", "Sure?", FlowButtons.YesNo);
		sys.Render.UpdateDisplay();

		// Yes is the first (primary) button.
		FindButton(sys, "flow-show-0").PerformClickForTest();

		var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	[Fact]
	public async Task ConfirmAsync_YesNoPreset_ReturnsFalse_OnSecondButton()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var task = DialogsApi.ConfirmAsync(sys, "Q", "Sure?", FlowButtons.YesNo);
		sys.Render.UpdateDisplay();

		FindButton(sys, "flow-show-1").PerformClickForTest();

		var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.False(result);
	}

	[Fact]
	public async Task ConfirmAsync_Classic_StillReturnsTrue_OnOk()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var task = DialogsApi.ConfirmAsync(sys, "Q", "Sure?");
		sys.Render.UpdateDisplay();

		FindButton(sys, "flow-confirm-ok").PerformClickForTest();

		var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.True(result);
	}

	[Fact]
	public async Task ConfirmAsync_Classic_StillReturnsFalse_OnCancel()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var task = DialogsApi.ConfirmAsync(sys, "Q", "Sure?");
		sys.Render.UpdateDisplay();

		FindButton(sys, "flow-confirm-cancel").PerformClickForTest();

		var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.False(result);
	}

	[Fact]
	public void FlowButtonSets_DialogPresets_Resolve()
	{
		Assert.Equal(new[] { FlowVerdict.Ok }, FlowButtonSets.For(FlowButtons.Ok).Select(b => b.Verdict));
		Assert.Equal(new[] { FlowVerdict.Yes, FlowVerdict.No },
			FlowButtonSets.For(FlowButtons.YesNo).Select(b => b.Verdict));
		Assert.Equal(new[] { FlowVerdict.Yes, FlowVerdict.No, FlowVerdict.Cancel },
			FlowButtonSets.For(FlowButtons.YesNoCancel).Select(b => b.Verdict));
		Assert.Equal(new[] { FlowVerdict.Retry, FlowVerdict.Cancel },
			FlowButtonSets.For(FlowButtons.RetryCancel).Select(b => b.Verdict));
	}
}
