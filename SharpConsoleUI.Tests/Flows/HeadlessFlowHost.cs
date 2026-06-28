// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

/// <summary>
/// A scripted, terminal-free <see cref="IFlowHost"/> for unit-testing flows. Returns a queue of
/// pre-scripted <see cref="FlowStepOutcome{TResult}"/> answers in order (value + verdict),
/// records the chrome title of each presented step, and lets a test fire the last presented
/// content's <see cref="IFlowStepContent{TResult}.StateChanged"/> so dynamic re-evaluation paths
/// can be exercised without a real window.
/// </summary>
public sealed class HeadlessFlowHost : IFlowHost
{
	private readonly Queue<ScriptedOutcome> _answers;

	/// <summary>The chrome titles of every step presented, in presentation order.</summary>
	public List<string> PresentedTitles { get; } = new();

	/// <summary>
	/// Initializes a new <see cref="HeadlessFlowHost"/> with a sequence of scripted answers.
	/// Each entry is replayed in order as one step is presented.
	/// </summary>
	/// <param name="answers">
	/// The scripted outcomes, in order. Use <see cref="Answer(object?, FlowVerdict)"/> to build them.
	/// When the queue is exhausted the host returns <c>(default, FlowVerdict.Cancel)</c>.
	/// </param>
	public HeadlessFlowHost(params ScriptedOutcome[] answers)
		=> _answers = new Queue<ScriptedOutcome>(answers);

	/// <summary>
	/// A test-supplied delegate that fires <see cref="IFlowStepContent{TResult}.StateChanged"/> on
	/// the content most recently passed to <see cref="PresentAsync"/>. <c>null</c> until a step is
	/// presented. Useful for asserting that dynamic button re-evaluation reacts to body edits.
	/// </summary>
	public Action? FireStateChangedOnLastPresented { get; private set; }

	/// <inheritdoc/>
	public Task<FlowStepOutcome<TResult>> PresentAsync<TResult>(
		IFlowStepContent<TResult> content, FlowChrome chrome, CancellationToken ct)
	{
		PresentedTitles.Add(chrome.Title);

		// Wire a way for tests to raise this content's StateChanged.
		FireStateChangedOnLastPresented = content.RaiseStateChangedForTest;

		if (ct.IsCancellationRequested)
			return Task.FromResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel));

		if (_answers.Count == 0)
			return Task.FromResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel));

		var scripted = _answers.Dequeue();
		var value = scripted.Value is TResult typed ? typed : default;
		return Task.FromResult(new FlowStepOutcome<TResult>(value, scripted.Verdict));
	}

	/// <summary>Builds a scripted outcome (value + verdict) for the host to replay.</summary>
	/// <param name="value">The value to surface for this step (boxed; cast to TResult at present time).</param>
	/// <param name="verdict">The verdict to surface for this step.</param>
	public static ScriptedOutcome Answer(object? value, FlowVerdict verdict) => new(value, verdict);

	/// <summary>A single scripted step result: a boxed value plus a navigation verdict.</summary>
	public readonly struct ScriptedOutcome
	{
		/// <summary>Initializes a scripted outcome.</summary>
		/// <param name="value">The boxed value to surface (cast to TResult at present time).</param>
		/// <param name="verdict">The verdict to surface.</param>
		public ScriptedOutcome(object? value, FlowVerdict verdict)
		{
			Value = value;
			Verdict = verdict;
		}

		/// <summary>The boxed value to surface for the step.</summary>
		public object? Value { get; }

		/// <summary>The navigation verdict to surface for the step.</summary>
		public FlowVerdict Verdict { get; }
	}
}

/// <summary>
/// Small helpers for driving flow content through tests without real mouse input.
/// </summary>
public static class FlowTestHelpers
{
	/// <summary>
	/// Walks every window in <paramref name="system"/> for a <see cref="ButtonControl"/> whose
	/// <see cref="BaseControl.Name"/> matches <paramref name="name"/> and raises its Click.
	/// </summary>
	/// <param name="system">The window system to search.</param>
	/// <param name="name">The button's <c>Name</c>.</param>
	/// <returns><c>true</c> if a matching button was found and clicked; otherwise <c>false</c>.</returns>
	public static bool ClickButtonByName(ConsoleWindowSystem system, string name)
	{
		foreach (var window in system.Windows.Values)
		{
			var btn = window.FindControl<ButtonControl>(name);
			if (btn != null)
			{
				btn.PerformClickForTest();
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Waits for a wizard/flow step transition to settle, then clicks the named host button. A step
	/// change is asynchronous: the prior step resolves, the wizard-loop continuation runs (often on the
	/// thread pool) and enqueues the next step's <c>ShowStep</c>/modal via <c>EnqueueOnUIThread</c>, and
	/// the next button is named only once that has applied. Polling (drain + render) until the button is
	/// present-and-clickable replaces a fixed <c>Task.Delay</c>, which raced under parallel test load.
	/// Fails the test if the button never appears within the bounded timeout (~2s).
	/// </summary>
	public static async Task WaitAndClickButtonAsync(ConsoleWindowSystem system, string name, string because)
	{
		for (int i = 0; i < 200; i++)
		{
			system.DrainPendingUIActionsForTest();
			system.Render.UpdateDisplay();
			if (ClickButtonByName(system, name))
				return;
			await Task.Delay(10);
		}

		Assert.Fail($"{because}: button '{name}' never became clickable within the timeout.");
	}
}

/// <summary>
/// Extension that exposes a content's <c>StateChanged</c> as a callable delegate for tests.
/// Implemented by raising through the public event subscription set in production code; since the
/// event can only be raised from inside the declaring type, this relies on the content type
/// providing an internal test hook. For the framework primitives and test doubles that is the
/// <c>RaiseStateChanged*</c> method; this shim falls back to a no-op when none is available.
/// </summary>
internal static class FlowStepContentTestExtensions
{
	public static void RaiseStateChangedForTest<TResult>(this IFlowStepContent<TResult> content)
	{
		switch (content)
		{
			case IRaiseStateChangedForTest hook:
				hook.RaiseStateChanged();
				break;
		}
	}
}

/// <summary>Opt-in test hook a content can implement so a host/test can fire its StateChanged.</summary>
internal interface IRaiseStateChangedForTest
{
	/// <summary>Raises the content's StateChanged event.</summary>
	void RaiseStateChanged();
}
