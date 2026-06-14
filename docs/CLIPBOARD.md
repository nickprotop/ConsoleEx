# Clipboard, Copy & Paste

SharpConsoleUI's clipboard is designed to work **both on a local desktop and over SSH** — a copy
inside an app running on a remote server can land on the operator's **local** clipboard. This guide
explains how copy and paste work, the remote/SSH behavior, and the configuration knobs.

## TL;DR

- **Copy** writes the system clipboard via the local tool (`xclip`/`wl-copy`/`pbcopy`/`clip.exe`)
  **and** emits an [OSC 52](#osc-52-the-remote-clipboard-escape) escape so the text also reaches the
  **local** clipboard over SSH. Belt-and-braces — it works whether the session is local or remote.
- **Paste** uses the terminal's own paste. [Bracketed paste](#bracketed-paste) is enabled so a
  multi-line paste inserts as one atomic block (no runaway auto-indent), and `Ctrl+V` reads the
  clipboard for local sessions.
- **Backward compatible:** the local-tool path is unchanged; everything new is additive and on by
  default in a way that never breaks existing local behavior.

## The `ClipboardHelper` API

All copy/paste flows through `SharpConsoleUI.Helpers.ClipboardHelper`:

```csharp
ClipboardHelper.SetText("hello");      // copy (local tool + OSC 52)
string text = ClipboardHelper.GetText(); // read back (local tool / in-process buffer)
```

`SetText` never throws — clipboard operations are best-effort.

## How copy works

`SetText` does two things, in order:

1. **Emit OSC 52** (when enabled — see below) through the terminal's output stream. This is what
   reaches the **local** clipboard over SSH.
2. **Run the local clipboard tool** (unchanged from earlier versions): `clip.exe` (Windows),
   `pbcopy` (macOS), `wl-copy` / `xclip` / `xsel` (Linux), or an in-process buffer when none is
   found.

Both run on every copy. On a local desktop the OSC 52 is harmless (the terminal either honors it —
setting the same clipboard the local tool just set — or silently ignores an unknown escape). Over
SSH the local tool typically finds no clipboard on the (often headless) server, and OSC 52 carries
the text to your machine.

> OSC 52 is only emitted when running inside a live `ConsoleWindowSystem` (the console driver
> registers the emitter at startup). In tests or non-driver contexts no escape is written.

> Both paths transmit text in a Unicode-correct encoding regardless of the OS console's default
> code page, so non-ASCII content — CJK, accented Latin, Cyrillic, emoji — copies without
> corruption. OSC 52 base64-encodes **UTF-8** bytes. The local clipboard tool gets the encoding it
> expects: the POSIX tools (`xclip`/`wl-copy`/`pbcopy`/`xsel`) receive **UTF-8** on stdin, while
> Windows `clip.exe` receives **UTF-16LE with a BOM** — `clip.exe` reads BOM-less stdin as the
> legacy OEM/ANSI code page, so forcing UTF-8 into it would garble non-Latin-1 text.

### OSC 52: the remote-clipboard escape

[OSC 52](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html#h3-Operating-System-Commands) is a
terminal escape sequence (`ESC ] 52 ; c ; <base64> BEL`) that asks the **terminal** — i.e. the
program on your local machine — to set its clipboard. Because the terminal runs locally, this is the
mechanism that crosses an SSH boundary. Most modern terminals support it (iTerm2, WezTerm, kitty,
Alacritty, Windows Terminal, foot, …); some disable it by default for security and must be opted in.

Whether OSC 52 actually lands is therefore a property of **your terminal**, not the library. The
library always emits a correct sequence when enabled; the terminal decides whether to honor it.

## Behavior by session type

| Session | OSC 52 | Where a copy lands |
|---|---|---|
| Local (X11 / Wayland / macOS / Windows) | emitted (redundant) | Local clipboard via the local tool (and OSC 52 if the terminal honors it) |
| SSH, plain terminal | emitted | **Local** clipboard (if the terminal supports OSC 52) |
| SSH + **tmux** | emitted, **passthrough-wrapped** | **Local** clipboard — no `~/.tmux.conf` change needed |
| SSH + **screen** (`STY` set) | **skipped** | Server-side tool / in-process buffer (OSC 52 is unreliable under screen) |
| Copy larger than ~74 KB | **skipped** | Local tool only (OSC 52 has terminal size limits) |

Session detection happens once at startup and is exposed (read-only) on
`SharpConsoleUI.Helpers.TerminalCapabilities`:

```csharp
TerminalCapabilities.IsRemoteSession  // SSH_TTY or SSH_CONNECTION set
TerminalCapabilities.IsTmux           // TMUX set — OSC 52 is passthrough-wrapped
TerminalCapabilities.IsScreen         // STY set — OSC 52 skipped
TerminalCapabilities.SupportsOsc52    // whether OSC 52 will be attempted (false under screen)
```

## Configuration

OSC 52 emission is controlled by `ClipboardHelper.Osc52Mode` (default `Auto`):

```csharp
// Default: emit when the session is believed to support OSC 52.
ClipboardHelper.Osc52Mode = Osc52Mode.Auto;

// Always emit, regardless of detection (e.g. a terminal you know supports it).
ClipboardHelper.Osc52Mode = Osc52Mode.Enabled;

// Never emit — local tools / in-process buffer only.
ClipboardHelper.Osc52Mode = Osc52Mode.Disabled;

// Override capability detection (e.g. force-enable under screen, or disable for a known-bad terminal).
TerminalCapabilities.SetOsc52Override(true);   // force on
TerminalCapabilities.SetOsc52Override(false);  // force off
TerminalCapabilities.SetOsc52Override(null);   // restore auto-detection

// Adjust the OSC 52 size cap (base64 payload length). Larger copies skip OSC 52.
ClipboardHelper.MaxOsc52Bytes = Osc52.DefaultMaxBytes; // 74000
```

## How paste works

### Bracketed paste

When the app starts it enables **bracketed paste** (`ESC[?2004h`). The terminal then wraps pasted
content in `ESC[200~` … `ESC[201~`, so the app can recognize a paste as one block instead of a flood
of individual keystrokes. Without it, a multi-line paste would be processed key-by-key — newlines
running as Enter, auto-indent corrupting the text. With it, the block is delivered atomically and
inserted as content.

This is also **the paste path that works over SSH**: when you press your terminal's paste
(Cmd/Ctrl+Shift+V, middle-click, etc.), the terminal injects your **local** clipboard into the SSH
stream, and bracketed paste lets the app insert it correctly.

### `Ctrl+V` and `IPasteTarget`

Paste is centralized: the window routes **both** a bracketed-paste block and `Ctrl+V` to the focused
control's `IPasteTarget.Paste(string)`:

```csharp
public interface IPasteTarget
{
    void Paste(string text); // insert text at the current position as a single block
}
```

Built-in editors implement it (`MultilineEditControl`, `PromptControl`, `TableControl`). `Ctrl+V`
reads `ClipboardHelper.GetText()` (the local-session source); bracketed paste uses the
terminal-delivered text. A read-only editor's `Paste` is a no-op; `PromptControl` (single-line)
flattens embedded newlines to spaces.

> **Remote paste note:** reading the clipboard *back* (`GetText`, the `Ctrl+V` source) reads the
> **server-side** clipboard over SSH — most terminals disable OSC 52 clipboard *reads* for security,
> so there is no reliable app-driven remote read. Use your **terminal's** paste over SSH; bracketed
> paste makes it insert correctly.

## Verifying it works

Automated tests cover the encoder (byte-exact OSC 52 + tmux wrap), session detection, the layered
`SetText`, the bracketed-paste parser, and end-to-end paste routing. The real round-trip depends on
your terminal, so confirm it manually:

```bash
# Local
dotnet run --project Examples/DemoApp
#   → Controls → Selectable Text: drag-select, Ctrl+C, paste into a local editor.

# Remote (the real test) — from an OSC 52-capable terminal
ssh you@server
dotnet run --project Examples/DemoApp
#   → select + Ctrl+C in the app → paste on your LOCAL machine.
#   → paste a multi-line block into the editor → it inserts as one block.
```

## Backward compatibility

- The local clipboard tool still runs on every copy exactly as before; OSC 52 is added in front of
  it and is a no-op on terminals that don't support it.
- `GetText` is unchanged.
- All new surface (`Osc52Mode`, `MaxOsc52Bytes`, the `TerminalCapabilities` session flags,
  `IPasteTarget`) is additive; nothing existing was removed or changed in default behavior.

## See also

- [MultilineEditControl](controls/MultilineEditControl.md) — multi-line editor (Ctrl+C/X/V)
- [PromptControl](controls/PromptControl.md) — single-line input with clipboard support
- [MarkupControl](controls/MarkupControl.md) — opt-in selectable/copyable display text

---

[Back to Documentation](../README.md#documentation)
