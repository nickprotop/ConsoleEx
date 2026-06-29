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
using SharpConsoleUI.Flows;
using SharpConsoleUI.Layout;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A container control that renders a single flow step <em>inline</em> — a banner band on top, a
	/// scrollable body in the middle, and a button toolbar at the bottom — instead of opening a modal
	/// window per step. It is the in-control counterpart to
	/// <see cref="SharpConsoleUI.Flows.ModalWindowHost"/> /
	/// <see cref="SharpConsoleUI.Flows.SwapContentHost"/>: drive it via <see cref="AsHost"/> to present
	/// flow steps that live inside an existing window's layout.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Composition.</b> A <see cref="FlowControl"/> <em>is</em> a three-row <see cref="GridControl"/>
	/// (<c>Auto</c> top band / <c>Star</c> body / <c>Auto</c> bottom band) with a flow-presentation API.
	/// It subclasses <see cref="GridControl"/> rather than wrapping one, so it inherits — for free and
	/// without any change to the layout/focus/mouse core — full child hosting: cell children participate
	/// in the layout tree, take focus and Tab order, route mouse clicks, and report a cursor. This is why
	/// a button rendered into the bottom band is reachable and clickable inside the control.
	/// </para>
	/// <para>
	/// <b>Threading.</b> <see cref="ShowStep"/> mutates the grid and so must run on the UI thread; the
	/// inline host marshals its call via <c>ConsoleWindowSystem.EnqueueOnUIThread</c>.
	/// </para>
	/// </remarks>
	public sealed class FlowControl : GridControl
	{
		private InlineFlowHost? _host;
		private bool _running;
		private IWindowControl? _placeholder;

		// The cancellation source for the currently running flow. Created per Run and cancelled when the
		// control is removed from its parent mid-flow so the flow's token trips → FlowResult.Cancelled
		// (the token path is what makes the flow end now that ctx.Show returns default on cancel rather
		// than throwing). Null while idle.
		private CancellationTokenSource? _runCts;

		/// <summary>
		/// Initializes a new, empty <see cref="FlowControl"/>. The control shows nothing until a step is
		/// presented through <see cref="AsHost"/> or one of the <c>Run</c> overloads.
		/// </summary>
		public FlowControl()
		{
			// Fill the height the parent allots. A FlowControl's whole point is to occupy the region it is
			// placed in (banner on top, body filling, toolbar pinned to the bottom). With the BaseControl
			// default of Top it would collapse to its content height (1 row), so when hosted in a container
			// that hands it a tall slot (e.g. a ScrollablePanel) it would render a single line. Fill makes it
			// take the slot. (When added directly to a window it already received the window's content slot.)
			VerticalAlignment = VerticalAlignment.Fill;

			// Stretch to fill the parent's width too. GridControl.MeasureDOM reports zero natural width
			// (the grid sizes its tracks from the parent's allotted width, not its own desired size), so
			// without Stretch a FlowControl added directly to a window/stack arranges at width 0 and the
			// whole region collapses (the Star column divides nothing). Stretch makes it take the parent's
			// width — matching StatusBar/Toolbar/ProgressBar and the other stretch-wide containers. (In the
			// DemoApp it happened to work because a wrapping Panel handed it a width.)
			HorizontalAlignment = HorizontalAlignment.Stretch;

			// One Star column so the single content column fills the control's width. GridLayout needs at
			// least one column definition to size and arrange cells; without it colCount is 0 and the grid
			// collapses to width 0 (cells never arranged). Cells are placed in column 0 throughout.
			ColumnDefinitions.Add(GridLength.Star());

			// Three rows: Auto top band, Star body (fills), Auto bottom band. The body's Star row is what
			// pushes the bottom band to the control's bottom edge regardless of body height.
			RowDefinitions.Add(GridLength.Auto());
			RowDefinitions.Add(GridLength.Star());
			RowDefinitions.Add(GridLength.Auto());

			// Start in the idle/done state.
			ShowPlaceholder();
		}

		/// <summary>
		/// Gets or sets the control displayed when the <see cref="FlowControl"/> is idle (before any
		/// <c>Run</c> call) and after a flow has ended. When <see langword="null"/> the control renders
		/// empty in those states. Setting this property while the control is idle immediately updates the
		/// displayed content; setting it during a running flow stores the value for restoration when the
		/// flow ends.
		/// </summary>
		public IWindowControl? Placeholder
		{
			get => _placeholder;
			set
			{
				_placeholder = value;
				if (!_running)
					ShowPlaceholder();
			}
		}

		/// <summary>
		/// Returns the <see cref="IFlowHost"/> that presents flow steps inside this control. The same host
		/// instance is returned on every call, so steps presented through it share this one control.
		/// </summary>
		/// <returns>The control's inline flow host.</returns>
		public IFlowHost AsHost() => _host ??= new InlineFlowHost(this);

		// -----------------------------------------------------------------------
		// Run overloads
		// -----------------------------------------------------------------------

		/// <summary>
		/// Runs an imperative flow body that produces a typed value inline inside this control.
		/// The body receives a <see cref="FlowContext"/> and returns the flow's value; each step is
		/// presented inside this control (no modal window is opened).
		/// </summary>
		/// <typeparam name="T">The value type produced by the flow body on completion.</typeparam>
		/// <param name="body">The flow body; receives a <see cref="FlowContext"/> and returns the flow's value.</param>
		/// <returns>
		/// A <see cref="FlowResult{T}"/> carrying the body's value on completion, cancelled when the
		/// body throws <see cref="System.OperationCanceledException"/>, or faulted for any other exception.
		/// </returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown synchronously when a flow is already running on this control (re-entrancy guard), or
		/// when the control has not yet been added to a window in a <see cref="ConsoleWindowSystem"/>.
		/// </exception>
		public Task<FlowResult<T>> Run<T>(Func<FlowContext, Task<T>> body)
		{
			// Guard + ws-resolve run synchronously before returning the Task so callers that check for
			// InvalidOperationException (re-entrancy guard) see a synchronous throw, not a faulted Task.
			var ws = GetWindowSystem();
			SetRunning();
			return RunCoreAsync<T>(ws, body);
		}

		private async Task<FlowResult<T>> RunCoreAsync<T>(ConsoleWindowSystem ws, Func<FlowContext, Task<T>> body)
		{
			var token = _runCts!.Token;
			try
			{
				return await Flow.Run(ws, parent: null, body, host: AsHost(), cancellationToken: token).ConfigureAwait(false);
			}
			finally
			{
				ClearRunning();
				ws.EnqueueOnUIThread(() => ShowPlaceholder());
			}
		}

		/// <summary>
		/// Runs an imperative flow body that produces no payload inline inside this control.
		/// The returned <see cref="FlowResult{T}"/> carries <c>bool</c> with
		/// <see cref="FlowResult{T}.Value"/> set to <c>true</c> on completion.
		/// </summary>
		/// <param name="body">The flow body; receives a <see cref="FlowContext"/>.</param>
		/// <returns>A <see cref="FlowResult{T}"/> of <c>bool</c>, with <c>Value == true</c> on completion.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown synchronously when a flow is already running on this control (re-entrancy guard), or
		/// when the control has not yet been added to a window in a <see cref="ConsoleWindowSystem"/>.
		/// </exception>
		public Task<FlowResult<bool>> Run(Func<FlowContext, Task> body)
		{
			var ws = GetWindowSystem();
			SetRunning();
			return RunCoreAsync(ws, body);
		}

		private async Task<FlowResult<bool>> RunCoreAsync(ConsoleWindowSystem ws, Func<FlowContext, Task> body)
		{
			var token = _runCts!.Token;
			try
			{
				return await Flow.Run(ws, parent: null, body, host: AsHost(), cancellationToken: token).ConfigureAwait(false);
			}
			finally
			{
				ClearRunning();
				ws.EnqueueOnUIThread(() => ShowPlaceholder());
			}
		}

		/// <summary>
		/// Runs a declarative wizard inline inside this control. Every step is presented inside this
		/// control instead of opening a modal window per step.
		/// </summary>
		/// <typeparam name="TState">The mutable wizard state type.</typeparam>
		/// <param name="wizard">The configured <see cref="FlowWizardBuilder{TState}"/> to run.</param>
		/// <returns>The wizard outcome carrying the final state on completion.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown synchronously when a flow is already running on this control (re-entrancy guard), or
		/// when the control has not yet been added to a window in a <see cref="ConsoleWindowSystem"/>.
		/// </exception>
		public Task<FlowResult<TState>> Run<TState>(FlowWizardBuilder<TState> wizard) where TState : new()
		{
			ArgumentNullException.ThrowIfNull(wizard);
			var ws = GetWindowSystem();
			SetRunning();
			return RunWizardCoreAsync(ws, wizard);
		}

		private async Task<FlowResult<TState>> RunWizardCoreAsync<TState>(
			ConsoleWindowSystem ws,
			FlowWizardBuilder<TState> wizard) where TState : new()
		{
			var token = _runCts!.Token;
			try
			{
				return await wizard.Run(ws, parent: null, host: AsHost(), cancellationToken: token).ConfigureAwait(false);
			}
			finally
			{
				ClearRunning();
				ws.EnqueueOnUIThread(() => ShowPlaceholder());
			}
		}

		// -----------------------------------------------------------------------
		// Lifecycle hooks
		// -----------------------------------------------------------------------

		/// <summary>
		/// Overrides the <see cref="GridControl.Container"/> setter to detect removal from the parent
		/// while a flow is running. When the control is detached (<paramref name="value"/> is
		/// <see langword="null"/>) and a flow is in progress, the per-run cancellation token is cancelled
		/// so the flow observes <see cref="System.OperationCanceledException"/> and resolves as
		/// <see cref="FlowResult{T}.Cancelled"/> instead of hanging indefinitely. The inline host's
		/// in-flight step is also resolved promptly (with <see cref="FlowVerdict.Cancel"/>) so the
		/// pending <c>await</c> unblocks immediately rather than only on the next token-observing await.
		/// </summary>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				if (value == null && _running)
				{
					// The token cancel is what makes Flow.Run/Wizard.Run return Cancelled (ctx.Show now
					// returns default on a Cancel verdict rather than throwing). CancelCurrent additionally
					// resolves the in-flight step right away so the await does not wait for the next one.
					_runCts?.Cancel();
					_host?.CancelCurrent();
				}
			}
		}

		// -----------------------------------------------------------------------
		// Private helpers
		// -----------------------------------------------------------------------

		/// <summary>
		/// Obtains the <see cref="ConsoleWindowSystem"/> this control is hosted in, throwing a clear
		/// message when the control has not yet been added to a window.
		/// </summary>
		private ConsoleWindowSystem GetWindowSystem()
			=> ((IContainer)this).GetConsoleWindowSystem
				?? throw new InvalidOperationException(
					"FlowControl must be added to a window before Run is called.");

		/// <summary>
		/// Asserts the re-entrancy guard: sets <c>_running = true</c> or throws
		/// <see cref="InvalidOperationException"/> if a flow is already running.
		/// </summary>
		private void SetRunning()
		{
			if (_running)
				throw new InvalidOperationException(
					"A flow is already running on this FlowControl. Await or cancel it before starting another.");
			_running = true;
			_runCts = new CancellationTokenSource();
		}

		/// <summary>
		/// Clears the running state and disposes the per-run cancellation source. Called from each
		/// <c>Run</c> overload's <c>finally</c>.
		/// </summary>
		private void ClearRunning()
		{
			_running = false;
			_runCts?.Dispose();
			_runCts = null;
		}

		/// <summary>
		/// Displays the <see cref="Placeholder"/> (if any) in the grid, replacing whatever was shown
		/// before. Called from the constructor and from each <c>Run</c> overload's <c>finally</c>.
		/// </summary>
		private void ShowPlaceholder()
		{
			ClearControls();
			if (_placeholder != null)
				Place(_placeholder, 1, 0); // star-row body slot, column 0
		}

		/// <summary>
		/// (Re)builds the inner grid to show one flow step: the <paramref name="top"/> band in row 0, the
		/// <paramref name="body"/> in row 1 (filling), and the <paramref name="bottom"/> band in row 2.
		/// Replaces whatever step was shown before. Must be called on the UI thread.
		/// </summary>
		/// <param name="top">The top-band controls (banner + accent rule). May be empty.</param>
		/// <param name="body">The scrollable step body that fills the middle row.</param>
		/// <param name="bottom">The bottom-band controls (ruler + button toolbar). May be empty.</param>
		/// <param name="title">The step title (reserved for future header rendering; not painted today).</param>
		internal void ShowStep(
			IReadOnlyList<IWindowControl> top,
			IWindowControl body,
			IReadOnlyList<IWindowControl> bottom,
			string title)
		{
			_ = title; // Reserved: a future task may render the title as a header band.

			// Swap: drop the previous step's controls, then place this step's three bands.
			ClearControls();

			var topBand = WrapBand(top);
			if (topBand != null)
				Place(topBand, 0, 0);

			Place(body, 1, 0);

			var bottomBand = WrapBand(bottom);
			if (bottomBand != null)
				Place(bottomBand, 2, 0);
		}

		/// <summary>
		/// Collapses a band (a list of controls) into the single child a grid cell holds: returns the lone
		/// control unchanged when the band has exactly one control, wraps several in a borderless,
		/// scrollbar-less <see cref="ScrollablePanelControl"/> (which stacks them vertically), and returns
		/// <see langword="null"/> for an empty band so the caller leaves that row empty.
		/// </summary>
		private static IWindowControl? WrapBand(IReadOnlyList<IWindowControl> band)
		{
			if (band == null || band.Count == 0)
				return null;
			if (band.Count == 1)
				return band[0];

			var panel = Ctl.ScrollablePanel()
				.WithScrollbar(false)
				.WithVerticalScroll(ScrollMode.None);
			foreach (var c in band)
				panel.AddControl(c);
			return panel.Build();
		}
	}
}
