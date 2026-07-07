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

namespace SharpConsoleUI.Flows
{
	public static partial class Flow
	{
		/// <summary>
		/// Begins building a declarative wizard (Tier B) over a mutable <typeparamref name="TState"/>.
		/// Add ordered steps with <see cref="FlowWizardBuilder{TState}.Step(Func{FlowContext, TState, Task{FlowVerdict}})"/>
		/// (code-driven) or <see cref="FlowWizardBuilder{TState}.Step(Func{TState, IFlowStepContent{object}})"/>
		/// (content + standardized buttons), then run with
		/// <see cref="FlowWizardBuilder{TState}.Run(ConsoleWindowSystem, Window?, IFlowHost?, System.Threading.CancellationToken)"/>. The
		/// wizard owns the navigation loop (Next/Back/Cancel/Finish/Stay), the step indicator, and the
		/// Back commit-barrier; the shared state flows through every step.
		/// </summary>
		/// <typeparam name="TState">The mutable wizard state type; default-constructed unless seeded.</typeparam>
		/// <returns>A new <see cref="FlowWizardBuilder{TState}"/>.</returns>
		public static FlowWizardBuilder<TState> Wizard<TState>() where TState : new()
			=> new FlowWizardBuilder<TState>();
	}

	/// <summary>
	/// Fluent builder for a declarative <see cref="Flow.Wizard{TState}"/>. Steps are added in order and
	/// reduced to a single navigation loop. Two step forms coexist:
	/// <list type="number">
	/// <item><description>
	/// <b>Code-driven</b> — a <see cref="Func{T1, T2, TResult}"/> that runs the step body (using the
	/// shared <see cref="FlowContext"/> and state) and returns a <see cref="FlowVerdict"/> directly.
	/// </description></item>
	/// <item><description>
	/// <b>Content + standardized buttons</b> — a content factory that returns
	/// <see cref="IFlowStepContent{TResult}"/> (typed as <c>object?</c>); the wizard renders the
	/// context-aware button row, presents the content, and maps the chosen button to a verdict, applying
	/// any per-step fluent overrides (<see cref="FlowStepConfig{TState}"/>).
	/// </description></item>
	/// </list>
	/// </summary>
	/// <typeparam name="TState">The mutable wizard state type; default-constructed unless seeded.</typeparam>
	/// <remarks>
	/// <para>
	/// The content+buttons form presents the content as <see cref="IFlowStepContent{TResult}"/> of
	/// <c>object?</c>: the wizard does not consume the content's typed value. Such steps are for
	/// side-effect navigation where the content writes its result into <typeparamref name="TState"/>
	/// itself (e.g. a version picker writing <c>s.Version</c>), and the fluent callbacks read
	/// <typeparamref name="TState"/>. This keeps the builder mono-generic over
	/// <typeparamref name="TState"/> rather than per-step typed.
	/// </para>
	/// </remarks>
	public sealed class FlowWizardBuilder<TState> where TState : new()
	{
		private readonly List<Func<FlowContext, TState, Task<FlowVerdict>>> _steps = new();
		private TState _state = new();
		private bool _indicator;
		private string _title = string.Empty;
		private bool _seamless;

		/// <summary>Sets the initial wizard state (otherwise a default-constructed instance is used).</summary>
		/// <param name="state">The seed state.</param>
		/// <returns>This builder, for chaining.</returns>
		public FlowWizardBuilder<TState> Seed(TState state)
		{
			_state = state;
			return this;
		}

		/// <summary>
		/// Enables the step indicator: each step's <see cref="FlowChrome"/> carries a
		/// <c>(Index, Count)</c> tuple so the host can render e.g. "(2/3)".
		/// </summary>
		/// <returns>This builder, for chaining.</returns>
		public FlowWizardBuilder<TState> WithStepIndicator()
		{
			_indicator = true;
			return this;
		}

