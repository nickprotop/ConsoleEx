using System.Text;
using AgentStudio.Models;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;

namespace AgentStudio.Services;

/// <summary>
/// Mock AI service for simulating AI responses with animations
/// </summary>
public class MockAiService
{
	// Streaming cadence: delay between appended chunks (ms).
	private const int StreamChunkDelayMs = 30;
	// Simulated network delay before each scenario message (ms).
	private const int NetworkDelayMs = 500;
	// Simulated tool-execution delay after a tool-call message (ms).
	private const int ToolExecutionDelayMs = 800;
	// How many words each streamed chunk carries (small so it visibly streams).
	private const int WordsPerChunk = 2;

	private readonly ChatTranscriptControl _transcript;
	private readonly ConsoleWindowSystem _windowSystem;

	public MockAiService(ConsoleWindowSystem windowSystem, ChatTranscriptControl transcript)
	{
		_windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
		_transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
	}

	/// <summary>
	/// Adds messages from a scenario with simulated delays and animations.
	/// Assistant replies stream token-by-token behind a native thinking spinner; other
	/// roles (and any native tool-call / findings messages) are posted whole.
	/// </summary>
	public async Task AddScenarioAsync(List<Message> scenario, CancellationToken cancellationToken = default)
	{
		foreach (var message in scenario)
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			// Simulate network delay before each message
			await Task.Delay(TimeSpan.FromMilliseconds(NetworkDelayMs), cancellationToken);

			if (message.Role == MessageRole.Assistant)
			{
				await StreamAssistantMessageAsync(message, cancellationToken);
			}
			else
			{
				PostWholeMessage(message);
			}

			// Longer delay after tool calls to simulate execution time
			if (message.ToolCall != null)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(ToolExecutionDelayMs), cancellationToken);
			}
		}
	}

	/// <summary>
	/// Streams an assistant reply into the transcript: opens the message with a native thinking
	/// spinner, then appends the content in small chunks so it visibly streams. The spinner is
	/// cleared automatically by <see cref="ChatTranscriptControl.Append(ChatMessageId, string)"/>
	/// on the first chunk. Any native tool-call / findings messages are posted whole afterwards.
	/// </summary>
	private async Task StreamAssistantMessageAsync(Message msg, CancellationToken cancellationToken)
	{
		// Start the turn with a thinking spinner. AddMessage mutates the transcript, so marshal to
		// the UI thread (this continuation may resume on a thread-pool thread — CLAUDE.md Rule 13).
		ChatMessageId id = default;
		_windowSystem.EnqueueOnUIThread(() =>
			id = _transcript.AddMessage(ChatRole.Assistant, "", thinking: true));

		foreach (var chunk in ChunkResponse(msg.Content))
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			// Capture per-iteration so the closure appends the right chunk. Append clears the
			// thinking spinner on the first token.
			var localChunk = chunk;
			_windowSystem.EnqueueOnUIThread(() => _transcript.Append(id, localChunk));
			await Task.Delay(StreamChunkDelayMs, cancellationToken);
		}

		// Native tool-call (collapsible Tool message) and findings (markdown table) rendering. These are
		// posted whole, not streamed, and are marshaled to the UI thread like any transcript mutation.
		PostToolCallAndFindings(msg);
	}

	/// <summary>
	/// Posts a non-assistant message (and any native tool-call / findings messages) whole to the
	/// transcript. All transcript mutations are marshaled to the UI thread (CLAUDE.md Rule 13).
	/// </summary>
	private void PostWholeMessage(Message msg)
	{
		var chatRole = msg.Role switch
		{
			MessageRole.User => ChatRole.User,
			MessageRole.Assistant => ChatRole.Assistant,
			_ => ChatRole.System,
		};

		_windowSystem.EnqueueOnUIThread(() => _transcript.AddMessage(chatRole, msg.Content));

		PostToolCallAndFindings(msg);
	}

	/// <summary>
	/// Emits the tool-call and findings for a message. A tool call becomes a native collapsible
	/// <see cref="ChatRole.Tool"/> message (name in the collapsed header, args/output in the body);
	/// findings become a single native <c>[markdown]</c> table message. All transcript
	/// mutations are marshaled to the UI thread (CLAUDE.md Rule 13).
	/// </summary>
	private void PostToolCallAndFindings(Message msg)
	{
		if (msg.ToolCall != null)
		{
			var toolCall = msg.ToolCall;
			var body = BuildToolBody(toolCall);
			_windowSystem.EnqueueOnUIThread(() =>
				_transcript.AddMessage(ChatRole.Tool, body, author: $"Tool Call: {toolCall.Name}"));
		}

		if (msg.Findings != null && msg.Findings.Count > 0)
		{
			var table = BuildFindingsTable(msg.Findings);
			_windowSystem.EnqueueOnUIThread(() =>
				_transcript.AddMessage(ChatRole.Assistant, table, author: "Analysis"));
		}
	}

	/// <summary>
	/// Renders the analysis findings as a single native <c>[markdown]</c> GitHub-style pipe table
	/// (Severity | Finding), which the transcript draws with box-drawing borders.
	/// The intro line ("Analysis complete. Found N…") is already posted separately as the streamed
	/// assistant message that carries these findings, so this returns just the table region. Cell
	/// text is escaped via <see cref="EscapeCell"/> so a literal <c>|</c>, <c>[</c>, <c>]</c> or
	/// newline in a finding can't break the table columns or the markup.
	/// </summary>
	private static string BuildFindingsTable(List<Finding> findings)
	{
		var sb = new StringBuilder();
		sb.AppendLine("[markdown]");
		sb.AppendLine("| Severity | Finding |");
		sb.AppendLine("|---|---|");
		foreach (var finding in findings)
		{
			var findingCell = string.IsNullOrEmpty(finding.Location)
				? finding.Title
				: $"{finding.Title} ({finding.Location})";
			sb.AppendLine($"| {EscapeCell(finding.Severity.ToString())} | {EscapeCell(findingCell)} |");
		}
		sb.Append("[/]");
		return sb.ToString();
	}

	/// <summary>
	/// Escapes a value for a markdown table cell: strips newlines, escapes markup brackets via
	/// <see cref="MarkupParser.Escape"/>, and replaces pipes so they don't split the column.
	/// </summary>
	private static string EscapeCell(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;

		var flattened = text.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|");
		return MarkupParser.Escape(flattened);
	}

	/// <summary>
	/// Formats a <see cref="ToolCall"/> as native markup for the collapsible Tool message body. The
	/// tool name surfaces in the collapsed header (via the message author); this body holds the
	/// parameters + status, output, and execution time.
	/// The parameters are escaped with <see cref="MarkupParser.Escape"/> (plain values that may contain
	/// literal brackets); the output is appended as-is because it is pre-formatted markup (intentional
	/// syntax highlighting) that must render as color rather than as literal tags.
	/// </summary>
	private static string BuildToolBody(ToolCall toolCall)
	{
		var statusMarkup = toolCall.Status switch
		{
			ToolStatus.Complete => "[green]✓ Complete[/]",
			ToolStatus.Error => "[red]✗ Error[/]",
			_ => "[yellow]⏳ Running[/]"
		};

		var sb = new StringBuilder();
		sb.Append("[grey70]args:[/] ").Append(MarkupParser.Escape(toolCall.Parameters));
		sb.Append("  |  status: ").Append(statusMarkup);

		if (!string.IsNullOrEmpty(toolCall.Output))
		{
			// Output is pre-formatted markup (syntax-highlighted code, e.g. [magenta1]…[/]). Append it
			// as-is so it renders as color — do NOT escape it.
			sb.Append('\n').Append("[grey70]output:[/]");
			sb.Append('\n').Append(toolCall.Output);
		}

		if (toolCall.ExecutionTime.HasValue)
		{
			sb.Append('\n').Append($"[grey50]Execution time: {toolCall.ExecutionTime.Value:F1}s[/]");
		}

		return sb.ToString();
	}

	/// <summary>
	/// Splits a reply into small chunks (a few words each, keeping their trailing whitespace) so it
	/// reads naturally as it streams. Whitespace is preserved so the reassembled text is identical to
	/// the source.
	/// </summary>
	private static IEnumerable<string> ChunkResponse(string text)
	{
		if (string.IsNullOrEmpty(text))
			yield break;

		var chunk = new StringBuilder();
		int wordsInChunk = 0;
		int i = 0;

		while (i < text.Length)
		{
			// Append one "word" (run of non-whitespace) plus its trailing whitespace run.
			int wordStart = i;
			while (i < text.Length && !char.IsWhiteSpace(text[i]))
				i++;
			bool hadWord = i > wordStart;
			chunk.Append(text, wordStart, i - wordStart);

			int wsStart = i;
			while (i < text.Length && char.IsWhiteSpace(text[i]))
				i++;
			chunk.Append(text, wsStart, i - wsStart);

			if (hadWord)
				wordsInChunk++;

			if (wordsInChunk >= WordsPerChunk)
			{
				yield return chunk.ToString();
				chunk.Clear();
				wordsInChunk = 0;
			}
		}

		if (chunk.Length > 0)
			yield return chunk.ToString();
	}
}
