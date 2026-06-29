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
	/// The framework-default <see cref="IFlowHost"/>: each step is presented in a fresh modal window
	/// (<see cref="WindowBuilder.AsModal"/>). The window hosts the step body, a separator rule, and the
	/// standardized right-aligned button row built from <see cref="FlowChrome.Buttons"/>. The window is
	/// disposed in a <c>finally</c>, so cancel/fault never leaks a window.
	/// </summary>
	/// <remarks>
	/// <para>Resolution paths (whichever fires first wins, via <c>TrySetResult</c>):</para>
	/// <list type="bullet">
	///   <item>A host-rendered button click resolves with that button's <see cref="FlowVerdict"/>.</item>
	///   <item>The content completing its own <see cref="IFlowStepContent{TResult}.Completion"/> resolves
	///         with <see cref="FlowVerdict.Next"/> (body self-resolve).</item>
	///   <item>Token cancellation or window dismissal resolves with <see cref="FlowVerdict.Cancel"/>.</item>
	/// </list>
	/// <para>Double-button-row decision: the host renders the button row ONLY from
	/// <see cref="FlowChrome.Buttons"/>. The framework primitives build their own OK/Cancel buttons inside
	/// <c>BuildContent</c> and resolve their own <c>Completion</c>; callers present them with an empty
	/// <see cref="FlowChrome.Buttons"/> set so no second row is rendered (their standalone
	/// <c>Dialogs.*</c> path does not go through this host at all).</para>
	/// </remarks>
	public sealed class ModalWindowHost : IFlowHost
	{
		private readonly ConsoleWindowSystem _ws;
		private readonly Window? _parent;

		/// <summary>
		/// Initializes a new <see cref="ModalWindowHost"/>.
		/// </summary>
		/// <param name="ws">The window system to present steps in.</param>
		/// <param name="parent">Optional parent window; when provided each step modal is modal to it.</param>
		public ModalWindowHost(ConsoleWindowSystem ws, Window? parent)
		{
			_ws = ws ?? throw new ArgumentNullException(nameof(ws));
			_parent = parent;
		}

		/// <inheritdoc/>
		public async Task<FlowStepOutcome<TResult>> PresentAsync<TResult>(
			IFlowStepContent<TResult> content, FlowChrome chrome, CancellationToken ct)
		{
			ArgumentNullException.ThrowIfNull(content);

			var tcs = new TaskCompletionSource<FlowStepOutcome<TResult>>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			// Build the step body (called once per presentation).
			var body = content.BuildContent(chrome);

			// Build the host's standardized button row (empty when chrome.Buttons is empty).
			var buttonControls = new List<(ButtonControl Control, FlowButton Spec)>();
			FlowStepPresenter.BuildButtonControls(chrome, tcs, content, buttonControls);

			// Assemble the modal window using the canonical three-band shape. The bands are added
			// as WINDOW children so the window's content layout honours their StickyPosition (a
			// ScrollablePanel does not). Order: StickyTop band, scrollable Fill body, StickyBottom band.
			int width = chrome.WidthHint ?? 50;
			int height = FlowContentHelpers.ResolveWindowHeight(
				chrome, body, width, bandRows: 6, terminalHeight: _ws.DesktopDimensions.Height, fixedDefault: 12);

			var builder = new WindowBuilder(_ws)
				.WithTitle(FlowChromeFormat.FormatTitle(chrome))
				.WithSize(width, height)
				.Centered()
				.AsModal()
				.Resizable(chrome.Resizable)
				.Minimizable(false)
				.Maximizable(false)
				.Movable(true);

			// D3: tint the window frame by the step role (same role the top band uses), resolved against
			// the active theme. Active = role border; inactive = dimmed variant so it recedes when unfocused.
			var (activeBorder, inactiveBorder) = FlowContentHelpers.ResolveBorderColors(chrome, _ws.Theme);
			builder.WithActiveBorderColor(activeBorder).WithInactiveBorderColor(inactiveBorder);

			// Top band is ALWAYS host-built from chrome (title + accent rule), so every step — primitive,
			// plain custom content, or wizard — shows a consistent banner. Content that supplies its own
			// bottom band (the framework primitives) owns its ruler+buttons; otherwise the host builds the
			// bottom band from chrome.Buttons.
			foreach (var c in FlowContentHelpers.BuildTopBand(chrome))
				builder.AddControl(c);

			builder.AddControl(FlowContentHelpers.WrapBody(body));

			if (content is IFlowChromeBands bands)
			{
				foreach (var c in bands.BuildBottomBand(chrome))
					builder.AddControl(c);
			}
			else
			{
				if (buttonControls.Count > 0)
				{
					// Bottom band: sticky ruler + sticky right-aligned toolbar holding the host
					// buttons. Both StickyBottom so slack collapses above the ruler (consistent
					// spacing regardless of body height).
					builder.AddControl(Ctl.RuleBuilder().WithColorRole(ColorRole.Primary).StickyBottom().Build());

					var toolbarBuilder = Ctl.Toolbar()
						.WithAlignment(SharpConsoleUI.Layout.HorizontalAlignment.Right)
						.StickyBottom();
					foreach (var (control, _) in buttonControls)
						toolbarBuilder.AddButton(control);
					builder.AddControl(toolbarBuilder.Build());
				}
			}

			if (_parent != null)
				builder.WithParent(_parent);

			var modal = builder.Build();

			FlowStepPresenter.WireBodySelfResolve(content, tcs);

			// Dynamic buttons: re-evaluate enabled state in place on each StateChanged.
			Action onStateChanged = () => FlowStepPresenter.RefreshButtonStates(chrome, buttonControls);
			content.StateChanged += onStateChanged;

			// Dismissal (Esc / title-bar close) → Cancel.
			modal.OnClosed += (_, _) =>
				tcs.TrySetResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel));

			// Token cancellation → Cancel (and the finally closes the window).
			using var ctReg = ct.Register(() =>
				tcs.TrySetResult(new FlowStepOutcome<TResult>(default, FlowVerdict.Cancel)));

			_ws.AddWindow(modal);

			try
			{
				return await tcs.Task.ConfigureAwait(false);
			}
			finally
			{
				content.StateChanged -= onStateChanged;
				// Idempotent close, marshalled to the UI thread, so cancel/fault never leaks the window.
				_ws.EnqueueOnUIThread(() => modal.Close());
			}
		}

	}
}