		/// <summary>
		/// Opts this wizard into the seamless single-window host (<see cref="SwapContentHost"/>): every
		/// step is presented in ONE reused modal window whose content is swapped per step (no open/close
		/// flicker between steps), instead of the default fresh-modal-per-step <see cref="ModalWindowHost"/>.
		/// Ignored when an explicit <c>host</c> is passed to <see cref="Run"/>.
		/// </summary>
		/// <returns>This builder, for chaining.</returns>
		public FlowWizardBuilder<TState> WithSeamlessHost()
		{
			_seamless = true;
			return this;
		}

		/// <summary>
		/// Sets a default title used for content+buttons steps that do not override it. Code-driven steps
		/// title their own host frames via the <see cref="FlowContext"/> verbs.
		/// </summary>
		/// <param name="title">The default step title.</param>
		/// <returns>This builder, for chaining.</returns>
		public FlowWizardBuilder<TState> WithTitle(string title)
		{
			_title = title ?? string.Empty;
			return this;
		}

		/// <summary>
		/// Adds a <b>code-driven</b> step: the body runs with the shared context and state and returns a
		/// <see cref="FlowVerdict"/> that drives the loop.
		/// </summary>
		/// <param name="step">The step body returning a navigation verdict.</param>
		/// <returns>This builder, for chaining.</returns>
		public FlowWizardBuilder<TState> Step(Func<FlowContext, TState, Task<FlowVerdict>> step)
		{
			ArgumentNullException.ThrowIfNull(step);
			_steps.Add(step);
			return this;
		}

		/// <summary>
		/// Adds a <b>content + standardized buttons</b> step: the factory builds the step content (which
		/// writes its result into <typeparamref name="TState"/>), and the wizard renders the
		/// context-aware button row, presents the content, and maps the chosen button to a verdict.
		/// Returns a <see cref="FlowStepConfig{TState}"/> sub-builder for fluent per-step overrides
		/// (<c>.OnNext</c>, <c>.OnBack</c>, <c>.OnCancel</c>, <c>.CanGoNext</c>, <c>.NextLabel</c>,
		/// <c>.BackLabel</c>); its members return the same sub-builder, and adding the next step continues
		/// from the parent wizard builder.
		/// </summary>
		/// <param name="contentFactory">
		/// Builds the step content for the current state. The content's typed value is ignored; callbacks
		/// read <typeparamref name="TState"/>.
		/// </param>
		/// <returns>A <see cref="FlowStepConfig{TState}"/> for fluent customization of this step.</returns>
		public FlowStepConfig<TState> Step(Func<TState, IFlowStepContent<object?>> contentFactory)
		{
			ArgumentNullException.ThrowIfNull(contentFactory);
			var config = new FlowStepConfig<TState>(this, contentFactory);
			// The config registers the reduced code-driven step into _steps when first created so step
			// ordering matches call order even if no fluent overrides follow.
			_steps.Add(config.Invoke);
			return config;
		}

