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
| `MessagesSelectable` | `bool` | `true` | Control-wide baseline for whether message text can be selected (and, since `MarkupControl.CopyEnabled` is `true` by default, copied). Acts as the master switch: setting it re-applies to existing message bodies. A per-role `ChatRoleStyle.Selectable` can override it in either direction — see below. |
| `AutoScroll` | `bool` | `true` | Inherited from `ScrollablePanelControl`. When `true`, the transcript scrolls to the bottom on new content — but only while already near the bottom. |
| `CollapsedPreview` | `bool` | `true` | When `true`, a collapsed message shows a one-line **peek** preview row directly below its header — the first line of its hidden content, faded left→right into a dim, clickable `expand…` cue. Clicking the peek row expands the message. When `false`, collapsed messages show only their header. Affects messages that collapse/expand after the value changes. |
| `CollapsedPreviewFadeWidth` | `int` | `10` | The number of trailing columns over which a collapsed-message peek row fades its preview text from opaque to transparent before the dim `expand…` cue. Affects peek rows built afterwards. |
| `MessageRailEnabled` | `bool` | `true` | When `true`, a message that has a footer shows a dim, role-tinted left rail (`│`) down its body and footer. Footer-gated: plain (no-footer) messages are unaffected. Set to `false` to disable the rail entirely. See [Message Rail](#message-rail). |
| `MessageRailGlyph` | `char` | `'│'` | The glyph painted down a railed message's left gutter (U+2502 by default). |
| `MessageRailGutterWidth` | `int` | `2` | The reserved left gutter width, in columns, for a railed message (rail glyph plus gap). The message's body, actions, and status rows are inset by this amount; the header stays flush at column 0. |
| `MessageRailColor` | `Color?` | `null` | Explicit rail color. When `null` (the default), the rail derives a dimmed version of the message's role color. |

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

## Message Actions & Status

Each message can carry a **footer** made of two optional rows: an **actions row** (a toolbar of buttons) and a **status row** (a single tinted status line). The footer is an *honest composition* — the two rows are real `ToolbarControl` / `StatusBarControl` children inserted into the transcript **as siblings of the message's `CollapsiblePanel`, not as children of it**. Because of that, **the footer survives collapsing the message body**: click to collapse a verbose tool message and its Copy / 👍 buttons and status line stay put.

### `ChatMessageAction`

A `sealed record` describing one footer button. The host owns what the action *does* (via `OnClick` / `OnClickAsync`); the control renders and dispatches it.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Id` | `string` (required) | — | Stable identifier — used for state updates, toggle restore, and removal. |
| `Label` | `string` (required) | — | Button text (markup allowed). |
| `Icon` | `string?` | `null` | Optional leading glyph, prepended to the label. |
| `Variant` | `ChatActionVariant` | `Default` | Visual variant — see below. `Toggle` makes it a stateful on/off button. |
| `IsPressed` | `bool` | `false` | For `Toggle`: the initial pressed state. |
| `Enabled` | `bool` | `true` | Whether the button is enabled. |
| `AfterPress` | `ChatActionAfterPress` | `None` | For non-toggle actions: what happens to the actions row after a press. |
| `Group` | `string?` | `null` | Optional grouping key; a separator is drawn between adjacent groups. |
| `OnClick` | `Action<ChatActionContext>?` | `null` | Synchronous click handler. |
| `OnClickAsync` | `Func<ChatActionContext, Task>?` | `null` | Asynchronous click handler (fire-and-forget; the host owns awaiting/marshaling). |

**`ChatActionVariant`** — `Default` (neutral), `Primary` (accent, the recommended action), `Danger` (destructive, e.g. delete/stop), `Toggle` (stateful on/off — the control tracks the pressed state, flips it on click, restyles the button to an accent role when pressed, and raises `ActionToggled`).

**`ChatActionAfterPress`** — `None` leaves the actions row in place; `Hide` removes the actions row after a non-toggle press (ignored for `Toggle`, which stays so it can be flipped back).

**Groups** — set `Group` on several actions to draw a separator between adjacent groups (e.g. a "Copy | Retry" group and a "👍 👎" reaction group).

**`ChatActionContext`** — the argument passed to a handler. It exposes `MessageId`, `Action`, and three helpers: `SetStatus(text, severity?)` (update this message's status row), `HideActions()` (remove the actions row), and `SetPressed(bool)` (drive a toggle from inside its own handler).

### Reactive API

All of these must run on the UI thread (like the other mutators); background callers marshal via `windowSystem.EnqueueOnUIThread(...)`.

```csharp
void SetActions(ChatMessageId id, IEnumerable<ChatMessageAction> actions)  // Replace the whole actions row (empty clears it)
void AddAction(ChatMessageId id, ChatMessageAction action)                 // Append one action (creates the row if needed)
void RemoveAction(ChatMessageId id, string actionId)                       // Remove action(s) by id (removes the row if it empties)
void ClearActions(ChatMessageId id)                                        // Remove the actions row entirely
void SetActionEnabled(ChatMessageId id, string actionId, bool enabled)     // Enable/disable an action in place
void SetActionState(ChatMessageId id, string actionId, bool pressed)       // Set a Toggle action's pressed state (does not run OnClick)

void SetStatus(ChatMessageId id, string text, NotificationSeverity? severity = null)  // Set/replace the status row
void SetStatus(ChatMessageId id, ChatMessageStatus status)                            // Full status (text + severity + left/center/right items)
void ClearStatus(ChatMessageId id)                                                    // Remove the status row
```

The status row is a non-sticky, transparent, borderless `StatusBarControl`. Its severity tints the text: `Success` → green, `Warning` → yellow, `Danger` → red, others → theme default. A `ChatMessageStatus` also accepts optional `Left` / `Center` / `Right` `StatusBarItem` lists for richer status bars.

### Seeding actions at creation

```csharp
ChatMessageId AddMessage(ChatRole role, string content, string? author,
    IEnumerable<ChatMessageAction>? actions, ChatMessageStatus? status)
```

This overload seeds a message's footer at creation. When `actions` is non-`null` it overrides the role's `ChatRoleStyle.DefaultActions`; when `null` the role defaults are kept. When `status` is non-`null` the status row is set.

Alternatively, set `ChatRoleStyle.DefaultActions` so **every** message of a role gets the same footer buttons automatically (e.g. every assistant reply gets Copy / Retry).

### Events

```csharp
event EventHandler<ChatActionEventArgs>? ActionInvoked;         // A non-toggle action was dispatched (its handler ran)
event EventHandler<ChatActionToggledEventArgs>? ActionToggled;  // A Toggle action's pressed state changed
```

`ChatActionEventArgs` carries `MessageId` and `Action`. `ChatActionToggledEventArgs` (derived) adds `IsPressed` — the new pressed state. `ActionToggled` fires whether the change came from a click, a handler's `SetPressed`, or a programmatic `SetActionState`.

### Example — assistant reply with Copy / Retry and a 👍 toggle

```csharp
var id = chat.AddMessage(ChatRole.Assistant, "Here's the summary you asked for.");

chat.SetActions(id, new[]
{
    new ChatMessageAction { Id = "copy",  Label = "Copy",  Group = "ops",
        OnClick = ctx => { Clipboard.Set(...); ctx.SetStatus("Copied", NotificationSeverity.Success); } },
    new ChatMessageAction { Id = "retry", Label = "Retry", Group = "ops",
        OnClick = ctx => RegenerateReply(ctx.MessageId) },
    new ChatMessageAction { Id = "like",  Label = "👍",     Group = "react",
        Variant = ChatActionVariant.Toggle,
        OnClick = ctx => RecordVote(ctx.MessageId, ctx.Action.IsPressed) },
});

chat.SetStatus(id, "generated in 1.8 s", NotificationSeverity.Success);

chat.ActionInvoked += (_, e) => Log($"action {e.Action.Id} on {e.MessageId}");
chat.ActionToggled += (_, e) => Log($"{e.Action.Id} -> {e.IsPressed}");
```

> **The footer survives collapse.** Because the actions and status rows are siblings of the message panel (not children), collapsing a verbose Tool/System message keeps its footer visible — the buttons and status line do not disappear with the body. This is verified end-to-end by `ChatMessageFooterRealThingTest`.

## Collapsed Preview & Footer Spacing

Two small polish features keep a collapsed transcript legible and evenly spaced.

**Collapsed peek preview.** When `CollapsedPreview` is `true` (the default), a collapsed message shows a one-line **peek** row directly below its header: the first line of its hidden content, faded left→right into a dim, clickable `expand…` cue. Clicking the peek row expands the message. The peek is a real child `MarkupControl` inserted as a sibling of the message panel — it is added and removed automatically as the panel collapses and expands, and it survives a re-render. Set `CollapsedPreview = false` to show only the header for collapsed messages. `CollapsedPreviewFadeWidth` (default `10`) controls how many trailing columns the preview text fades over before the cue. Both properties affect peek rows built after the value changes.

**Footer spacer.** A message that carries a footer (an actions row and/or a status row) gets a 1-line trailing spacer on its bottommost footer row, so a single blank line separates a footer'd message from the next one. The spacer self-corrects as rows come and go.

Both features are verified end-to-end together by `ChatMessagePeekRealThingTest` (peek shown when collapsed → survives re-render → removed on expand → the footer spacer coexists).

## Message Rail

A message that carries a footer (an actions row and/or a status row) shows a dim, role-tinted left rail (`│`) down its body and footer, bracketing the message together with its actions. The header stays flush at column 0 — the rail begins one header-height below the panel top and runs to the bottom of the last footer row. The rail is **footer-gated**: a plain message with no footer shows no rail. It is **on by default** (`MessageRailEnabled = true`); set `MessageRailEnabled = false` to disable it. The rail survives the collapse/expand transition and re-computes its span from the live layout — when a collapsible Tool/System message is collapsed the rail shrinks to just the footer rows, and expanding it extends the rail back over the body (verified end-to-end by `ChatMessageRailRealThingTest`). `MessageRailGlyph`, `MessageRailGutterWidth`, and `MessageRailColor` tune the glyph, gutter inset, and color.

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
| `Selectable` | `bool?` | `null` | Per-role override for text selection. `null` inherits the control's `MessagesSelectable` baseline; `true` forces selection **on** for this role even when the master is off; `false` forces it **off** even when the master is on. A body resolves to `role.Selectable ?? MessagesSelectable`, so the two compose symmetrically (e.g. master off but `Assistant`/`Tool` output still selectable). |
| `HeaderGradient` | `(Color From, Color To)?` | `null` | Optional gradient sweep applied to the header text. When non-`null`, the header is rendered with a color transition from `From` to `To`. |
| `Background` | `Color?` | `null` | Optional background color for the message body area. Accepts colors with an alpha channel (via `Color.WithAlpha`) so the compositor blends the bubble over the window background. |
| `DefaultActions` | `IReadOnlyList<ChatMessageAction>?` | `null` | Footer actions seeded on every message of this role at creation. `null` (the default) adds no actions. An explicit `AddMessage(..., actions:, ...)` overload overrides these per message. See [Message Actions & Status](#message-actions--status). |

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

You don't need to invalidate or repaint after these calls — mutating a message self-invalidates through the reactive framework, so the transcript redraws on the next frame. (Should you ever need to force a redraw from a background thread directly, `Container?.Invalidate(...)` is the one call that is safe to make off the UI thread.)

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
        await Task.Delay(600, ct);

        // 3. Stream the final answer into the thinking message.
        //    The spinner clears automatically on the first token.
        string[] tokens = { "The diff ", "engine scans ", "changed cells, ", "groups runs, ",
                             "and emits ", "only the ", "minimal ANSI ", "sequences." };
        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            ws.EnqueueOnUIThread(() => chat.Append(thinkId, token));
            await Task.Delay(90, ct);
        }
    })
    .BuildAndShow();
```

> **Dropping the marshalling.** The `EnqueueOnUIThread` wrappers above are required because the async window thread runs on a background `Task` by default, so its mutations must be marshalled to the UI thread. If you opt into `ConsoleWindowSystemOptions.InstallSynchronizationContext = true`, every `await` inside the window thread resumes **on the UI thread** — so any mutation that runs *after* an `await` can be called directly, no wrapper:
>
> ```csharp
> // With InstallSynchronizationContext = true:
> await Task.Delay(1200, ct);
> chat.AddMessage(ChatRole.Tool, "…", author: "🔧 diff_engine");   // safe — resumed on the UI thread
> ```
>
> One caveat: code *before the first `await`* still runs on the background thread the `Task` started on, so seed the opening message synchronously (before the window is built, as done above) rather than as the delegate's first line. `InstallSynchronizationContext` is a global, opt-in setting — leave it off if any of your synchronous input/click handlers block on async (`.Result` / `.GetAwaiter().GetResult()`), which would deadlock under it. See [Threading & Async](../THREADING_AND_ASYNC.md).

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
