// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Core;
using SharpConsoleUI.Flows;

namespace SharpConsoleUI.Dialogs
{
	/// <summary>
	/// Framework-owned primitive dialogs: Confirm, Prompt, and Progress.
	/// These are the missing <c>MessageBox</c> layer for SharpConsoleUI — standalone, themed,
	/// typed, and usable outside any flow composition.
	/// </summary>
	public static class Dialogs
	{
		/// <summary>
		/// Shows a modal confirmation dialog and returns <c>true</c> when the user clicks OK,
		/// or <c>false</c> when they click Cancel or dismiss the dialog.
		/// </summary>
		/// <param name="windowSystem">The window system to host the dialog in.</param>
		/// <param name="title">Title displayed in the dialog window chrome.</param>
		/// <param name="message">The confirmation question or statement to display.</param>
		/// <param name="ok">Label for the confirm (OK) button. Defaults to <c>"OK"</c>.</param>
		/// <param name="cancel">Label for the dismiss (Cancel) button. Defaults to <c>"Cancel"</c>.</param>
		/// <param name="severity">
		/// Severity level that controls the glyph, accent rule color, window border tint, and focused-button role.
		/// Defaults to <see cref="NotificationSeverityEnum.Info"/>.
		/// </param>
		/// <param name="parent">
		/// Optional parent window. When provided the dialog is modal to that window only.
		/// </param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> that completes with <c>true</c> (OK) or <c>false</c> (Cancel/dismiss).
		/// </returns>
		public static Task<bool> ConfirmAsync(
			ConsoleWindowSystem windowSystem,
			string title,
			string message,
			string ok = "OK",
			string cancel = "Cancel",
			NotificationSeverityEnum severity = NotificationSeverityEnum.Info,
			Window? parent = null)
		{
			var content = new ConfirmContent(message, ok, cancel, severity);
			var chrome = new FlowChrome(title, widthHint: 50, heightHint: 11, severity: severity);

			ShowContentModal(
				windowSystem,
				FlowContentHelpers.BuildTopBand(chrome),
				content.BuildContent(chrome),
				content.BuildBottomBand(chrome),
				chrome,
				parent,
				onDismiss: content.CancelFromDismiss,
				completion: content.Result);

			return content.Result;
		}

		/// <summary>
		/// Shows a modal prompt dialog with a single-line text input and returns the entered text
		/// when the user presses Enter or clicks OK, or <c>null</c> when they click Cancel or dismiss.
		/// </summary>
		/// <param name="windowSystem">The window system to host the dialog in.</param>
		/// <param name="title">Title displayed in the dialog window chrome.</param>
		/// <param name="message">The prompt question or label displayed above the input field.</param>
		/// <param name="initial">Optional initial text pre-filled into the input. Defaults to <c>null</c> (empty).</param>
		/// <param name="severity">
		/// Severity level that controls the glyph, accent rule color, window border tint, and focused-button role.
		/// Defaults to <see cref="NotificationSeverityEnum.Info"/>.
		/// </param>
		/// <param name="parent">
		/// Optional parent window. When provided the dialog is modal to that window only.
		/// </param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> that completes with the entered text (OK/Enter) or
		/// <c>null</c> (Cancel/dismiss).
		/// </returns>
		public static Task<string?> PromptAsync(
			ConsoleWindowSystem windowSystem,
			string title,
			string message,
			string? initial = null,
			NotificationSeverityEnum severity = NotificationSeverityEnum.Info,
			Window? parent = null)
		{
			var content = new PromptContent(message, initial, severity);
			var chrome = new FlowChrome(title, widthHint: 50, heightHint: 11, severity: severity);

			ShowContentModal(
				windowSystem,
				FlowContentHelpers.BuildTopBand(chrome),
				content.BuildContent(chrome),
				content.BuildBottomBand(chrome),
				chrome,
				parent,
				onDismiss: content.CancelFromDismiss,
				completion: content.Completion);

			return content.Completion;
		}