		/// <summary>
		/// Runs the wizard: presents each step in order through <paramref name="host"/> (or a default
		/// <see cref="ModalWindowHost"/>), honouring Next/Back/Cancel/Finish/Stay, the Back commit-barrier,
		/// and the optional step indicator. A wholly-blocked Back (at step 0 or pinned by the commit
		/// barrier) re-presents the current step unchanged (Stay semantics) — the wizard and its
		/// state are preserved and the loop continues. Cancellation surfaces as
		/// <see cref="FlowResult{T}.Cancelled"/>; any other exception is logged and surfaced as
		/// <see cref="FlowResult{T}.Faulted"/>.
		/// </summary>
		/// <param name="ws">The window system the wizard presents into.</param>
		/// <param name="parent">Optional parent window for the default modal host; ignored when <paramref name="host"/> is supplied.</param>
		/// <param name="host">Optional presentation host. When <c>null</c> a <see cref="ModalWindowHost"/> is used.</param>
		/// <param name="cancellationToken">
		/// Optional external cancellation. When it is cancelled the wizard's own token trips (via a linked
		/// source), so the in-flight step resolves Cancel and the loop surfaces
		/// <see cref="FlowResult{T}.Cancelled"/>. Used, for example, by <see cref="Controls.FlowControl"/>
		/// to cancel a running inline wizard when its control is removed from the visual tree mid-flow.
		/// </param>
		/// <returns>The wizard outcome carrying the final state on completion.</returns>
		public async Task<FlowResult<TState>> Run(
			ConsoleWindowSystem ws,
			Window? parent,
			IFlowHost? host = null,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(ws);

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var activeHost = host
				?? (_seamless ? new SwapContentHost(ws, parent) : (IFlowHost)new ModalWindowHost(ws, parent));
			var ctx = new FlowContext(ws, activeHost, cts);

			// commitBarrier = the lowest step index Back may return to. A step that calls ctx.Commit()
			// raises the barrier to the NEXT index, so Back can never move before (or onto a re-run of) it.
			int commitBarrier = 0;
			int i = 0;

			try
			{
				while (i < _steps.Count)
				{
					ctx.StepIndex = i + 1;
					ctx.StepCount = _indicator ? _steps.Count : (int?)null;
					ctx.ResetCommitForStep();

					// Expose the active step index to content+buttons steps so they can size the button row.
					_activeIndex = i;

					var verdict = await _steps[i](ctx, _state).ConfigureAwait(false);

					// The wizard token is the authoritative whole-flow cancel signal (external token or
					// control-removal cancelling the linked source). A step that ignored the token would
					// otherwise return a non-Cancel verdict; honour the token and end Cancelled.
					if (cts.IsCancellationRequested)
						return FlowResult<TState>.Cancel();

					if (ctx.Committed)
						commitBarrier = Math.Max(commitBarrier, i + 1);

					// Non-exhaustive navigation switch: only the flow-navigation verdicts are handled here.
					// The dialog-answer verdicts (Ok/Yes/No/Retry/Abort/Ignore/None) are consumed by the
					// dialog layer, never emitted by a wizard step, so an unlisted value is a safe no-op.
					switch (verdict)
					{
						case FlowVerdict.Next:
							i++;
							break;
						case FlowVerdict.Finish:
							return FlowResult<TState>.Complete(_state);
						case FlowVerdict.Cancel:
							return FlowResult<TState>.Cancel();
						case FlowVerdict.Back:
							{
								int target = Math.Max(commitBarrier, i - 1); // floor: never Back past a committed step
								if (target == i)
									// Back is wholly blocked: the commit barrier (or step 0) means there is no
									// earlier step to move to. Re-present the current step unchanged (Stay
									// semantics) — the wizard and its state are preserved. A code-driven step
									// that returns Back unconditionally will loop here indefinitely, which is
									// the same contract as a step that returns Stay unconditionally.
									break; // leave i unchanged → same step re-presented
								i = target;
							}
							break;
						case FlowVerdict.Stay:
							// Re-present the SAME step: leave i unchanged.
							break;
					}
				}

				return FlowResult<TState>.Complete(_state);
			}
			catch (OperationCanceledException)
			{
				return FlowResult<TState>.Cancel();
			}
			catch (Exception ex)
			{
				ws.LogService?.LogError($"Wizard faulted: {ex.Message}", ex, "Flows");
				return FlowResult<TState>.Fault(ex);
			}
			finally
			{
				// Tear down hosts that own a long-lived resource (e.g. the seamless SwapContentHost's
				// reused window). The default ModalWindowHost is not IDisposable → no-op (non-breaking).
				if (activeHost is IDisposable disposable)
					disposable.Dispose();
			}
		}

		// --- internal seam used by FlowStepConfig (content+buttons step) ---

		private int _activeIndex;

		/// <summary>The zero-based index of the step currently executing (set by the run loop).</summary>
		internal int ActiveIndex => _activeIndex;

		/// <summary>Total step count (for first/last button-row context).</summary>
		internal int StepCount => _steps.Count;

		/// <summary>The default step title for content+buttons steps.</summary>
		internal string DefaultTitle => _title;
	}
}
