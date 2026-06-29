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
using SharpConsoleUI.Core;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// The imperative context handed to a flow body by <see cref="Flow.Run{T}"/>. It exposes the flow's
	/// single cancellation <see cref="Token"/> plus verbs for presenting steps through the current
	/// <see cref="IFlowHost"/>: arbitrary typed content (<see cref="Show{TResult}"/>) and the built-in
	/// primitives (<see cref="Confirm"/>, <see cref="Prompt"/>, <see cref="RunWithProgress{TResult}"/>).
	/// Each verb returns only the resolved value; a Cancel verdict (button or dismiss) maps to
	/// <c>default</c>/<c>false</c>/<c>null</c>.
	/// </summary>
	public sealed class FlowContext
	{
		private readonly ConsoleWindowSystem _ws;
		private readonly IFlowHost _host;
		private readonly CancellationTokenSource _cts;

		/// <summary>
		/// Initializes a new <see cref="FlowContext"/>. Constructed internally by <see cref="Flow.Run{T}"/>.
		/// </summary>
		/// <param name="ws">The window system the flow presents into.</param>
		/// <param name="host">The presentation host (defaults to <see cref="ModalWindowHost"/> in <see cref="Flow.Run{T}"/>).</param>
		/// <param name="cts">The cancellation source whose token drives the whole flow.</param>
		internal FlowContext(ConsoleWindowSystem ws, IFlowHost host, CancellationTokenSource cts)
		{
			_ws = ws ?? throw new ArgumentNullException(nameof(ws));
			_host = host ?? throw new ArgumentNullException(nameof(host));
			_cts = cts ?? throw new ArgumentNullException(nameof(cts));
		}

		/// <summary>
		/// The single cancellation token for the whole flow. Esc / dismiss / host cancellation trips it.
		/// </summary>
		public CancellationToken Token => _cts.Token;

		/// <summary>
		/// The zero-based index of the current step, used to render the step indicator. Defaults to
		/// <c>0</c> for a Tier-A single-shot flow (no indicator shown). Set by the wizard loop (Tier B).
		/// </summary>
		public int StepIndex { get; internal set; }

		/// <summary>
		/// The total step count, used to render the step indicator (e.g. "step 2 of 4"). <c>null</c> for a
		/// flow with no fixed step count (the default for Tier A → no indicator shown).
		/// </summary>
		public int? StepCount { get; internal set; }

		/// <summary>
		/// <c>true</c> once <see cref="Commit"/> has been called for the current step. Marks a Back-barrier
		/// after side-effecting work so the wizard knows the step's effects must not be silently undone.
		/// </summary>
		public bool Committed { get; internal set; }

		/// <summary>
		/// Marks the current step as committed (a Back-barrier). Call after performing side-effecting work
		/// so a subsequent Back is treated as crossing an irreversible boundary.
		/// </summary>
		public void Commit() => Committed = true;

		/// <summary>
		/// Clears the <see cref="Committed"/> flag. Called by the wizard loop (Tier B) at the start of each
		/// step so the commit barrier is evaluated per step rather than for the whole flow.
		/// </summary>
		internal void ResetCommitForStep() => Committed = false;

		/// <summary>
		/// Presents an arbitrary typed step body through the current host and returns its resolved value.
		/// Consistent with the other verbs (<see cref="Confirm"/>, <see cref="Prompt"/>,
		/// <see cref="RunWithProgress{TResult}"/>), a Cancel verdict (Cancel button, dismiss, or token
		/// cancellation) is <em>not</em> an exception: this method simply returns
		/// <c>default(TResult?)</c> and does not abort the enclosing <see cref="Flow.Run{T}"/> body —
		/// the body decides how to react (return, branch, or propagate). Callers that need the precise
		/// verdict should present the step directly via <see cref="PresentStep{TResult}"/> and inspect it.
		/// </summary>
		/// <typeparam name="TResult">The content's result type.</typeparam>
		/// <param name="content">The step body to present.</param>
		/// <param name="title">The title labelling the host frame.</param>
		/// <param name="buttons">
		/// The canonical button row to render. Defaults to <see cref="FlowButtons.OkCancel"/>; pass
		/// <see cref="FlowButtons.None"/> for content that builds and resolves its own buttons.
		/// </param>
		/// <returns>
		/// The content's value on a Next/Finish/OK verdict, or <c>default(TResult?)</c> on a Cancel
		/// verdict (button, dismiss, or token cancellation).
		/// </returns>
		public async Task<TResult?> Show<TResult>(
			IFlowStepContent<TResult> content,
			string title = "",
			FlowButtons buttons = FlowButtons.OkCancel)
		{
			var chrome = new FlowChrome(title, Indicator(), buttons: FlowButtonSets.For(buttons));
			var outcome = await _host.PresentAsync(content, chrome, Token).ConfigureAwait(false);

			// Cancel is NOT an exception: a Cancel verdict returns default, like the other verbs. The
			// body decides what cancellation means. (Whole-flow cancellation flows through the token —
			// e.g. control-removal cancels the flow CTS, which any subsequent await observes.)
			if (outcome.Verdict == FlowVerdict.Cancel)
				return default;

			return outcome.Value;
		}

		/// <summary>
		/// Presents a built-in confirm dialog and returns the user's choice.
		/// </summary>
		/// <param name="title">The dialog title.</param>
		/// <param name="message">The confirmation message.</param>
		/// <param name="ok">Label for the affirmative button. Defaults to <c>"OK"</c>.</param>
		/// <param name="cancel">Label for the negative button. Defaults to <c>"Cancel"</c>.</param>
		/// <param name="severity">Severity controlling the dialog glyph and accent colour.</param>
		/// <returns><c>true</c> when confirmed; <c>false</c> on Cancel or dismiss.</returns>
		public async Task<bool> Confirm(
			string title,
			string message,
			string ok = "OK",
			string cancel = "Cancel",
			NotificationSeverityEnum severity = NotificationSeverityEnum.Info)
		{
			// The primitive builds & owns its OK/Cancel and resolves its own Completion → empty button row.
			var content = new ConfirmContent(message, ok, cancel, severity);
			var chrome = new FlowChrome(title, Indicator(), widthHint: 50, autoSizeHeight: true);
			var outcome = await _host.PresentAsync(content, chrome, Token).ConfigureAwait(false);
			return outcome.Verdict != FlowVerdict.Cancel && outcome.Value;
		}

		/// <summary>
		/// Presents a built-in single-line prompt and returns the entered text.
		/// </summary>
		/// <param name="title">The dialog title.</param>
		/// <param name="message">The prompt question shown above the input.</param>
		/// <param name="initial">Optional initial value pre-filled into the input.</param>
		/// <param name="severity">Severity controlling the dialog glyph and accent colour.</param>
		/// <returns>The entered text, or <c>null</c> on Cancel or dismiss.</returns>
		public async Task<string?> Prompt(
			string title,
			string message,
			string? initial = null,
			NotificationSeverityEnum severity = NotificationSeverityEnum.Info)
		{
			var content = new PromptContent(message, initial, severity);
			var chrome = new FlowChrome(title, Indicator(), widthHint: 50, autoSizeHeight: true);
			var outcome = await _host.PresentAsync(content, chrome, Token).ConfigureAwait(false);
			return outcome.Verdict == FlowVerdict.Cancel ? null : outcome.Value;
		}

		/// <summary>
		/// Presents a built-in progress dialog while running <paramref name="work"/> on a background
		/// thread, surfacing its <see cref="IProgress{T}"/> updates in a live status line.
		/// </summary>
		/// <typeparam name="TResult">The type produced by the work function.</typeparam>
		/// <param name="title">The dialog title.</param>
		/// <param name="description">The initial status text shown below the accent rule.</param>
		/// <param name="work">The async work; receives the flow's cancellation and a status reporter.</param>
		/// <returns>The work's result, or <c>default</c> when cancelled.</returns>
		public async Task<TResult> RunWithProgress<TResult>(
			string title,
			string description,
			Func<CancellationToken, IProgress<string>, Task<TResult>> work)
		{
			// The primitive owns its own Cancel button → empty button row.
			var content = new ProgressContent<TResult>(_ws, description, work);
			var chrome = new FlowChrome(title, Indicator(), widthHint: 54, autoSizeHeight: true);
			var outcome = await _host.PresentAsync(content, chrome, Token).ConfigureAwait(false);
			return outcome.Value!;
		}

		/// <summary>
		/// Presents app-provided content through the current host using a wizard-supplied button row and
		/// optional live <paramref name="refreshButtons"/> delegate, returning the full step outcome
		/// (typed value + chosen verdict). Used by the Tier-B wizard's content+buttons step form; the
		/// wizard owns the button row and the dynamic enable predicate, so this bypasses the Tier-A
		/// default button set used by <see cref="Show{TResult}"/>.
		/// </summary>
		/// <typeparam name="TResult">The content's result type.</typeparam>
		/// <param name="content">The step body to present.</param>
		/// <param name="title">The host frame title.</param>
		/// <param name="buttons">The concrete, context-aware button row to render for this step.</param>
		/// <param name="refreshButtons">
		/// Optional delegate the host re-invokes on each <see cref="IFlowStepContent{TResult}.StateChanged"/>
		/// to re-evaluate the button row's enabled state in place (dynamic buttons).
		/// </param>
		/// <returns>The step outcome carrying the content value and the navigation verdict.</returns>
		internal Task<FlowStepOutcome<TResult>> PresentStep<TResult>(
			IFlowStepContent<TResult> content,
			string title,
			IReadOnlyList<FlowButton> buttons,
			Func<IReadOnlyList<FlowButton>>? refreshButtons)
		{
			var chrome = new FlowChrome(title, Indicator(), buttons: buttons, refreshButtons: refreshButtons);
			return _host.PresentAsync(content, chrome, Token);
		}

		/// <summary>
		/// Builds the step indicator tuple for chrome, or <c>null</c> when no indicator should show
		/// (the Tier-A default of <see cref="StepIndex"/> 0 with a <c>null</c> <see cref="StepCount"/>).
		/// </summary>
		private (int Index, int? Count)? Indicator()
		{
			if (StepCount is { } count)
				return (StepIndex, count);

			return StepIndex == 0 ? null : (StepIndex, (int?)null);
		}
	}
}