		/// <summary>
		/// Shows a modal progress dialog that runs <paramref name="work"/> on a background thread
		/// while displaying a live status line. Returns the work's result when it completes,
		/// <c>default(T)</c> when cancelled (user clicks Cancel or dismisses), or re-throws
		/// if the work throws.
		/// </summary>
		/// <typeparam name="T">The type produced by the work function.</typeparam>
		/// <param name="windowSystem">The window system to host the dialog in.</param>
		/// <param name="title">Title displayed in the dialog window chrome.</param>
		/// <param name="description">Initial status text shown below the progress rule.</param>
		/// <param name="work">
		/// The async work to run. Receives a <see cref="CancellationToken"/> (honoured by Cancel/dismiss)
		/// and an <see cref="IProgress{T}">IProgress&lt;string&gt;</see> that updates the status line.
		/// </param>
		/// <param name="parent">
		/// Optional parent window. When provided the dialog is modal to that window only.
		/// </param>
		/// <returns>
		/// The work's result on success; <c>default(T)</c> on cancellation; thrown on exception.
		/// </returns>
		public static async Task<T?> RunWithProgressAsync<T>(
			ConsoleWindowSystem windowSystem,
			string title,
			string description,
			Func<CancellationToken, IProgress<string>, Task<T>> work,
			Window? parent = null)
		{
			var content = new ProgressContent<T>(windowSystem, description, work);
			// useProgressGlyph: the host top-band builder renders the ⟳ spinner on a Primary rule, which the
			// NotificationSeverityEnum set does not model.
			var chrome = new FlowChrome(
				title,
				stepIndicator: null,
				widthHint: 54,
				heightHint: 9,
				buttons: null,
				refreshButtons: null,
				severity: NotificationSeverityEnum.None,
				useProgressGlyph: true);

			ShowContentModal(
				windowSystem,
				FlowContentHelpers.BuildTopBand(chrome),
				content.BuildContent(chrome),
				content.BuildBottomBand(chrome),
				chrome,
				parent,
				onDismiss: content.CancelFromDismiss,
				completion: content.Completion);

			return await content.Completion.ConfigureAwait(false);
		}

		/// <summary>
		/// Shared modal-window plumbing used by all primitive dialogs.
		/// Builds and shows a modal window hosting the canonical three-band layout — the StickyTop
		/// <paramref name="topBand"/>, the scrollable <paramref name="body"/>, and the StickyBottom
		/// <paramref name="bottomBand"/> — added as WINDOW children so the window's content layout
		/// honours their <c>StickyPosition</c> (a ScrollablePanel does not). Wires
		/// <c>OnClosed</c> → <paramref name="onDismiss"/> (so Esc/close resolves the content), and
		/// closes the window automatically when <paramref name="completion"/> finishes.
		/// </summary>
		internal static void ShowContentModal(
			ConsoleWindowSystem ws,
			IReadOnlyList<Controls.IWindowControl> topBand,
			Controls.IWindowControl body,
			IReadOnlyList<Controls.IWindowControl> bottomBand,
			FlowChrome chrome,
			Window? parent,
			System.Action onDismiss,
			Task completion)
		{
			var builder = new WindowBuilder(ws)
				.WithTitle(FormatTitle(chrome))
				.WithSize(chrome.WidthHint ?? 50, chrome.HeightHint ?? 12)
				.Centered()
				.AsModal()
				.Resizable(false)
				.Minimizable(false)
				.Maximizable(false)
				.Movable(true);

			// Tint the dialog frame by the step role (the same role the top band uses), resolved against
			// the active theme — so a standalone Dialogs.* dialog reflects its severity on the border just
			// like the in-flow / inline host paths do. Active = role border; inactive = dimmed variant.
			var (activeBorder, inactiveBorder) = FlowContentHelpers.ResolveBorderColors(chrome, ws.Theme);
			builder.WithActiveBorderColor(activeBorder).WithInactiveBorderColor(inactiveBorder);

			// Three-band assembly: StickyTop band, scrollable Fill body, StickyBottom band. The body is
			// wrapped in a Fill scroll viewport (no-op when it already scrolls, e.g. the primitives) so a
			// tall body scrolls inside the bounded window — matching the host paths.
			foreach (var c in topBand)
				builder.AddControl(c);
			builder.AddControl(FlowContentHelpers.WrapBody(body));
			foreach (var c in bottomBand)
				builder.AddControl(c);

			if (parent != null)
				builder.WithParent(parent);

			var modal = builder.Build();

			// When the modal is dismissed by the user (Esc / title-bar close), resolve the content.
			modal.OnClosed += (_, _) => onDismiss();

			// When the content resolves (button click), close the modal on the UI thread.
			completion.ContinueWith(
				_ => ws.EnqueueOnUIThread(() => modal.Close()),
				System.Threading.CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);

			ws.AddWindow(modal);
		}

		internal static string FormatTitle(FlowChrome chrome) => FlowChromeFormat.FormatTitle(chrome);
	}
}
