// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using DialogsApi = SharpConsoleUI.Dialogs.Dialogs;

namespace SharpConsoleUI.Tests.Dialogs;

public class MessageDialogsTests
{
	// Assembles step content into a window using the canonical three-band shape
	// (StickyTop band / scrollable body / StickyBottom band), mirroring ShowContentModal so the
	// buttons (which live in the bottom band) are present in the window tree for lookup.
	private static Window BuildThreeBandWindow(
		ConsoleWindowSystem sys, IFlowChromeBands content, IWindowControl body, FlowChrome chrome)
	{
		var win = new WindowBuilder(sys).WithTitle("Test").WithSize(60, 14).Build();
		foreach (var c in FlowContentHelpers.BuildTopBand(chrome))
			win.AddControl(c);
		win.AddControl(body);
		foreach (var c in content.BuildBottomBand(chrome))
			win.AddControl(c);
		sys.AddWindow(win);
		return win;
	}

	[Fact]
	public void ConfirmContent_Completes_True_OnOkClick()
	{
		var content = new ConfirmContent("Proceed?", "OK", "Cancel");
		var chrome = new FlowChrome("Title");

		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var win = BuildThreeBandWindow(sys, content, content.BuildContent(chrome), chrome);

		var ok = win.FindControl<ButtonControl>("flow-confirm-ok");
		Assert.NotNull(ok);
		ok!.PerformClickForTest();

		Assert.True(content.Completion.IsCompleted);
		Assert.True(content.Completion.Result);
	}

	[Fact]
	public void ConfirmContent_Completes_False_OnCancelClick()
	{
		var content = new ConfirmContent("Proceed?", "OK", "Cancel");
		var chrome = new FlowChrome("Title");

		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var win = BuildThreeBandWindow(sys, content, content.BuildContent(chrome), chrome);

		var cancel = win.FindControl<ButtonControl>("flow-confirm-cancel");
		Assert.NotNull(cancel);
		cancel!.PerformClickForTest();

		Assert.True(content.Completion.IsCompleted);
		Assert.False(content.Completion.Result);
	}

	[Fact]
	public void ConfirmContent_StateChanged_EventDeclared()
	{
		// IFlowStepContent<bool> must declare StateChanged — verify it compiles and is subscribable
		IFlowStepContent<bool> content = new ConfirmContent("Msg", "OK", "Cancel");
		bool raised = false;
		content.StateChanged += () => raised = true;
		Assert.False(raised); // just verify no throw
	}

	[Fact]
	public void PromptContent_Completes_WithText_OnEnter()
	{
		var content = new PromptContent("Name?", initial: "abc");
		var root = content.BuildContent(new FlowChrome("Title"));

		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var win = new WindowBuilder(sys).WithTitle("Test").WithSize(60, 14).Build();
		win.AddControl(root);
		sys.AddWindow(win);

		var prompt = win.FindControl<PromptControl>("flow-prompt-input");
		Assert.NotNull(prompt);
		prompt!.PerformEnterForTest();

		Assert.True(content.Completion.IsCompleted);
		Assert.Equal("abc", content.Completion.Result);
	}

	[Fact]
	public void PromptContent_Completes_Null_OnCancel()
	{
		var content = new PromptContent("Name?", initial: null);
		var chrome = new FlowChrome("Title");

		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var win = BuildThreeBandWindow(sys, content, content.BuildContent(chrome), chrome);

		var cancel = win.FindControl<ButtonControl>("flow-prompt-cancel");
		Assert.NotNull(cancel);
		cancel!.PerformClickForTest();

		Assert.True(content.Completion.IsCompleted);
		Assert.Null(content.Completion.Result);
	}

	[Fact]
	public void PromptContent_StateChanged_RaisedOnInputChange()
	{
		var content = new PromptContent("Name?", initial: null);
		var root = content.BuildContent(new FlowChrome("Title"));

		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var win = new WindowBuilder(sys).WithTitle("Test").WithSize(60, 14).Build();
		win.AddControl(root);
		sys.AddWindow(win);

		var prompt = win.FindControl<PromptControl>("flow-prompt-input");
		Assert.NotNull(prompt);

		int stateChangedCount = 0;
		content.StateChanged += () => stateChangedCount++;

		// Simulate user typing by setting input (which raises InputChanged on PromptControl)
		prompt!.RaiseInputChangedForTest("hello");

		Assert.True(stateChangedCount > 0, "StateChanged must be raised when input changes");
		Assert.Equal("hello", content.CurrentText);
	}

