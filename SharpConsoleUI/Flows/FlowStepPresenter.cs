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
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Shared step-resolution helpers used by all <see cref="IFlowHost"/> implementations.
	/// Centralises the three pieces of per-step wiring that would otherwise be duplicated
	/// verbatim in each host: button construction, body self-resolve, and dynamic button
	/// state refresh.
	/// </summary>
	internal static class FlowStepPresenter
	{
		/// <summary>
		/// Builds a <see cref="ButtonControl"/> for every <see cref="FlowButton"/> in
		/// <paramref name="chrome"/>.Buttons, wires each button's <c>Click</c> to resolve
		/// <paramref name="tcs"/> with the button's verdict, and appends each pair to
		/// <paramref name="sink"/>. The first button receives the <c>Primary</c> colour role.
		/// Does nothing when <c>chrome.Buttons</c> is null or empty.
		/// </summary>
		public static void BuildButtonControls<TResult>(
			FlowChrome chrome,
			TaskCompletionSource<FlowStepOutcome<TResult>> tcs,
			IFlowStepContent<TResult> content,
			List<(ButtonControl Control, FlowButton Spec)> sink)
		{
			var buttons = chrome.Buttons;
			if (buttons == null || buttons.Count == 0)
				return;

			for (int i = 0; i < buttons.Count; i++)
			{
				var spec = buttons[i];
				// First button is the affirmative/default action → Primary role tint.
				// No per-button margins: the buttons sit flush in the right-aligned toolbar,
				// matching the FlowContentHelpers.BuildBottomBand layout exactly.
				var builder = Ctl.Button(spec.Label)
					.WithName($"flow-host-btn-{spec.Label}")
					.Enabled(spec.Enabled);

				if (i == 0)
					builder.WithColorRole(Themes.ColorRole.Primary);

				var btn = builder.Build();

				var verdict = spec.Verdict;
				btn.Click += (_, _) =>
				{
					// Value-on-verdict: value-bearing verdicts read the content's current result if
					// already resolved; otherwise default(TResult).
					TResult? value = default;
					if ((verdict == FlowVerdict.Next || verdict == FlowVerdict.Finish)
						&& content.Completion.Status == TaskStatus.RanToCompletion)
					{
						value = content.Completion.Result;
					}

					tcs.TrySetResult(new FlowStepOutcome<TResult>(value, verdict));
				};

				sink.Add((btn, spec));
			}
		}

		/// <summary>
		/// Wires <paramref name="content"/>.Completion via <c>ContinueWith</c> so that the
		/// body's self-resolve drives <paramref name="tcs"/>: RanToCompletion → Next,
		/// Faulted → TrySetException (inner exception), Canceled → Cancel verdict.
		/// The continuation runs synchronously on <see cref="TaskScheduler.Default"/>.
		/// </summary>
		public static void WireBodySelfResolve<TResult>(
			IFlowStepContent<TResult> content,
			TaskCompletionSource<FlowStepOutcome<TResult>> tcs)
		{
			// Body self-resolve: gate on completion status so faults and cancellations are not
			// silently masked as a successful Next resolution.
			_ = content.Completion.ContinueWith(
				t =>
				{
					if (t.Status == TaskStatus.RanToCompletion)
					{
						tcs.TrySetResult(new FlowStepOutcome<TResult>(t.Result, FlowVerdict.Next));
					}
					else if (t.Status == TaskStatus.Faulted)
					{
						var ex = t.Exception!.InnerException ?? t.Exception;
						tcs.TrySetException(ex);
					}
					else
					{
						// Canceled
						tcs.TrySetResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel));
					}
				},
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);
		}

		/// <summary>
		/// Re-evaluates each button's enabled state by invoking <c>chrome.RefreshButtons</c>
		/// and matching returned specs to live controls by verdict+label. Used as the
		/// <c>content.StateChanged</c> handler for dynamic button state.
		/// Does nothing when <c>chrome.RefreshButtons</c> is null.
		/// </summary>
		public static void RefreshButtonStates(
			FlowChrome chrome,
			List<(ButtonControl Control, FlowButton Spec)> buttonControls)
		{
			var refresh = chrome.RefreshButtons;
			if (refresh == null)
				return;

			var updated = refresh();
			if (updated == null)
				return;

			// Match refreshed specs to live controls by verdict+label (the row is built from the same set).
			foreach (var (control, spec) in buttonControls)
			{
				foreach (var u in updated)
				{
					if (u.Verdict == spec.Verdict && u.Label == spec.Label)
					{
						control.IsEnabled = u.Enabled;
						break;
					}
				}
			}
		}
	}
}
