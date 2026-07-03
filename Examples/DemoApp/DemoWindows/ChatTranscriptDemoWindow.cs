using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;

namespace DemoApp.DemoWindows;

/// <summary>
/// Showcases the <see cref="ChatTranscriptControl"/> — an agent-style chat transcript with all four
/// polish features enabled: per-role styling (a gradient Assistant header, a semi-transparent User
/// bubble), token-by-token streaming (marshalled onto the UI thread), collapsible Tool/System
/// messages that animate open/closed, and a "thinking" spinner that clears on the first token.
/// </summary>
/// <remarks>
/// The scripted conversation is driven from the window's async thread. All transcript mutations run
/// through <see cref="ConsoleWindowSystem.EnqueueOnUIThread(System.Action)"/> because the async
/// thread is a background thread and mutating child controls off the UI thread is a data race
/// (CLAUDE.md Rule 13).
/// </remarks>
internal static class ChatTranscriptDemoWindow
{
	private const int WindowWidth = 84;
	private const int WindowHeight = 30;

	// A short, scripted answer streamed a token at a time so the transcript visibly grows.
	private static readonly string[] AssistantTokens =
	{
		"Sure! ", "A ", "**compositor** ", "double-buffers ", "the ", "screen, ", "diffs ",
		"the ", "cells, ", "and ", "flushes ", "only ", "what ", "changed. ", "That ", "keeps ",
		"streaming ", "chat ", "smooth ", "even ", "as ", "tokens ", "arrive ", "one ", "by ", "one."
	};

	private static readonly string[] SummaryTokens =
	{
		"Done. ", "The ", "diff ", "engine ", "is ", "the ", "moat ", "here — ", "everything ",
		"else ", "renders ", "on ", "top ", "of ", "it."
	};

	public static Window Create(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup("[bold]Chat Transcript[/]  [dim]— role styles · streaming · collapsible tool msgs · gradient/alpha/animation[/]")
			.StickyTop()
			.WithMargin(1, 1, 1, 0)
			.Build();

		var hint = Controls.Markup("[dim]Watch the assistant stream in; click a 🔧 tool / System header to expand it | Esc: Close[/]")
			.StickyBottom()
			.WithMargin(1, 0, 1, 0)
			.Build();

		// Animate collapsible message expand/collapse.
		var chat = Controls.ChatTranscript()
			.AnimateMessages(true)
			// Assistant: a gradient header sweep (teal → violet). The control renders the gradient
			// via hex internally, so any two Colors work.
			.WithRoleStyle(ChatRole.Assistant, new ChatRoleStyle
			{
				ColorRole = ColorRole.Default,
				HeaderStyle = CollapsibleHeaderStyle.Borderless,
				HeaderGradient = (new Color(64, 224, 208), new Color(160, 120, 255)),
				Header = static (_, author) => author ?? "Assistant"
			})
			// User: a semi-transparent primary bubble the compositor blends over the window bg.
			.WithRoleStyle(ChatRole.User, new ChatRoleStyle
			{
				ColorRole = ColorRole.Primary,
				HeaderStyle = CollapsibleHeaderStyle.Rounded,
				Background = new Color(64, 96, 160).WithAlpha(160),
				Header = static (_, author) => author ?? "You"
			})
			// Tool + System keep their collapsible + start-collapsed defaults (header-only until
			// clicked); their expand/collapse animates because AnimateMessages is on.
			.WithMargin(1, 1, 1, 1)
			.Build();

		// Seed the opening turn synchronously (we're on the UI thread here).
		chat.AddMessage(ChatRole.User, "How does SharpConsoleUI keep streaming chat smooth?");

		var window = new WindowBuilder(ws)
			.WithTitle("Chat Transcript")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(header, chat, hint)
			.WithAsyncWindowThread(async (win, ct) =>
			{
				try
				{
					// 1) Assistant answers, streamed token by token.
					ChatMessageId answerId = default;
					ws.EnqueueOnUIThread(() => answerId = chat.AddMessage(ChatRole.Assistant, string.Empty));
					await Task.Delay(500, ct);

					foreach (var token in AssistantTokens)
					{
						ct.ThrowIfCancellationRequested();
						ws.EnqueueOnUIThread(() => chat.Append(answerId, token));
						win.Invalidate(Invalidation.Relayout);
						await Task.Delay(90, ct);
					}

					await Task.Delay(500, ct);

					// 2) A tool result — starts collapsed (header-only); click to expand.
					ws.EnqueueOnUIThread(() => chat.AddMessage(
						ChatRole.Tool,
						"```\nrender_frame(dirty=1274 cells, flushed=1274, skipped=3826)\nlatency=1.8ms\n```",
						author: "🔧 render_frame"));
					win.Invalidate(Invalidation.Relayout);

					await Task.Delay(700, ct);

					// 3) A "thinking" assistant message: spinner first, then it streams — the spinner
					// clears automatically on the first token.
					ChatMessageId thinkId = default;
					ws.EnqueueOnUIThread(() => thinkId = chat.AddMessage(
						ChatRole.Assistant, string.Empty, author: "Assistant", thinking: true));
					win.Invalidate(Invalidation.Relayout);

					await Task.Delay(1400, ct);

					foreach (var token in SummaryTokens)
					{
						ct.ThrowIfCancellationRequested();
						ws.EnqueueOnUIThread(() => chat.Append(thinkId, token));
						win.Invalidate(Invalidation.Relayout);
						await Task.Delay(90, ct);
					}
				}
				catch (OperationCanceledException)
				{
					// Window closed mid-stream — nothing to clean up.
				}
			})
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();

		DemoTheme.ApplyThemeGradient(window, ws);
		return window;
	}
}
