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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Flows;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

public class FlowWizardTests
{
	private sealed class S
	{
		public int Visits1;
		public int Visits2;
		public bool Ready;
	}

	private static ConsoleWindowSystem Sys() => new(new HeadlessConsoleDriver(120, 40));

	[Fact]
	public async Task Wizard_RunsStepsInOrder_AndFinishes()
	{
		var r = await Flow.Wizard<S>()
			.Step((ctx, s) => { s.Visits1++; return Task.FromResult(FlowVerdict.Next); })
			.Step((ctx, s) => { s.Visits2++; return Task.FromResult(FlowVerdict.Finish); })
			.Run(Sys(), null, new HeadlessFlowHost());

		Assert.True(r.Completed);
		Assert.Equal(1, r.Value!.Visits1);
		Assert.Equal(1, r.Value!.Visits2);
	}

	[Fact]
	public async Task Wizard_Back_RerunsPreviousStep_WithStatePreserved()
	{
		bool wentBackOnce = false;
		var r = await Flow.Wizard<S>()
			.Step((ctx, s) => { s.Visits1++; return Task.FromResult(FlowVerdict.Next); })
			.Step((ctx, s) =>
			{
				s.Visits2++;
				if (!wentBackOnce) { wentBackOnce = true; return Task.FromResult(FlowVerdict.Back); }
				return Task.FromResult(FlowVerdict.Finish);
			})
			.Run(Sys(), null, new HeadlessFlowHost());

		Assert.True(r.Completed);
		Assert.Equal(2, r.Value!.Visits1); // step 1 ran twice (initial + after Back)
		Assert.Equal(2, r.Value!.Visits2); // step 2 ran twice
	}

	[Fact]
	public async Task Wizard_Cancel_ReturnsCancelled()
	{
		var r = await Flow.Wizard<S>()
			.Step((ctx, s) => Task.FromResult(FlowVerdict.Cancel))
			.Run(Sys(), null, new HeadlessFlowHost());

		Assert.True(r.Cancelled);
	}

	[Fact]
	public async Task Wizard_Back_PastCommittedStep_IsRefused()
	{
		// Step 2 tries to go Back past the committed step 1.
		// The blocked Back must NOT move below the commit barrier and must NOT cancel the wizard.
		// Guard the Back-attempt with a counter so the step finishes after one refused Back.
		int step0Runs = 0;
		bool backAttempted = false;
		var r = await Flow.Wizard<S>()
			.Step((ctx, s) => { step0Runs++; return Task.FromResult(FlowVerdict.Next); })
			.Step((ctx, s) => { ctx.Commit(); return Task.FromResult(FlowVerdict.Next); })
			.Step((ctx, s) =>
			{
				if (!backAttempted)
				{
					backAttempted = true;
					return Task.FromResult(FlowVerdict.Back); // blocked — commit barrier is at step 1
				}
				return Task.FromResult(FlowVerdict.Finish); // step re-presented; now finish
			})
			.Run(Sys(), null, new HeadlessFlowHost());

		// The blocked Back re-presented step 2 (Stay semantics); step 0 ran exactly once (no
		// navigation below the barrier), and the wizard completed rather than being cancelled.
		Assert.Equal(1, step0Runs);
		Assert.True(r.Completed);
	}

	[Fact]
	public async Task Wizard_Back_AtStep0_RePresentsStep_WizardNotCancelled()
	{
		// At step 0 there is nowhere to go Back to; the blocked Back must re-present step 0 (Stay
		// semantics) and keep the wizard alive.  Guard the Back with a bool so it terminates.
		int step0Runs = 0;
		bool backAttempted = false;
		var r = await Flow.Wizard<S>()
			.Step((ctx, s) =>
			{
				step0Runs++;
				if (!backAttempted)
				{
					backAttempted = true;
					return Task.FromResult(FlowVerdict.Back); // wholly blocked at step 0
				}
				return Task.FromResult(FlowVerdict.Finish); // re-presented; now finish
			})
			.Run(Sys(), null, new HeadlessFlowHost());

		// Step 0 ran twice: once with the blocked Back, once on re-presentation with Finish.
		Assert.Equal(2, step0Runs);
		Assert.True(r.Completed);
	}

