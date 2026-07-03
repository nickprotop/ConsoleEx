# ChatTranscriptControl

A chat/agent transcript control that displays an ordered list of role-tagged messages with per-role styling, token-by-token streaming, collapsible verbose messages, and a thinking indicator.

## Overview

`ChatTranscriptControl` is an honest composition: it subclasses `ScrollablePanelControl` and hosts one real `CollapsiblePanel` per message, each containing a `MarkupControl` body (or a `SpinnerControl` while a message is "thinking"). Scrolling, word-wrap, markdown rendering, selection, and collapse animation are all provided by those child controls — none of it is re-implemented.

Each message is addressed by an opaque `ChatMessageId` handle, so `Append`, `UpdateMessage`, and `RemoveMessage` can target **any** message by id. The default `Append(token)` overload always targets the latest message, covering the common single-stream case; `Append(id, token)` lets multiple messages stream simultaneously (multi-in-flight agents).

Auto-scroll stickiness is inherited from `ScrollablePanelControl`: new content scrolls the transcript to the bottom **only when the user is already at the bottom**. If the user scrolled up to read history, streaming tokens do not yank them back down.

Every role has a themed default style (see [Themed Defaults](#themed-defaults)); the control looks correct with zero configuration.

See also: [ScrollablePanelControl](ScrollablePanelControl.md), [CollapsiblePanel](CollapsiblePanel.md), [MarkupControl](MarkupControl.md), [SpinnerControl](SpinnerControl.md)

## Quick Start

```csharp
var chat = Controls.ChatTranscript()
    .Build();

window.AddControl(chat);

// Add a user message and an assistant reply
chat.AddMessage(ChatRole.User, "Hello — can you explain double-buffering?");

ChatMessageId replyId = chat.AddMessage(ChatRole.Assistant, string.Empty);
// Stream tokens onto the UI thread (see Thread Safety section)
chat.Append(replyId, "Sure! A compositor double-buffers ");
chat.Append(replyId, "the screen, diffs the cells, ");
chat.Append(replyId, "and flushes only what changed.");
```

## Builder API

Create a `ChatTranscriptBuilder` through the `Controls` factory:

```csharp
var builder = Controls.ChatTranscript();
```

### Builder Methods

```csharp
.AnimateMessages(bool animate = true)         // Animate collapsible message expand/collapse (default: true)
.WithAutoScroll(bool autoScroll = true)       // Auto-scroll to bottom when near bottom (default: true)
.WithRoleStyle(ChatRole role, ChatRoleStyle style) // Override the themed style for a role
.WithName(string name)                        // Name for FindControl<T>() lookups
.WithMargin(int margin)                       // Uniform margin on all four sides
.WithMargin(int left, int top, int right, int bottom)
.WithColorRole(ColorRole role, ThemeMode? mode = null)
```

### Building

```csharp
ChatTranscriptControl chat = builder.Build();

// Implicit conversion is also supported:
ChatTranscriptControl chat = Controls.ChatTranscript()
    .AnimateMessages(true);
```

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AnimateMessages` | `bool` | `true` | When `true`, collapsible message panels use `CollapsibleAnimationMode.Height` so they expand/collapse with a height tween instead of snapping. Non-collapsible panels are unaffected. Only affects messages added after the value changes. |
| `ThinkingSpinnerStyle` | `SpinnerStyle` | `SpinnerStyle.Dots` | Spinner style for thinking messages. Only affects thinking messages added after the change. |
| `MessageIds` | `IReadOnlyList<ChatMessageId>` | — | Read-only. The ids of all messages currently in the transcript, in display order. |
| `AutoScroll` | `bool` | `true` | Inherited from `ScrollablePanelControl`. When `true`, the transcript scrolls to the bottom on new content — but only while already near the bottom. |

## Message API

### Adding Messages

```csharp
ChatMessageId AddMessage(ChatRole role, string content, string? author = null, bool thinking = false)
```

Adds a message to the transcript and returns its id.

- `role` — the role of the message author (`User`, `Assistant`, `System`, `Tool`, or `Error`).
- `content` — the initial body text (rendered as Markdown when the role style's `Markdown` is `true`).
- `author` — optional name that overrides the role's default header label (e.g. the agent's display name).
- `thinking` — when `true`, the message initially shows a spinner instead of a text body. The spinner clears automatically on the first `Append` or `UpdateMessage` call for that message.

### Streaming

```csharp
void Append(string token)                    // Append to the latest message
void Append(ChatMessageId id, string token)  // Append to a specific message
```

Appends a text chunk to a message's body. If the message was in "thinking" state, the spinner is removed and replaced by a text body on the first call.

Both overloads **must run on the UI thread** (see [Thread Safety](#thread-safety)).

### Updating and Removing

```csharp
void UpdateMessage(ChatMessageId id, string content)  // Replace a message's full body
void RemoveMessage(ChatMessageId id)                  // Remove a single message (no-op if unknown)
void Clear()                                          // Remove all messages
```

`UpdateMessage` also clears a thinking indicator if the message was in thinking state.

### Querying

```csharp
IReadOnlyList<ChatMessageId> MessageIds { get; }  // All message ids in display order
ChatRole GetRole(ChatMessageId id)                // Role of a message
bool IsThinking(ChatMessageId id)                 // Whether a message is still showing its thinking spinner
```

## Role Style API

```csharp
void SetRoleStyle(ChatRole role, ChatRoleStyle style)  // Override styling for a role (affects new messages)
ChatRoleStyle GetRoleStyle(ChatRole role)              // Retrieve the current style for a role
```

Role styles take effect for messages added **after** the call. Already-added messages keep the style they were built with.

## ChatRole

```csharp
public enum ChatRole
{
    User,       // Message from the human user
    Assistant,  // Message from the AI assistant
    System,     // System-level prompt or instruction
    Tool,       // Output from a tool or function call
    Error       // Error message
}
```

## ChatMessageId

An opaque, value-typed handle to a message in the transcript. Use it to target `Append`, `UpdateMessage`, `RemoveMessage`, `GetRole`, and `IsThinking` on a specific message.

```csharp
// Store the id returned by AddMessage
ChatMessageId assistantId = chat.AddMessage(ChatRole.Assistant, string.Empty);

// Use it to target subsequent calls
chat.Append(assistantId, "First token ");
chat.Append(assistantId, "second token.");
chat.UpdateMessage(assistantId, "# Final markdown\n\nReplaces all streamed content.");
```

`ChatMessageId` is a `readonly struct` with value equality. The underlying integer is exposed as `.Value` for debugging but should not be constructed manually.

## ChatRoleStyle

Defines the visual presentation of messages for a given role. All properties are init-only; use object initializers or `with` expressions.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ColorRole` | `ColorRole` | `ColorRole.Default` | Semantic color role applied to the message panel. |
| `BorderColor` | `Color?` | `null` | Explicit border color. When `null`, derived from `ColorRole` and the active theme. |
| `HeaderStyle` | `CollapsibleHeaderStyle` | `Borderless` | Panel header/border style: `Borderless`, `Bordered`, `Rounded`, or `DoubleLine`. |
| `HeaderAlignment` | `HorizontalAlignment` | `Left` | Horizontal alignment of the header text within the header row. |
| `ShowHeader` | `bool` | `true` | Whether to render a header row for messages of this role. |
| `Collapsible` | `bool` | `false` | Whether messages of this role can be collapsed by clicking the header. |
| `StartCollapsed` | `bool` | `false` | When `Collapsible` is `true`, whether messages start in the header-only (collapsed) state. Useful for verbose roles such as `System` or `Tool`. |
| `Header` | `Func<ChatRole, string?, string>?` | `null` | Factory producing the header text from `(role, author)`. When `null`, a built-in label is used ("You", "Assistant", etc.). |
| `Margin` | `Margin` | `new(0, 0, 0, 1)` | Outer margin applied to each message panel. The default leaves one blank line below each message. |
| `Markdown` | `bool` | `true` | Whether the message body is rendered as Markdown. When `false`, content is rendered as plain text. |
| `HeaderGradient` | `(Color From, Color To)?` | `null` | Optional gradient sweep applied to the header text. When non-`null`, the header is rendered with a color transition from `From` to `To`. |
| `Background` | `Color?` | `null` | Optional background color for the message body area. Accepts colors with an alpha channel (via `Color.WithAlpha`) so the compositor blends the bubble over the window background. |

## Themed Defaults

The control ships with per-role defaults that look correct with zero configuration:

| Role | `ColorRole` | `HeaderStyle` | `Collapsible` | `StartCollapsed` | Default Header |
|------|-------------|---------------|---------------|-----------------|----------------|
| `User` | `Primary` | `Rounded` | No | — | "You" |
| `Assistant` | `Default` | `Borderless` | No | — | "Assistant" |
| `System` | `Info` | `Borderless` | Yes | Yes | "System" |
| `Tool` | `Secondary` | `Borderless` | Yes | Yes | "🔧 tool" |
| `Error` | `Danger` | `Rounded` | No | — | "Error" |

`System` and `Tool` messages start collapsed (header-only) because they are often verbose. The user can click the header to expand them; when `AnimateMessages` is `true` (the default), the expand/collapse uses a height tween instead of snapping.

## Polish Features

All four features are opt-in. Only `AnimateMessages` defaults to `true` (it is subtle and safe); everything else is off by default.

### A. Animated Expand/Collapse

`AnimateMessages = true` (default) sets each **collapsible** message panel's `CollapsibleAnimationMode` to `Height`. This means `Tool` and `System` messages expand and collapse smoothly when the user (or code) toggles them — instead of snapping open or shut. Non-collapsible messages are unaffected.

This reuses `CollapsiblePanel`'s existing animation path; it is not a new engine and has no performance cost beyond the panel's own tween.

### B. Gradient Role Headers

Set `HeaderGradient = (from, to)` on a `ChatRoleStyle` to render the header text with a color sweep. The gradient is applied to the `CollapsiblePanel`'s header via the existing `[gradient=]` markup tag, so it renders through the normal paint path at no extra cost.

```csharp
.WithRoleStyle(ChatRole.Assistant, new ChatRoleStyle
{
    HeaderGradient = (new Color(64, 224, 208), new Color(160, 120, 255)),
    Header = static (_, author) => author ?? "Assistant"
})
```

### C. Semi-Transparent Message Bubbles

Set `Background = someColor.WithAlpha(value)` on a `ChatRoleStyle` to give messages of that role a translucent bubble. The compositor blends the alpha color over whatever is behind the window (including an animated background), producing a real depth effect.

```csharp
.WithRoleStyle(ChatRole.User, new ChatRoleStyle
{
    Background = new Color(64, 96, 160).WithAlpha(160)
})
```

No new code is needed — the compositor's existing Porter-Duff `Color.Blend` handles the blend.

### D. Thinking Indicator

Pass `thinking: true` to `AddMessage` to create a message that initially shows a spinner instead of a text body. This is the right pattern for an assistant message that is created before the first token arrives.

```csharp
ChatMessageId id = chat.AddMessage(ChatRole.Assistant, string.Empty, thinking: true);
// IsThinking(id) is true; the body shows a spinner

// When the first token arrives, the spinner clears automatically:
chat.Append(id, "First token");
// IsThinking(id) is now false
```

The spinner style is set by `ThinkingSpinnerStyle` (default `SpinnerStyle.Dots`). `UpdateMessage` also clears a thinking indicator.

## Thread Safety

Streaming tokens typically arrive on a background thread. **All `ChatTranscriptControl` methods that mutate child controls (`AddMessage`, `Append`, `UpdateMessage`, `RemoveMessage`, `Clear`) must run on the UI thread.** Calling them from a background thread is a data race.

Marshal calls via `windowSystem.EnqueueOnUIThread`:

```csharp
// In a background task or async continuation:
ws.EnqueueOnUIThread(() => chat.Append(id, token));
```

`Container?.Invalidate(...)` is the only call that is thread-safe from a background thread.

## Markdown Mid-Stream

When `Markdown` is `true` (the default), each `Append` call re-renders the accumulated buffer through `MarkupControl.SetMarkdown`. Partial Markdown syntax (e.g. an incomplete table or a half-arrived `**bold**` span) degrades gracefully rather than corrupting the display — the balanced-region logic in `SetMarkdown` treats markup regions atomically.

For strict cases where a partial render is unacceptable, stream plain text and finalize with `UpdateMessage`:

```csharp
// Stream plain text
chat.Append(id, token);

// On completion, replace with the finished Markdown
chat.UpdateMessage(id, finalMarkdown);
```

## Examples

### Simple Chat

```csharp
var chat = Controls.ChatTranscript()
    .WithMargin(1)
    .Build();

window.AddControl(chat);

// User turn
chat.AddMessage(ChatRole.User, "What is a compositor?");

// Assistant reply — add the message first, then stream tokens on the UI thread
ChatMessageId replyId = chat.AddMessage(ChatRole.Assistant, string.Empty);
chat.Append(replyId, "A compositor double-buffers the screen, ");
chat.Append(replyId, "diffs the cells, and flushes only what changed.");

// A system note (starts collapsed; user can click to expand)
chat.AddMessage(ChatRole.System, "Model: SharpConsoleUI-1.0 · Context: 4096 tokens");

// An error
chat.AddMessage(ChatRole.Error, "Request timed out after 30 s. Please try again.");
```

### Streaming Agent with Thinking Indicator and Collapsible Tool Message

This example mirrors the real adoption pattern (see `ChatTranscriptDemoWindow.cs`): an agent that thinks before answering, runs a tool, and streams its final reply — all from an async window thread.

```csharp
var chat = Controls.ChatTranscript()
    .AnimateMessages(true)               // Collapsible tool messages expand/collapse smoothly
    .WithRoleStyle(ChatRole.Assistant, new ChatRoleStyle
    {
        ColorRole       = ColorRole.Default,
        HeaderStyle     = CollapsibleHeaderStyle.Borderless,
        HeaderGradient  = (new Color(64, 224, 208), new Color(160, 120, 255)),
        Header          = static (_, author) => author ?? "Assistant"
    })
    .WithRoleStyle(ChatRole.User, new ChatRoleStyle
    {
        ColorRole   = ColorRole.Primary,
        HeaderStyle = CollapsibleHeaderStyle.Rounded,
        Background  = new Color(64, 96, 160).WithAlpha(160)
    })
    .WithMargin(1, 1, 1, 1)
    .Build();

window.AddControl(chat);

// Seed the opening turn synchronously (we are on the UI thread here)
chat.AddMessage(ChatRole.User, "How does the diff engine work?");

var window = new WindowBuilder(ws)
    .WithTitle("Agent Chat")
    .WithSize(80, 30)
    .Centered()
    .AddControl(chat)
    .WithAsyncWindowThread(async (win, ct) =>
    {
        // 1. Assistant starts thinking — shows a spinner
        ChatMessageId thinkId = default;
        ws.EnqueueOnUIThread(() =>
            thinkId = chat.AddMessage(ChatRole.Assistant, string.Empty, thinking: true));
        await Task.Delay(1200, ct);

        // 2. Tool call result — starts collapsed (header-only) until the user clicks to expand
        ws.EnqueueOnUIThread(() => chat.AddMessage(
            ChatRole.Tool,
            "```\ndiff(dirty=1274 cells, skipped=3826)\nlatency=1.8ms\n```",
            author: "🔧 diff_engine"));
        win.Invalidate(Invalidation.Relayout);
        await Task.Delay(600, ct);

        // 3. Stream the final answer into the thinking message.
        //    The spinner clears automatically on the first token.
        string[] tokens = { "The diff ", "engine scans ", "changed cells, ", "groups runs, ",
                             "and emits ", "only the ", "minimal ANSI ", "sequences." };
        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            ws.EnqueueOnUIThread(() => chat.Append(thinkId, token));
            win.Invalidate(Invalidation.Relayout);
            await Task.Delay(90, ct);
        }
    })
    .BuildAndShow();
```

## Best Practices

1. **Always marshal mutations to the UI thread.** Streaming tokens arrive on background threads; wrap every `Append`, `AddMessage`, `UpdateMessage`, and `RemoveMessage` call in `windowSystem.EnqueueOnUIThread(...)`.
2. **Use `thinking: true` for latency.** Create the assistant message before the first token arrives and pass `thinking: true`. The spinner gives users instant feedback; it clears on the first `Append` automatically.
3. **Keep `AnimateMessages = true` (the default).** The collapse tween is handled by `CollapsiblePanel` at no extra cost and makes tool/system expansion feel polished.
4. **Stream plain text, finalize with `UpdateMessage` for strict Markdown.** If you need the final Markdown to render without any intermediate partial state, stream plain text tokens and call `UpdateMessage(id, finalMarkdown)` once the response is complete.
5. **Use `ChatRoleStyle.Background` with alpha for depth.** `new Color(r, g, b).WithAlpha(value)` produces a translucent bubble the compositor blends over the window background — a depth effect no other TUI framework offers.
6. **Call `SetRoleStyle` before adding messages.** Role styles affect messages added after the call; existing messages are not retroactively restyled.
7. **Use the `author` parameter for multi-agent conversations.** Pass `author: agentName` to `AddMessage` to show the individual agent name in the header instead of the generic role label.

## See Also

- [ScrollablePanelControl](ScrollablePanelControl.md) — The base class; all scroll/viewport behavior is inherited.
- [CollapsiblePanel](CollapsiblePanel.md) — Each message is a `CollapsiblePanel`; consult its docs for header/border style options.
- [MarkupControl](MarkupControl.md) — The message body; selection/copy, the `[markdown]` tag, and inline markup are all available.
- [SpinnerControl](SpinnerControl.md) — Used for the thinking indicator.
- [Markup Syntax](../MARKUP_SYNTAX.md) — Colors, decorations, the `[markdown]` tag, gradients, and spinners.

---

[Back to Controls](../CONTROLS.md) | [Back to Main Documentation](../../README.md)