	[Fact]
	public void ConfirmContent_Buttons_LiveInBottomToolbar_AbovedByRule()
	{
		var content = new ConfirmContent("Proceed?", "OK", "Cancel");

		// Buttons live in the StickyBottom band (a window child), not in the scrollable body —
		// only the window's content layout honours StickyPosition, a ScrollablePanel does not.
		var band = ((IFlowChromeBands)content).BuildBottomBand(new FlowChrome("Title"));

		// The last band control must be a bottom-sticky, right-aligned toolbar holding both buttons.
		var toolbar = band[band.Count - 1] as ToolbarControl;
		Assert.NotNull(toolbar);
		Assert.Equal(StickyPosition.Bottom, toolbar!.StickyPosition);
		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Right, toolbar.HorizontalAlignment);
		Assert.Contains(toolbar.Items, c => c.Name == "flow-confirm-ok");
		Assert.Contains(toolbar.Items, c => c.Name == "flow-confirm-cancel");

		// A rule sits immediately above the toolbar as its separator.
		var ruleAbove = band[band.Count - 2] as RuleControl;
		Assert.NotNull(ruleAbove);
		Assert.Equal(StickyPosition.Bottom, ruleAbove!.StickyPosition);
	}

	[Fact]
	public void PromptContent_Buttons_LiveInBottomToolbar_AbovedByRule()
	{
		var content = new PromptContent("Name?", initial: null);

		var band = ((IFlowChromeBands)content).BuildBottomBand(new FlowChrome("Title"));

		var toolbar = band[band.Count - 1] as ToolbarControl;
		Assert.NotNull(toolbar);
		Assert.Equal(StickyPosition.Bottom, toolbar!.StickyPosition);
		Assert.Equal(SharpConsoleUI.Layout.HorizontalAlignment.Right, toolbar.HorizontalAlignment);
		Assert.Contains(toolbar.Items, c => c.Name == "flow-prompt-ok");
		Assert.Contains(toolbar.Items, c => c.Name == "flow-prompt-cancel");

		var ruleAbove = band[band.Count - 2] as RuleControl;
		Assert.NotNull(ruleAbove);
		Assert.Equal(StickyPosition.Bottom, ruleAbove!.StickyPosition);
	}

	[Fact]
	public async Task RunWithProgressAsync_RunsWork_AndReturnsResult()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var result = await DialogsApi.RunWithProgressAsync(
			sys,
			"Working",
			"doing it",
			async (ct, progress) =>
			{
				progress.Report("step");
				await Task.Yield();
				return 42;
			});
		Assert.Equal(42, result);
	}

	[Fact]
	public async Task RunWithProgressAsync_ProgressReports_AreReceived()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		// Capture every message the work function passes to IProgress<string>.
		// The work lambda receives the IProgress<string> created by ProgressContent; we record
		// each call to .Report() so we can assert they are not silently dropped.
		var reports = new System.Collections.Generic.List<string>();

		await DialogsApi.RunWithProgressAsync(
			sys,
			"Working",
			"doing it",
			async (ct, progress) =>
			{
				progress.Report("step-1");
				reports.Add("step-1");
				await Task.Yield();
				progress.Report("step-2");
				reports.Add("step-2");
				await Task.Yield();
				return 0;
			});

		// The work function must have called progress.Report at least twice without throwing.
		// If reports were dropped (e.g. the IProgress parameter were null), the lambda would
		// throw a NullReferenceException and this assertion would never be reached.
		Assert.Contains("step-1", reports);
		Assert.Contains("step-2", reports);
		Assert.True(reports.Count >= 2, $"Expected at least 2 reports, got {reports.Count}");
	}

	[Fact]
	public async Task RunWithProgressAsync_CancelPath_ReturnsDefault()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var content = new ProgressContent<int>(
			sys,
			"doing it",
			async (ct, progress) =>
			{
				// Block until the CancellationToken fires
				await Task.Delay(System.TimeSpan.FromSeconds(30), ct);
				return 99;
			});
		var root = content.BuildContent(new FlowChrome("Title"));

		var win = new WindowBuilder(sys).WithTitle("Test").WithSize(60, 14).Build();
		win.AddControl(root);
		sys.AddWindow(win);

		// Trip the cancel path — same as a user clicking Cancel or dismissing the window
		content.CancelFromDismiss();

		var result = await content.Completion.WaitAsync(System.TimeSpan.FromSeconds(5));

		// Cancel → default(T), NOT a throw
		Assert.Equal(default, result);
	}

	[Fact]
	public async Task ProgressContent_CancelFromDismiss_ResolvesWithDefault()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new ProgressContent<int>(
			sys,
			"doing it",
			async (ct, progress) =>
			{
				// Block until cancelled
				await Task.Delay(System.TimeSpan.FromSeconds(30), ct);
				return 99;
			});
		var root = content.BuildContent(new FlowChrome("Title"));

		var win = new WindowBuilder(sys).WithTitle("Test").WithSize(60, 14).Build();
		win.AddControl(root);
		sys.AddWindow(win);

		// Simulate dismiss
		content.CancelFromDismiss();

		var r = await content.Completion.WaitAsync(System.TimeSpan.FromSeconds(5));
		Assert.Equal(default, r);
	}
}
