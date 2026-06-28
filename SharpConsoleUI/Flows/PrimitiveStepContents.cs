// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Shared helpers used by all primitive flow-step content classes.
	/// </summary>
	internal static class FlowContentHelpers
	{
		/// <summary>Maps a <see cref="NotificationSeverityEnum"/> to the appropriate <see cref="ColorRole"/>.</summary>
		internal static ColorRole SeverityToRole(NotificationSeverityEnum severity) => severity switch
		{
			NotificationSeverityEnum.Success => ColorRole.Success,
			NotificationSeverityEnum.Warning => ColorRole.Warning,
			NotificationSeverityEnum.Danger => ColorRole.Danger,
			_ => ColorRole.Primary,
		};

		/// <summary>
		/// Selects the <see cref="ColorRole"/> the host frame should use for a step: the same role the top
		/// band uses, so frame and banner never disagree. Progress steps force <see cref="ColorRole.Primary"/>.
		/// </summary>
		internal static ColorRole BorderRole(FlowChrome chrome)
			=> chrome.UseProgressGlyph ? ColorRole.Primary : SeverityToRole(chrome.Severity);

		/// <summary>
		/// Resolves the active and inactive border colors for a flow window from the step's role against the
		/// active theme. Active is the role's resolved border; inactive is a dimmed (shaded) variant so the
		/// severity identity persists but recedes when the window is unfocused — mirroring the theme's
		/// active/inactive border convention.
		/// </summary>
		/// <param name="chrome">The step chrome carrying severity / progress flag.</param>
		/// <param name="theme">The active theme to resolve the role against.</param>
		/// <returns>A tuple of (active, inactive) border colors.</returns>
		internal static (Color Active, Color Inactive) ResolveBorderColors(FlowChrome chrome, ITheme theme)
		{
			Color active = ColorRoleResolver.Resolve(BorderRole(chrome), theme).Border;
			Color inactive = active.Shade(ControlDefaults.FlowInactiveBorderShade);
			return (active, inactive);
		}

		/// <summary>Returns the display glyph string for the given <see cref="NotificationSeverityEnum"/>.</summary>
		internal static string SeverityToGlyph(NotificationSeverityEnum severity) => severity switch
		{
			NotificationSeverityEnum.Info => ControlDefaults.FlowGlyphInfo,
			NotificationSeverityEnum.Success => ControlDefaults.FlowGlyphSuccess,
			NotificationSeverityEnum.Warning => ControlDefaults.FlowGlyphWarning,
			NotificationSeverityEnum.Danger => ControlDefaults.FlowGlyphDanger,
			_ => ControlDefaults.FlowGlyphNone,
		};

		/// <summary>Escapes markup bracket characters so text is rendered literally.</summary>
		internal static string EscapeMarkup(string text)
			=> text.Replace("[", "[[").Replace("]", "]]");

		// --- Standardized dialog layout (canonical three-band shape) -----------------------------
		// Every flow dialog (primitives AND host steps) uses ONE layout, assembled from these
		// helpers and added as WINDOW children so the window's content layout honours sticky:
		//   StickyTop    : severity banner (glyph + title) + accent rule  [BuildTopBand — HOST-built]
		//   Scrollable   : the message/input/status body (Fill)           [BuildScrollableBody / caller]
		//   StickyBottom : ruler + right-aligned button toolbar           [BuildBottomBand]
		// A ScrollablePanel does NOT honour StickyPosition, so the bands must be window children.

		/// <summary>The stable name of the host-built top-band title markup (used by tests to locate the banner).</summary>
		internal const string TopBandTitleName = "flow-top-band-title";

		/// <summary>
		/// The single source of the top band the HOST always builds: a title banner line followed by an
		/// accent rule, derived entirely from <paramref name="chrome"/>. The glyph and rule role come from
		/// <see cref="FlowChrome.Severity"/> (<see cref="NotificationSeverityEnum.None"/> → no glyph, Primary
		/// rule); the progress dialog path sets <see cref="FlowChrome.UseProgressGlyph"/> to request the ⟳
		/// spinner on a Primary rule. The title text includes the step indicator when present.
		/// </summary>
		internal static IReadOnlyList<IWindowControl> BuildTopBand(FlowChrome chrome)
		{
			if (chrome.UseProgressGlyph)
				return BuildTopBand(chrome, ColorRole.Primary, ControlDefaults.FlowGlyphProgress);

			return BuildTopBand(chrome, SeverityToRole(chrome.Severity), SeverityToGlyph(chrome.Severity));
		}

		/// <summary>StickyTop band with an explicit glyph + role (e.g. the progress spinner ⟳ on a Primary rule).</summary>
		internal static IReadOnlyList<IWindowControl> BuildTopBand(FlowChrome chrome, ColorRole role, string glyph)
		{
			var band = new List<IWindowControl>();

			var title = FlowChromeFormat.FormatTitle(chrome);
			if (!string.IsNullOrEmpty(title))
			{
				var bannerLine = string.IsNullOrEmpty(glyph)
					? $"[bold]{EscapeMarkup(title)}[/]"
					: $"{glyph}  [bold]{EscapeMarkup(title)}[/]";

				band.Add(Ctl.Markup()
					.WithName(TopBandTitleName)
					.AddLine(bannerLine)
					.WithMargin(1, 1, 1, 0)
					.WithStickyPosition(StickyPosition.Top)
					.Build());
			}

			band.Add(Ctl.RuleBuilder()
				.WithColorRole(role)
				.StickyTop()
				.Build());

			return band;
		}

		/// <summary>Scrollable middle band: a single-line message body that fills the available height.</summary>
		internal static IWindowControl BuildScrollableBody(string message)
			=> Ctl.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(Ctl.Markup()
					.AddLine(EscapeMarkup(message))
					.WithMargin(1, 1, 1, 1)
					.Build())
				.Build();

		/// <summary>
		/// Wraps a step body in a Fill, auto-scrolling <see cref="ScrollablePanelControl"/> so a tall body
		/// scrolls and shows a scrollbar inside the host's bounded body slot. A body that is already a
		/// <see cref="ScrollablePanelControl"/> (the framework primitives) is returned unchanged to avoid
		/// double-wrapping. The wrapper fills the slot between the sticky top and bottom bands: short content
		/// sits at the top with the scrollbar hidden; tall content overflows and shows the bar.
		/// </summary>
		/// <param name="body">The control returned by <see cref="IFlowStepContent{TResult}.BuildContent"/>.</param>
		/// <returns>The body unchanged if it already scrolls, otherwise a Fill scroll viewport containing it.</returns>
		internal static IWindowControl WrapBody(IWindowControl body)
		{
			if (body is ScrollablePanelControl)
				return body;

			return Ctl.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(body)
				.Build();
		}

		/// <summary>StickyBottom band: an accent ruler followed by a right-aligned toolbar of the given buttons.</summary>
		internal static IReadOnlyList<IWindowControl> BuildBottomBand(ColorRole role, params ButtonControl[] buttons)
		{
			var toolbar = Ctl.Toolbar()
				.WithAlignment(SharpConsoleUI.Layout.HorizontalAlignment.Right)
				.StickyBottom();
			foreach (var b in buttons)
				toolbar.AddButton(b);

			return new IWindowControl[]
			{
				Ctl.RuleBuilder().WithColorRole(role).StickyBottom().Build(),
				toolbar.Build(),
			};
		}
	}

	/// <summary>
	/// A confirm-dialog body: displays a message with OK and Cancel buttons.
	/// Completes <c>true</c> when the user clicks OK, <c>false</c> on Cancel or dismiss.
	/// </summary>
	internal sealed class ConfirmContent : IFlowStepContent<bool>, IFlowChromeBands
	{
		private readonly TaskCompletionSource<bool> _tcs = new();
		private readonly string _message;
		private readonly string _okLabel;
		private readonly string _cancelLabel;
		private readonly NotificationSeverityEnum _severity;

		/// <summary>
		/// Initializes a new <see cref="ConfirmContent"/>.
		/// </summary>
		/// <param name="message">The message to display.</param>
		/// <param name="ok">Label for the OK/confirm button.</param>
		/// <param name="cancel">Label for the Cancel button.</param>
		/// <param name="severity">Severity that controls the glyph and button color role.</param>
		public ConfirmContent(
			string message,
			string ok,
			string cancel,
			NotificationSeverityEnum severity = NotificationSeverityEnum.Info)
		{
			_message = message;
			_okLabel = ok;
			_cancelLabel = cancel;
			_severity = severity;
		}

		/// <inheritdoc/>
		public Task<bool> Completion => _tcs.Task;

		/// <summary>The result task exposed to <see cref="SharpConsoleUI.Dialogs.Dialogs.ConfirmAsync"/>.</summary>
		internal Task<bool> Result => _tcs.Task;

		/// <inheritdoc/>
		public event System.Action? StateChanged;

		/// <inheritdoc/>
		/// <remarks>
		/// Returns the scrollable MESSAGE body only. The top band (title + rule) is built by the HOST from
		/// <see cref="FlowChrome"/>; this content owns only the bottom band (buttons), returned separately
		/// so the host/modal can add it as a StickyBottom WINDOW child — only the window's content layout
		/// honours StickyPosition; a ScrollablePanel does not. The canonical shape is sticky-top
		/// (host banner + accent rule) / scrollable-middle (message) / sticky-bottom (ruler + button
		/// toolbar), which keeps the chrome flush and the spacing consistent across dialogs.
		/// </remarks>
		public IWindowControl BuildContent(FlowChrome chrome)
			=> FlowContentHelpers.BuildScrollableBody(_message);

		/// <summary>StickyBottom band: ruler + right-aligned button toolbar (OK/Cancel).</summary>
		public IReadOnlyList<IWindowControl> BuildBottomBand(FlowChrome chrome)
		{
			var role = FlowContentHelpers.SeverityToRole(_severity);

			var okBtn = Ctl.Button(_okLabel)
				.WithName("flow-confirm-ok")
				.WithColorRole(role)
				.Build();
			okBtn.Click += (_, _) => _tcs.TrySetResult(true);

			var cancelBtn = Ctl.Button(_cancelLabel)
				.WithName("flow-confirm-cancel")
				.Build();
			cancelBtn.Click += (_, _) => _tcs.TrySetResult(false);

			return FlowContentHelpers.BuildBottomBand(role, okBtn, cancelBtn);
		}

		/// <summary>Resolves the content as cancelled (false) when the host window is dismissed.</summary>
		internal void CancelFromDismiss() => _tcs.TrySetResult(false);
	}

	/// <summary>
	/// A prompt-dialog body: displays a message with a single-line text input and OK/Cancel buttons.
	/// Completes with the entered text when the user presses Enter or clicks OK,
	/// or <c>null</c> on Cancel or dismiss.
	/// </summary>
	internal sealed class PromptContent : IFlowStepContent<string>, IFlowChromeBands
	{
		private readonly TaskCompletionSource<string?> _tcs = new();
		private readonly string _message;
		private readonly string? _initial;
		private readonly NotificationSeverityEnum _severity;
		private string _currentText;

		/// <summary>
		/// Initializes a new <see cref="PromptContent"/>.
		/// </summary>
		/// <param name="message">The prompt question or label to display above the input.</param>
		/// <param name="initial">Optional initial value pre-filled into the input field.</param>
		/// <param name="severity">Severity that controls the glyph and button color role.</param>
		public PromptContent(
			string message,
			string? initial = null,
			NotificationSeverityEnum severity = NotificationSeverityEnum.Info)
		{
			_message = message;
			_initial = initial;
			_currentText = initial ?? string.Empty;
			_severity = severity;
		}

		/// <inheritdoc/>
		public Task<string?> Completion => _tcs.Task;

		/// <summary>Gets the current text in the prompt input. Updated before <see cref="StateChanged"/> fires.</summary>
		internal string CurrentText => _currentText;

		/// <inheritdoc/>
		public event System.Action? StateChanged;

		/// <inheritdoc/>
		/// <remarks>
		/// Returns the scrollable MESSAGE + INPUT body only. The top band (title + rule) is built by the
		/// HOST from <see cref="FlowChrome"/>; this content owns only the bottom band, returned separately
		/// (<see cref="BuildBottomBand"/>) so the host/modal can add it as a StickyBottom WINDOW child —
		/// only the window's content layout honours StickyPosition; a ScrollablePanel does not.
		/// </remarks>
		public IWindowControl BuildContent(FlowChrome chrome)
		{
			// Single-line text input
			int inputWidth = chrome.WidthHint is { } w ? Math.Max(10, w - 6) : 30;
			var promptCtrl = Ctl.Prompt(string.Empty)
				.WithName("flow-prompt-input")
				.WithInputWidth(inputWidth)
				.WithMargin(1, 0, 1, 1)
				.Build();

			if (_initial != null)
				promptCtrl.Input = _initial;

			// Enter commits the value
			promptCtrl.Entered += (_, text) => _tcs.TrySetResult(text);

			// InputChanged: mutate state THEN raise StateChanged (dynamic-buttons contract)
			promptCtrl.InputChanged += (_, text) =>
			{
				_currentText = text;
				StateChanged?.Invoke();
			};

			// Scrollable body: message + input, filling the window content height so the
			// StickyBottom band anchors to the true window bottom (no blank rows below it).
			return Ctl.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(Ctl.Markup()
					.AddLine(FlowContentHelpers.EscapeMarkup(_message))
					.WithMargin(1, 1, 1, 0)
					.Build())
				.AddControl(promptCtrl)
				.Build();
		}

		/// <summary>StickyBottom band: ruler + right-aligned button toolbar (OK/Cancel).</summary>
		public IReadOnlyList<IWindowControl> BuildBottomBand(FlowChrome chrome)
		{
			var role = FlowContentHelpers.SeverityToRole(_severity);

			var okBtn = Ctl.Button("OK")
				.WithName("flow-prompt-ok")
				.WithColorRole(role)
				.Build();
			okBtn.Click += (_, _) => _tcs.TrySetResult(_currentText);

			var cancelBtn = Ctl.Button("Cancel")
				.WithName("flow-prompt-cancel")
				.Build();
			cancelBtn.Click += (_, _) => _tcs.TrySetResult(null);

			return FlowContentHelpers.BuildBottomBand(role, okBtn, cancelBtn);
		}

		/// <summary>Resolves the content as cancelled (<c>null</c>) when the host window is dismissed.</summary>
		internal void CancelFromDismiss() => _tcs.TrySetResult(null);
	}

	/// <summary>
	/// A progress-dialog body: runs an async work item while displaying a live status line.
	/// Completes with the work's result on success, or <c>default(T)</c> on cancellation.
	/// Any exception from the work propagates via the <see cref="Completion"/> task.
	/// </summary>
	/// <typeparam name="T">The type produced by the work function.</typeparam>
	internal sealed class ProgressContent<T> : IFlowStepContent<T>, IFlowChromeBands
	{
		private readonly TaskCompletionSource<T?> _tcs = new();
		private readonly string _description;
		private readonly Func<CancellationToken, IProgress<string>, Task<T>> _work;
		private readonly ConsoleWindowSystem _ws;
		private readonly CancellationTokenSource _cts = new();
		private readonly NotificationSeverityEnum _severity = NotificationSeverityEnum.Info;
		private MarkupControl? _status;

		/// <summary>
		/// Initializes a new <see cref="ProgressContent{T}"/>.
		/// </summary>
		/// <param name="ws">The window system (used for UI-thread marshalling and logging).</param>
		/// <param name="description">Initial status text displayed below the accent rule.</param>
		/// <param name="work">
		/// The async work to run on a background thread. Receives a <see cref="CancellationToken"/>
		/// and an <see cref="IProgress{T}"/> that updates the status line on the UI thread.
		/// </param>
		public ProgressContent(
			ConsoleWindowSystem ws,
			string description,
			Func<CancellationToken, IProgress<string>, Task<T>> work)
		{
			_ws = ws;
			_description = description;
			_work = work;
		}

		/// <inheritdoc/>
		public Task<T?> Completion => _tcs.Task;

		/// <inheritdoc/>
		public event System.Action? StateChanged;

		/// <summary>Cancels the in-flight work and resolves <see cref="Completion"/> with <c>default(T)</c>.</summary>
		internal void CancelFromDismiss()
		{
			_cts.Cancel();
			_tcs.TrySetResult(default);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Returns the scrollable DESCRIPTION + live-status body only. The top band (⟳ spinner + title +
		/// rule) is built by the HOST from <see cref="FlowChrome"/> (the progress path sets
		/// <see cref="FlowChrome.UseProgressGlyph"/>); this content owns only the bottom band, returned
		/// separately (<see cref="BuildBottomBand"/>) so the host/modal can add it as a StickyBottom WINDOW
		/// child — only the window's content layout honours StickyPosition; a ScrollablePanel does not.
		/// </remarks>
		public IWindowControl BuildContent(FlowChrome chrome)
		{
			// Live status line fed by IProgress<string>
			_status = Ctl.Markup()
				.AddLine(FlowContentHelpers.EscapeMarkup(_description))
				.WithMargin(1, 1, 1, 1)
				.Build();

			// Progress reporter: marshals status updates to the UI thread
			var progress = new Progress<string>(msg => _ws.EnqueueOnUIThread(() =>
				_status!.SetContent(new System.Collections.Generic.List<string> { FlowContentHelpers.EscapeMarkup(msg) })));

			// Start work on a background thread
			_ = Task.Run(async () =>
			{
				try
				{
					var r = await _work(_cts.Token, progress).ConfigureAwait(false);
					// Resolve TCS directly — thread-safe; the modal close is marshalled by ShowContentModal's
					// ContinueWith → EnqueueOnUIThread hook, not here.
					_tcs.TrySetResult(r);
				}
				catch (OperationCanceledException)
				{
					_tcs.TrySetResult(default);
				}
				catch (System.Exception ex)
				{
					_ws.LogService?.LogError($"RunWithProgress work failed: {ex.Message}", ex, "Flows");
					_tcs.TrySetException(ex);
				}
			});

			// Scrollable body: description + live status, filling the window content height so the
			// StickyBottom band anchors to the true window bottom (no blank rows below it).
			return Ctl.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(_status)
				.Build();
		}

		/// <summary>StickyBottom band: ruler + right-aligned toolbar holding the Cancel button.</summary>
		public IReadOnlyList<IWindowControl> BuildBottomBand(FlowChrome chrome)
		{
			var role = FlowContentHelpers.SeverityToRole(_severity);

			var cancelBtn = Ctl.Button("Cancel")
				.WithName("flow-progress-cancel")
				.Build();
			cancelBtn.Click += (_, _) => CancelFromDismiss();

			return FlowContentHelpers.BuildBottomBand(role, cancelBtn);
		}
	}
}
