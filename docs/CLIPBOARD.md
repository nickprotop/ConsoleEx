# Clipboard, Copy & Paste

SharpConsoleUI's clipboard is designed to work **both on a local desktop and over SSH** — a copy
inside an app running on a remote server can land on the operator's **local** clipboard. This guide
explains how copy and paste work, the remote/SSH behavior, and the configuration knobs.

## TL;DR

- **Copy** writes the system clipboard via the local tool (`xclip`/`wl-copy`/`pbcopy`/`clip.exe`) —
  or, on a Linux desktop with none of those installed, [directly over X11](#linux-without-a-clipboard-tool)
  — **and** emits an [OSC 52](#osc-52-the-remote-clipboard-escape) escape so the text also reaches the
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

## Opening URLs

`SharpConsoleUI.Helpers.UrlLauncher` opens a URL in the platform's default browser — the natural
companion to the clickable links you can embed in markup and markdown:

```csharp
UrlLauncher.Open("https://example.com");
```

Cross-platform (uses the OS shell, falling back to `xdg-open` / `open`), **best-effort, and never
throws** — a `null`/whitespace URL is a safe no-op, and a failed launch is swallowed. Wire it into a
link click or an "Open in browser" action.

## How copy works

`SetText` does two things, in order:

1. **Emit OSC 52** (when enabled — see below) through the terminal's output stream. This is what
   reaches the **local** clipboard over SSH.
2. **Write the system clipboard**: through the Win32 clipboard API (Windows), `pbcopy` (macOS), or on
   Linux the first backend that can reach the session — see [Linux backends](#linux-backends) — or an
   in-process buffer when none can.

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

### Linux backends

The backend is chosen once, on the first copy or paste, by what the **session** can reach — not only by
what is installed:

| Order | Backend | Used when |
|---|---|---|
| 1 | `wl-copy` / `wl-paste` | a Wayland session (`WAYLAND_DISPLAY`, or the default `wayland-0` socket) and wl-clipboard is installed |
| 2 | `xclip` | an X11 display (`DISPLAY`) and xclip is installed |
| 3 | `xsel` | an X11 display and xsel is installed |
| 4 | **X11, built in** | an X11 display and **none** of the tools is installed |
| 5 | in-process buffer | no display at all (SSH, a console) — OSC 52 still carries copies to your terminal |

The tools keep their order, so a machine that copied through one still does. A tool is skipped when the
session cannot use it — wl-copy with no Wayland compositor, xclip over SSH with no display — where it used to
be chosen and fail every copy silently. `ClipboardHelper.Backend` reports the choice.

Tools are found by a PATH lookup, not by running them, and their output is captured: a tool's error message
("Nothing is copied", "Can't open display") never lands on the rendered UI.

### Linux without a clipboard tool

A stock GNOME or KDE desktop ships none of `wl-copy`, `xclip` or `xsel`, and VTE terminals (GNOME Terminal,
Ptyxis, Console) ignore OSC 52 — so a copy used to reach nothing. The built-in X11 backend talks to
**libX11** directly, which every X11 desktop has, and which GNOME and KDE also run under Wayland as
**Xwayland**, bridged to the Wayland clipboard. Nothing to install, nothing to configure.

- **Copy** makes the app the clipboard's owner; other apps' pastes are answered from a background thread.
  Text of any size is sent — large copies in pieces (the X11 INCR protocol), as strict clients require.
- **Paste** (`Ctrl+V`) reads the clipboard the same way, bounded by the usual timeout.
- **On exit** the text is handed to the desktop's clipboard manager (the ICCCM `SAVE_TARGETS` handshake),
  so a paste after the app has quit still finds it. GNOME keeps the copy itself instead, up to a size limit
  — a copy of several hundred KB or more is pasteable while the app runs but not after it exits, as with any
  X11 app on GNOME.
- **Failures stay silent**: a missing libX11, a refused connection, or a lost display falls back to the
  in-process buffer. Nothing is printed and the process is never ended by Xlib.
- AOT-compatible: plain `DllImport` and function pointers, no runtime code generation.

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
| SSH + **tmux** | emitted **twice**: plain, then passthrough-wrapped | **Local** clipboard, once tmux forwards either form — see below |
| SSH + **screen** (`STY` set) | **skipped** | Server-side tool / in-process buffer (OSC 52 is unreliable under screen) |
| Copy larger than ~74 KB | **skipped** | Local tool only (OSC 52 has terminal size limits) |

Session detection happens once at startup and is exposed (read-only) on
`SharpConsoleUI.Helpers.TerminalCapabilities`:

```csharp
TerminalCapabilities.IsRemoteSession  // SSH_TTY or SSH_CONNECTION set
TerminalCapabilities.IsTmux           // TMUX set — OSC 52 is sent plain and passthrough-wrapped
TerminalCapabilities.IsScreen         // STY set — OSC 52 skipped
TerminalCapabilities.SupportsOsc52    // whether OSC 52 will be attempted (false under screen)
```

### tmux

tmux forwards an application's OSC 52 to the outer terminal only when told to, and its defaults forward
neither form (measured on tmux 3.6). Each form needs its own setting, so both are sent and either line in
`~/.tmux.conf` is enough:

```tmux
set -g set-clipboard on        # forwards the plain sequence (and keeps a tmux buffer of it)
set -g allow-passthrough on    # forwards the passthrough-wrapped one
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
terminal-delivered text. A read-only editor's `Paste` is a no-op. `PromptControl` flattens embedded
newlines to spaces when it is single-line (the default) and inserts them verbatim when
`Multiline` is set; text longer than its `MaxLength` is truncated to fit rather than rejected.

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

- The local clipboard tool still runs on every copy exactly as before, wherever it can reach the
  session; OSC 52 is added in front of it and is a no-op on terminals that don't support it.
- The built-in X11 backend only takes the case that used to fall back to the in-process buffer, and
  `ClipboardBackend.X11` is a new enum member added at the end.
- `GetText` is unchanged.
- All new surface (`Osc52Mode`, `MaxOsc52Bytes`, the `TerminalCapabilities` session flags,
  `IPasteTarget`) is additive; nothing existing was removed or changed in default behavior.

## See also

- [MultilineEditControl](controls/MultilineEditControl.md) — multi-line editor (Ctrl+C/X/V)
- [PromptControl](controls/PromptControl.md) — single-line input with clipboard support
- [MarkupControl](controls/MarkupControl.md) — opt-in selectable/copyable display text

---

[Back to Documentation](https://nickprotop.github.io/ConsoleEx/#documentation)
