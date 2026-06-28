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
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

/// <summary>
/// Real-thing end-to-end tests: drive entire flows through the real <see cref="ModalWindowHost"/>
/// (the default host — no <see cref="HeadlessFlowHost"/>) with real UI rendering and button clicks,
/// then assert the observable end state (FlowResult + no leaked windows).
/// </summary>
public class FlowRealThingTests
{
	// -----------------------------------------------------------------------
	// Helpers
	// -----------------------------------------------------------------------

	private static ConsoleWindowSystem Sys() => TestWindowSystemBuilder.CreateTestSystem(120, 40);

	/// <summary>
	/// Drains the UI thread queue until no windows remain or the attempt limit is reached.
	/// The ModalWindowHost marshals modal.Close via EnqueueOnUIThread; we must drain it.
	/// </summary>
	private static async Task WaitForNoWindowsAsync(ConsoleWindowSystem system)
	{
		for (int i = 0; i < 50 && system.Windows.Values.Any(); i++)
		{
			system.DrainPendingUIActionsForTest();
			await Task.Delay(10);
		}
	}


	// -----------------------------------------------------------------------
	// Tier-A flow E2E: Confirm → true → RunWithProgress → value 7
	// -----------------------------------------------------------------------

	[Fact]
	public async Task Flow_OverRealModalHost_ConfirmOk_ThenProgress_Completes_Value7_NoLeakedWindow()
	{
		var system = Sys();

		// Start the flow but do NOT await yet — we need to drive the UI interactively.
		var flowTask = Flow.Run<int>(system, null, async ctx =>
		{
			if (!await ctx.Confirm("Confirm", "Proceed?")) return 0;
			return await ctx.RunWithProgress(
				"Working", "doing it",
				async (ct, p) => { p.Report("go"); await Task.Yield(); return 7; });
		});

		// Render so the ConfirmContent modal is built and its buttons exist in the window tree.
		system.Render.UpdateDisplay();

		// Click the OK button rendered inside ConfirmContent.
		bool clicked = FlowTestHelpers.ClickButtonByName(system, "flow-confirm-ok");
		Assert.True(clicked, "Expected to find and click 'flow-confirm-ok' inside the confirm modal.");

		// Pump once more to let the progress modal appear and work complete.
		system.Render.UpdateDisplay();

		// Await completion with a guard timeout so a hang is a hard failure, not a hang.
		var result = await flowTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed but got: Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(7, result.Value);

		// No flow modal window left open after completion.
		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	// -----------------------------------------------------------------------
	// Tier-A flow E2E: Cancel path — click Cancel on confirm → body returns 0
	// -----------------------------------------------------------------------

	[Fact]
	public async Task Flow_OverRealModalHost_ConfirmCancel_ReturnsZero_NoLeakedWindow()
	{
		var system = Sys();

		var flowTask = Flow.Run<int>(system, null, async ctx =>
		{
			if (!await ctx.Confirm("Confirm", "Proceed?")) return 0;
			return await ctx.RunWithProgress(
				"Working", "doing it",
				async (ct, p) => { p.Report("go"); await Task.Yield(); return 7; });
		});

		system.Render.UpdateDisplay();

		// Click Cancel — ctx.Confirm returns false, body returns 0.
		bool clicked = FlowTestHelpers.ClickButtonByName(system, "flow-confirm-cancel");
		Assert.True(clicked, "Expected to find and click 'flow-confirm-cancel' inside the confirm modal.");

		system.Render.UpdateDisplay();

		var result = await flowTask.WaitAsync(TimeSpan.FromSeconds(10));

		// Body returned 0 (early exit via false confirm), so the flow Completed with value 0.
		Assert.True(result.Completed, $"Expected Completed but got: Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(0, result.Value);

		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	// -----------------------------------------------------------------------
	// Wizard E2E through the real ModalWindowHost: 2-step content+buttons wizard
	// -----------------------------------------------------------------------

	private sealed class WizardState
	{
		public int Step1Value;
		public int Step2Value;
	}

	/// <summary>
	/// Minimal step content that writes to <see cref="WizardState"/> and resolves immediately
	/// so the host button row (Back/Next/Finish/Cancel) is the only navigation path.
	/// </summary>
	private sealed class RecordingContent : IFlowStepContent<object?>, IRaiseStateChangedForTest
	{
		private readonly TaskCompletionSource<object?> _tcs = new();
		private readonly Action<WizardState> _write;
		private readonly WizardState _state;

		public RecordingContent(WizardState state, Action<WizardState> write)
		{
			_state = state;
			_write = write;
		}

		public Task<object?> Completion => _tcs.Task;

		public event Action? StateChanged;

		public void RaiseStateChanged() => StateChanged?.Invoke();

		public IWindowControl BuildContent(FlowChrome chrome)
		{
			_write(_state);
			return Builders.Controls.Markup().AddLine("step body").Build();
		}
	}

	[Fact]
	public async Task Wizard_TwoSteps_OverRealHost_ClickNextThenFinish_Completes_NoLeakedWindow()
	{
		var system = Sys();
		var state = new WizardState();

		// Build a 2-step content+buttons wizard (no host override → real ModalWindowHost used).
		// Step 1 is NOT the last step → host renders [Next, Cancel].
		// Step 2 IS the last step   → host renders [Finish, Back, Cancel].
		var wizardTask = Flow.Wizard<WizardState>()
			.Seed(state)
			.Step((_) => new RecordingContent(state, s => s.Step1Value = 1))
			.Step((_) => new RecordingContent(state, s => s.Step2Value = 2))
			.Run(system, null);  // null host → real ModalWindowHost

		// --- Step 1: render, click Next ---
		system.Render.UpdateDisplay();

		// On step 1 (not last), the standardized button is "Next" with verdict Next.
		bool clickedNext = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next");
		Assert.True(clickedNext, "Expected to find and click 'flow-host-btn-Next' on step 1.");

		// --- Step 2: poll until the step-1 modal closes and the step-2 "Finish" button appears, then click. ---
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Finish", "step 2: Finish");

		system.Render.UpdateDisplay();

		var result = await wizardTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed but got: Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.NotNull(result.Value);
		Assert.Equal(1, result.Value!.Step1Value);
		Assert.Equal(2, result.Value!.Step2Value);

		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	[Fact]
	public async Task Wizard_TwoSteps_OverRealHost_CancelOnStep1_ReturnsCancelled_NoLeakedWindow()
	{
		var system = Sys();
		var state = new WizardState();

		var wizardTask = Flow.Wizard<WizardState>()
			.Seed(state)
			.Step((_) => new RecordingContent(state, s => s.Step1Value = 1))
			.Step((_) => new RecordingContent(state, s => s.Step2Value = 2))
			.Run(system, null);

		system.Render.UpdateDisplay();

		bool clickedCancel = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Cancel");
		Assert.True(clickedCancel, "Expected to find and click 'flow-host-btn-Cancel' on step 1.");

		system.Render.UpdateDisplay();

		var result = await wizardTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Cancelled, $"Expected Cancelled but got: Completed={result.Completed} Faulted={result.Faulted}");

		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	[Fact]
	public async Task Wizard_TwoSteps_OverRealHost_BackOnStep2_ReturnsToStep1_ThenFinishes_NoLeakedWindow()
	{
		var system = Sys();
		var state = new WizardState();

		int step1Renders = 0;

		var wizardTask = Flow.Wizard<WizardState>()
			.Seed(state)
			.Step((_) => new RecordingContent(state, s => { s.Step1Value = 1; step1Renders++; }))
			.Step((_) => new RecordingContent(state, s => s.Step2Value = 2))
			.Run(system, null);

		// Each step transition is async; poll until the expected button is present-and-clickable
		// rather than guessing a fixed delay (which raced under parallel load).
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Next", "step 1: Next");
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Back", "step 2: Back");        // Back → step 1
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Next", "step 1 (again): Next");
		await FlowTestHelpers.WaitAndClickButtonAsync(system, "flow-host-btn-Finish", "step 2 (again): Finish");

		var result = await wizardTask.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed but: Cancelled={result.Cancelled} Faulted={result.Faulted}");
		// Step 1 rendered twice: initial visit + Back revisit
		Assert.Equal(2, step1Renders);

		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}
}
