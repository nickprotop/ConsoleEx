// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Fluent per-step customization sub-builder for a content+buttons wizard step (see
	/// <see cref="FlowWizardBuilder{TState}.Step(Func{TState, IFlowStepContent{object}})"/>). Lets the
	/// app override the affirmative button label/enable predicate and supply click callbacks whose
	/// returned <see cref="FlowVerdict"/> drives the loop. Its members return this sub-builder; the
	/// parent wizard's builder methods (<c>Step</c>, <c>WithStepIndicator</c>, <c>WithTitle</c>,
	/// <c>Seed</c>, <c>Run</c>) are forwarded so chaining the next step or running flows naturally.
	/// </summary>
	/// <typeparam name="TState">The mutable wizard state type.</typeparam>
	public sealed class FlowStepConfig<TState> where TState : new()
	{
		private readonly FlowWizardBuilder<TState> _parent;
		private readonly Func<TState, IFlowStepContent<object?>> _contentFactory;

		private Func<FlowContext, TState, bool>? _canGoNext;
		private Func<FlowContext, TState, Task<FlowVerdict>>? _onNext;
		private Func<FlowContext, TState, Task<FlowVerdict>>? _onBack;
		private Func<FlowContext, TState, Task<FlowVerdict>>? _onCancel;
		private string? _nextLabel;
		private string? _backLabel;
		private string? _title;

		internal FlowStepConfig(FlowWizardBuilder<TState> parent, Func<TState, IFlowStepContent<object?>> contentFactory)
		{
			_parent = parent;
			_contentFactory = contentFactory;
		}

		/// <summary>
		/// Sets the predicate controlling the affirmative (Next/Finish) button's enabled state. It is
		/// re-evaluated live on the content's <see cref="IFlowStepContent{TResult}.StateChanged"/> via the
		/// chrome's <see cref="FlowChrome.RefreshButtons"/> delegate.
		/// </summary>
		/// <param name="predicate">Returns <c>true</c> to enable the affirmative button.</param>
		/// <returns>This sub-builder, for chaining.</returns>
		public FlowStepConfig<TState> CanGoNext(Func<FlowContext, TState, bool> predicate)
		{
			_canGoNext = predicate;
			return this;
		}

		/// <summary>Callback invoked when the affirmative (Next/Finish) button is clicked; its return is the verdict.</summary>
		/// <param name="callback">The click callback returning the navigation verdict.</param>
		/// <returns>This sub-builder, for chaining.</returns>
		public FlowStepConfig<TState> OnNext(Func<FlowContext, TState, Task<FlowVerdict>> callback)
		{
			_onNext = callback;
			return this;
		}

		/// <summary>Callback invoked when the Back button is clicked; its return is the verdict (default Back).</summary>
		/// <param name="callback">The click callback returning the navigation verdict.</param>
		/// <returns>This sub-builder, for chaining.</returns>
		public FlowStepConfig<TState> OnBack(Func<FlowContext, TState, Task<FlowVerdict>> callback)
		{
			_onBack = callback;
			return this;
		}

		/// <summary>Callback invoked when the Cancel button is clicked; its return is the verdict (default Cancel).</summary>
		/// <param name="callback">The click callback returning the navigation verdict.</param>
		/// <returns>This sub-builder, for chaining.</returns>
		public FlowStepConfig<TState> OnCancel(Func<FlowContext, TState, Task<FlowVerdict>> callback)
		{
			_onCancel = callback;
			return this;
		}

		/// <summary>Relabels the affirmative (Next/Finish) button.</summary>
		/// <param name="label">The new label.</param>
		/// <returns>This sub-builder, for chaining.</returns>
		public FlowStepConfig<TState> NextLabel(string label)
		{
			_nextLabel = label;
			return this;
		}

		/// <summary>Relabels the Back button.</summary>
		/// <param name="label">The new label.</param>
		/// <returns>This sub-builder, for chaining.</returns>
		public FlowStepConfig<TState> BackLabel(string label)
		{
			_backLabel = label;
			return this;
		}

		/// <summary>Sets the host frame title for this step.</summary>
		/// <param name="title">The title.</param>
		/// <returns>This sub-builder, for chaining.</returns>
		public FlowStepConfig<TState> WithStepTitle(string title)
		{
			_title = title;
			return this;
		}

		// --- forwarders to the parent builder so chaining continues naturally ---

		/// <inheritdoc cref="FlowWizardBuilder{TState}.Step(Func{FlowContext, TState, Task{FlowVerdict}})"/>
		public FlowWizardBuilder<TState> Step(Func<FlowContext, TState, Task<FlowVerdict>> step) => _parent.Step(step);

		/// <inheritdoc cref="FlowWizardBuilder{TState}.Step(Func{TState, IFlowStepContent{object}})"/>
		public FlowStepConfig<TState> Step(Func<TState, IFlowStepContent<object?>> contentFactory) => _parent.Step(contentFactory);

		/// <inheritdoc cref="FlowWizardBuilder{TState}.Seed(TState)"/>
		public FlowWizardBuilder<TState> Seed(TState state) => _parent.Seed(state);

		/// <inheritdoc cref="FlowWizardBuilder{TState}.WithStepIndicator"/>
		public FlowWizardBuilder<TState> WithStepIndicator() => _parent.WithStepIndicator();

		/// <inheritdoc cref="FlowWizardBuilder{TState}.WithTitle(string)"/>
		public FlowWizardBuilder<TState> WithTitle(string title) => _parent.WithTitle(title);

		/// <inheritdoc cref="FlowWizardBuilder{TState}.Run(ConsoleWindowSystem, Window?, IFlowHost?, System.Threading.CancellationToken)"/>
		public Task<FlowResult<TState>> Run(
			ConsoleWindowSystem ws,
			Window? parent,
			IFlowHost? host = null,
			System.Threading.CancellationToken cancellationToken = default)
			=> _parent.Run(ws, parent, host, cancellationToken);

		// --- reduction to a code-driven step (form 1) ---

		/// <summary>
		/// The reduced code-driven step body: builds content, computes the context-aware button row with
		/// label/enable overrides, presents the content (wiring the live enable predicate to
		/// <see cref="FlowChrome.RefreshButtons"/>), then maps the chosen button verdict to the matching
		/// callback's result (or the button's default verdict when no callback is set).
		/// </summary>
		internal async Task<FlowVerdict> Invoke(FlowContext ctx, TState state)
		{
			var content = _contentFactory(state);

			bool isFirst = _parent.ActiveIndex == 0;
			bool isLast = _parent.ActiveIndex == _parent.StepCount - 1;

			// The affirmative button is Finish on the last step, otherwise Next.
			FlowVerdict affirmativeVerdict = isLast ? FlowVerdict.Finish : FlowVerdict.Next;
			string affirmativeLabel = _nextLabel ?? (isLast ? "Finish" : "Next");
			string backLabel = _backLabel ?? "Back";

			List<FlowButton> BuildButtons()
			{
				bool affirmativeEnabled = _canGoNext?.Invoke(ctx, state) ?? true;
				var list = new List<FlowButton>
				{
					new FlowButton(affirmativeLabel, affirmativeVerdict, affirmativeEnabled),
				};
				if (!isFirst)
					list.Add(new FlowButton(backLabel, FlowVerdict.Back));
				list.Add(new FlowButton("Cancel", FlowVerdict.Cancel));
				return list;
			}

			IReadOnlyList<FlowButton> initialButtons = BuildButtons();
			Func<IReadOnlyList<FlowButton>>? refresh = _canGoNext is null ? null : () => BuildButtons();

			string title = _title ?? _parent.DefaultTitle;

			var outcome = await ctx.PresentStep(content, title, initialButtons, refresh).ConfigureAwait(false);

			// Map the chosen button verdict to the matching callback (if any); the callback's return
			// drives the loop. With no callback, the button's own verdict is used.
			switch (outcome.Verdict)
			{
				case FlowVerdict.Next:
				case FlowVerdict.Finish:
					return _onNext is not null
						? await _onNext(ctx, state).ConfigureAwait(false)
						: outcome.Verdict;
				case FlowVerdict.Back:
					return _onBack is not null
						? await _onBack(ctx, state).ConfigureAwait(false)
						: FlowVerdict.Back;
				case FlowVerdict.Cancel:
					return _onCancel is not null
						? await _onCancel(ctx, state).ConfigureAwait(false)
						: FlowVerdict.Cancel;
				default:
					return outcome.Verdict;
			}
		}
	}
}
