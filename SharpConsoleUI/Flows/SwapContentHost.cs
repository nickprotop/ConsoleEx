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
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// An opt-in <see cref="IFlowHost"/> that presents every step in ONE reused modal window, swapping
	/// the window's content per step instead of opening a fresh window each time. This gives a seamless
	/// wizard experience (no open/close flicker between steps), in contrast to the framework-default
	/// <see cref="ModalWindowHost"/> (a fresh modal per step).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The window is created lazily on the FIRST <see cref="PresentAsync{TResult}"/> call and reused on
	/// every subsequent call: each step calls <see cref="Window.ClearControls"/>, re-titles the window,
	/// and adds the SAME canonical three-band surface that <see cref="ModalWindowHost"/> builds
	/// (<see cref="IFlowChromeBands"/> → top/body/bottom; else body + a sticky-bottom right-aligned
	/// toolbar from <see cref="FlowChrome.Buttons"/>).
	/// </para>
	/// <para>Resolution paths mirror <see cref="ModalWindowHost"/> exactly (whichever fires first wins
	/// via <c>TrySetResult</c>): a host button click resolves with that button's verdict; the content
	/// completing its own <see cref="IFlowStepContent{TResult}.Completion"/> resolves Next (gated on
	/// status so faults/cancellations are not masked); token cancellation resolves Cancel; user dismissal
	/// of the window (Esc / title-bar close) resolves the CURRENT step as Cancel.</para>
	/// <para>
	/// The window stays open BETWEEN steps and is closed when the host is disposed, when the user
	/// dismisses it, or when a presentation's token cancels. Dispose the host to tear the window down on
	/// normal completion: <see cref="Flow.Run{T}"/> and the wizard <c>Run</c> dispose the host in their
	/// <c>finally</c> when it is <see cref="IDisposable"/>. Use via <c>using</c> when driving steps
	/// directly.
	/// </para>
	/// </remarks>
	public sealed class SwapContentHost : IFlowHost, IDisposable
	{
		private readonly ConsoleWindowSystem _ws;
		private readonly Window? _parent;
		private readonly object _gate = new();

		private Window? _window;
		private bool _disposed;

		// The current step's resolver. A single OnClosed handler (wired once when the window is created)
		// cancels whichever step is currently presented by reading this field.
		private Action? _cancelCurrentStep;

		/// <summary>
		/// Initializes a new <see cref="SwapContentHost"/>.
		/// </summary>
		/// <param name="ws">The window system to present steps in.</param>
		/// <param name="parent">Optional parent window; when provided the reused modal is modal to it.</param>
		public SwapContentHost(ConsoleWindowSystem ws, Window? parent)
		{
			_ws = ws ?? throw new ArgumentNullException(nameof(ws));
			_parent = parent;
		}

		/// <inheritdoc/>
		public async Task<FlowStepOutcome<TResult>> PresentAsync<TResult>(
			IFlowStepContent<TResult> content, FlowChrome chrome, CancellationToken ct)
		{
			ArgumentNullException.ThrowIfNull(content);

			lock (_gate)
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(SwapContentHost));
			}

			var tcs = new TaskCompletionSource<FlowStepOutcome<TResult>>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			// Build the step body (called once per presentation).
			var body = content.BuildContent(chrome);

			// Build the host's standardized button row (empty when chrome.Buttons is empty).
			var buttonControls = new List<(ButtonControl Control, FlowButton Spec)>();
			FlowStepPresenter.BuildButtonControls(chrome, tcs, content, buttonControls);

			// Ensure the single reused window exists, then swap its content to this step's surface.
			var window = EnsureWindow(chrome);

			// Point the shared OnClosed handler at THIS step's resolver, so a user dismiss while this
			// step is presented resolves the step as Cancel.
			_cancelCurrentStep = () =>
				tcs.TrySetResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel));

			SetWindowSurface(window, content, chrome, body, buttonControls);

			// D3: update the reused window's frame to this step's role each step (severity can change
			// between steps). Border setters Invalidate(Repaint); marshal to the UI thread.
			var (activeBorder, inactiveBorder) = FlowContentHelpers.ResolveBorderColors(chrome, _ws.Theme);
			_ws.EnqueueOnUIThread(() =>
			{
				window.ActiveBorderForegroundColor = activeBorder;
				window.InactiveBorderForegroundColor = inactiveBorder;
			});

			// D2: size the reused window to this step (per the precedence) and re-center for the new
			// height (Centered() computed position once at build; it does NOT re-center on resize).
			int width = chrome.WidthHint ?? 50;
			int resolvedHeight = FlowContentHelpers.ResolveWindowHeight(
				chrome, body, width, bandRows: 6, terminalHeight: _ws.DesktopDimensions.Height, fixedDefault: 12);
			_ws.EnqueueOnUIThread(() =>
			{
				if (window.Height != resolvedHeight)
					window.Height = resolvedHeight;
				if (window.Width != width)
					window.Width = width;
				// Re-center for the (possibly new) size, mirroring WindowBuilder.Centered().
				var desk = _ws.DesktopDimensions;
				window.Left = System.Math.Max(0, (desk.Width - window.Width) / 2);
				window.Top = System.Math.Max(0, (desk.Height - window.Height) / 2);
			});

			FlowStepPresenter.WireBodySelfResolve(content, tcs);

			// Dynamic buttons: re-evaluate enabled state in place on each StateChanged.
			Action onStateChanged = () => FlowStepPresenter.RefreshButtonStates(chrome, buttonControls);
			content.StateChanged += onStateChanged;

			// Token cancellation → Cancel (and Dispose / the flow's finally closes the window).
			using var ctReg = ct.Register(() =>
				tcs.TrySetResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel)));

			try
			{
				return await tcs.Task.ConfigureAwait(false);
			}
			finally
			{
				content.StateChanged -= onStateChanged;
				// Detach this step's dismiss resolver so a later OnClosed (or Dispose) does not try to
				// resolve an already-completed step (TrySetResult would no-op anyway, but keep it tidy).
				_cancelCurrentStep = null;
				// NOTE: do NOT close the window here — it is reused across steps. The window is closed on
				// Dispose / dismissal / token-cancel only.
			}
		}

		private Window EnsureWindow(FlowChrome chrome)
		{
			lock (_gate)
			{
				if (_window != null)
					return _window;

				var builder = new WindowBuilder(_ws)
					.WithTitle(FlowChromeFormat.FormatTitle(chrome))
					.WithSize(chrome.WidthHint ?? 50, chrome.HeightHint ?? 12)
					.Centered()
					.AsModal()
					.Resizable(chrome.Resizable)
					.Minimizable(false)
					.Maximizable(false)
					.Movable(true);

				if (_parent != null)
					builder.WithParent(_parent);

				var window = builder.Build();

				// A single OnClosed handler resolves whichever step is currently presented as Cancel.
				window.OnClosed += (_, _) => _cancelCurrentStep?.Invoke();

				_window = window;
				_ws.AddWindow(window);
				return window;
			}
		}

		/// <summary>
		/// Swaps the reused window's content to <paramref name="content"/>'s step surface: clears the old
		/// controls, re-titles the window, and adds the canonical three-band layout. Mirrors
		/// <see cref="ModalWindowHost"/>'s surface assembly so a future <c>FlowControl</c> is a clean lift.
		/// </summary>
		private static void SetWindowSurface<TResult>(
			Window window,
			IFlowStepContent<TResult> content,
			FlowChrome chrome,
			IWindowControl body,
			List<(ButtonControl Control, FlowButton Spec)> buttonControls)
		{
			window.ClearControls();
			window.Title = FlowChromeFormat.FormatTitle(chrome);

			// Top band is ALWAYS host-built from chrome (title + accent rule), so every step shows a
			// consistent banner. Content that supplies its own bottom band (the framework primitives) owns
			// its ruler+buttons; otherwise the host builds the bottom band from chrome.Buttons.
			foreach (var c in FlowContentHelpers.BuildTopBand(chrome))
				window.AddControl(c);

			window.AddControl(FlowContentHelpers.WrapBody(body));

			if (content is IFlowChromeBands bands)
			{
				foreach (var c in bands.BuildBottomBand(chrome))
					window.AddControl(c);
			}
			else
			{
				if (buttonControls.Count > 0)
				{
					// Bottom band: sticky ruler + sticky right-aligned toolbar holding the host buttons.
					window.AddControl(Ctl.RuleBuilder().WithColorRole(ColorRole.Primary).StickyBottom().Build());

					var toolbarBuilder = Ctl.Toolbar()
						.WithAlignment(SharpConsoleUI.Layout.HorizontalAlignment.Right)
						.StickyBottom();
					foreach (var (control, _) in buttonControls)
						toolbarBuilder.AddButton(control);
					window.AddControl(toolbarBuilder.Build());
				}
			}
		}

		/// <summary>
		/// Closes the reused window (idempotent, marshalled to the UI thread). Call this when the flow is
		/// done so the seamless host does not leak its window. <see cref="Flow.Run{T}"/> and the wizard
		/// <c>Run</c> invoke this automatically in their <c>finally</c> when the host is the seamless host.
		/// </summary>
		public void Dispose()
		{
			Window? window;
			lock (_gate)
			{
				if (_disposed)
					return;
				_disposed = true;
				window = _window;
				_window = null;
				_cancelCurrentStep = null;
			}

			if (window != null)
				_ws.EnqueueOnUIThread(() => window.Close());
		}
	}
}
