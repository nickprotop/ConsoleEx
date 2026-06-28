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
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// An <see cref="IFlowHost"/> that presents each step <em>inline</em> inside a
	/// <see cref="FlowControl"/> instead of opening a window. Each presentation builds the canonical
	/// three-band surface (the same shape <see cref="ModalWindowHost"/> and <see cref="SwapContentHost"/>
	/// build) and swaps it into the target control via <see cref="FlowControl.ShowStep"/>.
	/// </summary>
	/// <remarks>
	/// <para>Resolution mirrors <see cref="ModalWindowHost"/> exactly (first wins, via <c>TrySetResult</c>):
	/// a host button click resolves with that button's verdict; the content completing its own
	/// <see cref="IFlowStepContent{TResult}.Completion"/> resolves <see cref="FlowVerdict.Next"/> (gated on
	/// status so faults/cancellations are not masked); token cancellation resolves
	/// <see cref="FlowVerdict.Cancel"/>.</para>
	/// <para>Unlike the window hosts there is NO window to open or close: nothing is closed in the
	/// <c>finally</c> (the control stays in place and is reused for the next step). Handling the control
	/// being removed mid-flow is a later task.</para>
	/// </remarks>
	internal sealed class InlineFlowHost : IFlowHost
	{
		private readonly FlowControl _target;

		// Action that cancels the step currently being presented. Set at the start of PresentAsync
		// and cleared in its finally. Invoked by CancelCurrent when the FlowControl is removed from
		// its parent mid-flow so the in-flight task resolves immediately with Cancel instead of hanging.
		private volatile Action? _cancelCurrentStep;

		/// <summary>
		/// Initializes a new <see cref="InlineFlowHost"/> bound to the control it renders steps into.
		/// </summary>
		/// <param name="target">The control to render flow steps inside.</param>
		public InlineFlowHost(FlowControl target)
		{
			_target = target ?? throw new ArgumentNullException(nameof(target));
		}

		/// <summary>
		/// Cancels the step that is currently being presented (if any), resolving it with
		/// <see cref="FlowVerdict.Cancel"/>. Called by <see cref="FlowControl"/> when the control is
		/// removed from its parent while a flow is running so the flow resolves as
		/// <see cref="FlowResult{T}.Cancelled"/> instead of hanging indefinitely.
		/// </summary>
		internal void CancelCurrent() => _cancelCurrentStep?.Invoke();

		/// <inheritdoc/>
		public async Task<FlowStepOutcome<TResult>> PresentAsync<TResult>(
			IFlowStepContent<TResult> content, FlowChrome chrome, CancellationToken ct)
		{
			ArgumentNullException.ThrowIfNull(content);

			var ws = ((IContainer)_target).GetConsoleWindowSystem
				?? throw new InvalidOperationException(
					"FlowControl must be added to a window in a ConsoleWindowSystem before presenting a step.");

			var tcs = new TaskCompletionSource<FlowStepOutcome<TResult>>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			// Register this step as the cancellable current step so CancelCurrent can unblock it.
			_cancelCurrentStep = () =>
				tcs.TrySetResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel));

			// Build the step body (called once per presentation).
			var body = content.BuildContent(chrome);

			// Build the host's standardized button row (empty when chrome.Buttons is empty).
			var buttonControls = new List<(ButtonControl Control, FlowButton Spec)>();
			FlowStepPresenter.BuildButtonControls(chrome, tcs, content, buttonControls);

			// Assemble the canonical three bands. The top band is ALWAYS host-built from chrome (title +
			// accent rule), so every step — primitive, plain custom content, or wizard — shows a consistent
			// banner. Content that supplies its own bottom band (the framework primitives) owns its
			// ruler+buttons verbatim; otherwise the host builds a sticky-bottom right-aligned toolbar from
			// chrome.Buttons. Mirrors ModalWindowHost.
			IReadOnlyList<IWindowControl> top = FlowContentHelpers.BuildTopBand(chrome);
			IReadOnlyList<IWindowControl> bottom;

			if (content is IFlowChromeBands bands)
			{
				bottom = bands.BuildBottomBand(chrome);
			}
			else
			{
				if (buttonControls.Count > 0)
				{
					var buttons = new ButtonControl[buttonControls.Count];
					for (int i = 0; i < buttonControls.Count; i++)
						buttons[i] = buttonControls[i].Control;
					bottom = FlowContentHelpers.BuildBottomBand(ColorRole.Primary, buttons);
				}
				else
				{
					bottom = Array.Empty<IWindowControl>();
				}
			}

			FlowStepPresenter.WireBodySelfResolve(content, tcs);

			// Dynamic buttons: re-evaluate enabled state in place on each StateChanged.
			Action onStateChanged = () => FlowStepPresenter.RefreshButtonStates(chrome, buttonControls);
			content.StateChanged += onStateChanged;

			// Swap the step into the control on the UI thread (ShowStep mutates the grid).
			var wrappedBody = FlowContentHelpers.WrapBody(body);
			ws.EnqueueOnUIThread(() => _target.ShowStep(top, wrappedBody, bottom, FlowChromeFormat.FormatTitle(chrome)));

			// Token cancellation → Cancel. No window to close: the control is reused for the next step.
			using var ctReg = ct.Register(() =>
				tcs.TrySetResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel)));

			try
			{
				return await tcs.Task.ConfigureAwait(false);
			}
			finally
			{
				content.StateChanged -= onStateChanged;
				// Clear the cancellable-step registration so a stale CancelCurrent call after
				// the step has already resolved is a no-op (the next step registers itself).
				_cancelCurrentStep = null;
			}
		}
	}
}