	[Fact]
	public async Task Wizard_Stay_RepresentsSameStep()
	{
		bool stayedOnce = false;
		int stepRuns = 0;
		var r = await Flow.Wizard<S>()
			.Step((ctx, s) =>
			{
				stepRuns++;
				if (!stayedOnce) { stayedOnce = true; return Task.FromResult(FlowVerdict.Stay); }
				return Task.FromResult(FlowVerdict.Finish);
			})
			.Run(Sys(), null, new HeadlessFlowHost());

		Assert.True(r.Completed);
		Assert.Equal(2, stepRuns); // ran twice: Stay re-presented the same step, no index advance
	}

	// --- Content + buttons step (form 2) ---

	/// <summary>
	/// A minimal app-provided content that writes nothing itself; the wizard's CanGoNext reads TState.
	/// Implements the test hook so HeadlessFlowHost can fire StateChanged.
	/// </summary>
	private sealed class DummyContent : IFlowStepContent<object?>, IRaiseStateChangedForTest
	{
		private readonly TaskCompletionSource<object?> _tcs = new();

		public IWindowControl BuildContent(FlowChrome chrome) => new MarkupControl(new List<string> { "body" });

		public Task<object?> Completion => _tcs.Task;

		public event Action? StateChanged;

		public void RaiseStateChanged() => StateChanged?.Invoke();
	}

	[Fact]
	public async Task Wizard_ContentStep_CanGoNext_DisablesNext_AndStateChangedReEvaluates()
	{
		var state = new S { Ready = false };

		IReadOnlyList<FlowButton>? capturedButtons = null;
		Func<IReadOnlyList<FlowButton>>? capturedRefresh = null;

		var host = new CapturingHeadlessHost(
			(content, chrome) =>
			{
				capturedButtons = chrome.Buttons;
				capturedRefresh = chrome.RefreshButtons;
			},
			HeadlessFlowHost.Answer(null, FlowVerdict.Finish));

		var runTask = Flow.Wizard<S>()
			.Seed(state)
			.Step(_ => new DummyContent())
				.CanGoNext((ctx, s) => s.Ready)
			.Run(Sys(), null, host);

		var r = await runTask;

		Assert.NotNull(capturedButtons);
		Assert.NotNull(capturedRefresh);

		// Single content step → last step → [Finish, Back?, Cancel]. Finish maps to the "Next/Finish"
		// gate controlled by CanGoNext. Initially Ready=false → the Finish (advance) button is disabled.
		var finishBefore = capturedButtons!.First(b => b.Verdict == FlowVerdict.Finish);
		Assert.False(finishBefore.Enabled);

		// Flip readiness and re-evaluate via the refresh delegate (what the host calls on StateChanged).
		state.Ready = true;
		var refreshed = capturedRefresh!();
		var finishAfter = refreshed.First(b => b.Verdict == FlowVerdict.Finish);
		Assert.True(finishAfter.Enabled);

		Assert.True(r.Completed);
	}

	/// <summary>
	/// A headless host that also captures the chrome of the first presented step, then replays a
	/// scripted answer like <see cref="HeadlessFlowHost"/>.
	/// </summary>
	private sealed class CapturingHeadlessHost : IFlowHost
	{
		private readonly Action<object, FlowChrome> _capture;
		private readonly Queue<HeadlessFlowHost.ScriptedOutcome> _answers;
		private bool _captured;

		public CapturingHeadlessHost(Action<object, FlowChrome> capture, params HeadlessFlowHost.ScriptedOutcome[] answers)
		{
			_capture = capture;
			_answers = new Queue<HeadlessFlowHost.ScriptedOutcome>(answers);
		}

		public Task<FlowStepOutcome<TResult>> PresentAsync<TResult>(
			IFlowStepContent<TResult> content, FlowChrome chrome, System.Threading.CancellationToken ct)
		{
			if (!_captured)
			{
				_captured = true;
				_capture(content!, chrome);
			}

			if (_answers.Count == 0)
				return Task.FromResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel));

			var scripted = _answers.Dequeue();
			var value = scripted.Value is TResult typed ? typed : default;
			return Task.FromResult(new FlowStepOutcome<TResult>(value, scripted.Verdict));
		}
	}
}
