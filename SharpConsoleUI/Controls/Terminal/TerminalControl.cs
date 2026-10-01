// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// A self-contained PTY-backed terminal control. The constructor opens the PTY,
/// spawns the target process, and starts a background read thread. Add to any window
/// with .AddControl(). No WithAsyncWindowThread needed.
/// <para>
/// Supported platforms: Linux (openpty + shim), Windows 10 1809+ (ConPTY).
/// </para>
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
public sealed class TerminalControl
	: IWindowControl, IDOMPaintable, IInteractiveControl, IFocusableControl, IMouseAwareControl, IDisposable
{
	private readonly IPtyBackend _pty;
	private readonly VT100Machine _vt;
	private readonly Thread _readThread;
	private readonly object _lock = new();
	private readonly ILogService? _log;
	// Flipped once by whichever thread tears down, and read from the UI thread by IsDisposed and
	// by the input/resize guards. volatile so those reads see the flip rather than a cached zero;
	// the writes go through Interlocked, which is what makes the teardown itself run once.
	private volatile int _disposed = 0;
	private int _exitAnnounced = 0;
	// Laid-out bounds. Written on the UI thread (PaintDOM) and readable from any thread through
	// IWindowControl.ActualWidth/ActualHeight — and this control hands hosts a background thread of
	// its own, since ProcessExited is raised on the PTY-read thread. volatile establishes the
	// publication edge so such a read sees a recent value rather than a hoisted or stale one.
	// Matches BaseControl, which made the same four fields volatile for the same reason;
	// TerminalControl does not derive from it, so it did not inherit the fix.
	private volatile int _actualX;
	private volatile int _actualY;
	private volatile int _actualWidth;
	private volatile int _actualHeight;

	private volatile bool _nudgeReadlineOnResize;

	// Scrollback viewport: 0 = live/bottom, positive = lines scrolled back into history
	private int _scrollOffset;

	/// <summary>Window title derived from the launched executable name.</summary>
	public string Title { get; }

	/// <summary>The OS process ID of the child process running inside the PTY.</summary>
	public int ProcessId => _pty.ChildProcessId;

	/// <summary>
	/// The exit status of the process this terminal ran, or null while it is still running
	/// (or when the status could not be determined). A host that launches one command in a
	/// terminal reads this to learn whether the command worked — zero is a real answer and
	/// means success, not "unknown".
	/// </summary>
	public int? ExitCode => _pty.ExitCode;

	/// <summary>
	/// Whether the containing window force-closes itself when the process exits. True by
	/// default because every existing consumer relies on the window tearing itself down at
	/// EOF; keeping it open is opt-in via <see cref="Builders.TerminalBuilder.KeepOpenOnExit"/>.
	/// When false the final screen stays put — scrolling and <see cref="GetTranscript"/> keep
	/// working after disposal — and closing the window becomes the host's job, typically from
	/// a <see cref="ProcessExited"/> handler after reading <see cref="ExitCode"/>.
	/// </summary>
	public bool CloseWindowOnExit { get; init; } = true;

	/// <summary>Raised on the PTY read thread when the spawned process exits.</summary>
	public event EventHandler? ProcessExited;

	/// <summary>Async counterpart of <see cref="ProcessExited"/>.</summary>
	public event SharpConsoleUI.Core.AsyncEventHandler<EventArgs>? ProcessExitedAsync;

	internal TerminalControl(string exe, string[]? args, string? workingDirectory = null, ILogService? logService = null, Color? defaultBackground = null)
	{
		_log = logService;
		_log?.LogInfo($"TerminalControl: creating for '{exe}' args=[{string.Join(" ", args ?? Array.Empty<string>())}] cwd='{workingDirectory ?? "(inherit)"}'", "Terminal");

		if (OperatingSystem.IsLinux())
		{
			int rows = 24, cols = 80;
			_vt = new VT100Machine(cols, rows, defaultBg: defaultBackground);
			_pty = new LinuxPtyBackend(exe, args, rows, cols, workingDirectory, logService);
		}
		else if (OperatingSystem.IsWindows())
		{
			int rows = 24, cols = 80;
			_vt = new VT100Machine(cols, rows, defaultBg: defaultBackground);
			_pty = new WindowsPtyBackend(exe, args, rows, cols, workingDirectory, logService);
		}
		else
		{
			_log?.LogError("TerminalControl: unsupported platform (requires Linux or Windows)", null, "Terminal");
			throw new PlatformNotSupportedException(
				"TerminalControl requires Linux or Windows 10 1809+.");
		}

		Title = $"  Terminal — {Path.GetFileName(exe)}";

		_readThread = new Thread(ReadLoop) { IsBackground = true, Name = "PTY-read" };
		_readThread.Start();
		_log?.LogInfo($"TerminalControl: PTY read thread started (childPid={_pty.ChildProcessId})", "Terminal");
	}


	private void ReadLoop()
	{
		_log?.LogDebug($"TerminalControl.ReadLoop: entering (pid={_pty.ChildProcessId})", "Terminal");
		var buf = new byte[4096];
		while (true)
		{
			int n = _pty.Read(buf, buf.Length);
			if (n <= 0) break;
			lock (_lock)
			{
				_vt.Process(buf.AsSpan(0, n));
				// Snap viewport to bottom when new output arrives
				_scrollOffset = 0;
			}
			Invalidate(Invalidation.Relayout);
		}

		_log?.LogInfo($"TerminalControl.ReadLoop: EOF reached (pid={_pty.ChildProcessId}), closeWindowOnExit={CloseWindowOnExit}", "Terminal");

		// The read ended, so the session is over however it got here — the child exited on its
		// own, or Dispose signalled it. Teardown therefore finishes HERE, on this thread, in both
		// cases, which is what lets Dispose return to the UI thread immediately: the blocking
		// reap of the child runs on the reader, not on the caller.
		//
		// Reaping MUST precede the event: the backend's Dispose is where the wait on the child
		// happens and where ExitCode is captured, so this ordering is what lets a ProcessExited
		// handler read ExitCode. Raising first would hand every handler null.
		// Not Dispose(): a host-initiated Dispose has already flipped the guard, so calling it
		// here would return without reaping. The reap is idempotent in the backend, so it is
		// invoked directly — this thread is the one that finishes teardown either way.
		Interlocked.Exchange(ref _disposed, 1);
		_pty.Dispose();

		RaiseProcessExited();
	}

	/// <summary>
	/// Fires <see cref="ProcessExited"/> exactly once and, by default, closes the window.
	/// </summary>
	/// <remarks>
	/// Called only from the reader thread's exit path, and guarded so a second arrival — which a
	/// future caller could introduce — cannot double-fire the event.
	/// </remarks>
	private void RaiseProcessExited()
	{
		if (Interlocked.Exchange(ref _exitAnnounced, 1) != 0) return;

		var window = Container as Window;
		var windowSystem = window?.GetConsoleWindowSystem;

		// The event fires regardless of CloseWindowOnExit: a host keeping the window open still
		// needs to know the process is gone — this is its cue to read ExitCode/GetTranscript()
		// and, on its own terms, close the window itself.
		//
		// It is marshalled onto the UI thread, because this runs on the PTY read thread and
		// handlers routinely touch windows and controls. The window close below has always
		// marshalled; raising the event inline next to it was the inconsistency. Both are
		// enqueued in one callback so the host sees the event before the window goes away.
		// Without a window system there is no UI thread to post to, so it is raised inline.
		if (windowSystem != null)
		{
			windowSystem.EnqueueOnUIThread(() =>
			{
				SharpConsoleUI.Core.AsyncEvent.Raise(ProcessExited, ProcessExitedAsync, this, EventArgs.Empty, _log);
				if (CloseWindowOnExit)
					window!.Close(force: true);
			}, "terminalControl.ProcessExited");
		}
		else
		{
			SharpConsoleUI.Core.AsyncEvent.Raise(ProcessExited, ProcessExitedAsync, this, EventArgs.Empty, _log);
			if (CloseWindowOnExit)
				window?.Close(force: true);
		}
	}

	/// Gets whether this terminal's PTY process has exited and the control has been disposed.
	public bool IsDisposed => _disposed != 0;

	/// <summary>
	/// Requests that the next PTY resize also sends Ctrl-L to force
	/// readline to redraw the prompt. Call before a programmatic reparent.
	/// </summary>
	public void NudgeOnNextResize() => _nudgeReadlineOnResize = true;

	/// <summary>
	/// Every line the terminal has shown, oldest first: scrollback history, then the
	/// current screen. Trailing whitespace is trimmed from each line and blank lines are
	/// dropped from the end of the result; interior blank lines are real output and kept.
	/// <para>
	/// Remains readable after the process exits — <see cref="Dispose"/> tears down the PTY
	/// only, and the VT machine with its buffers survives, so a <see cref="ProcessExited"/>
	/// handler (which fires after disposal) can still collect the transcript. Any future
	/// change to <see cref="Dispose"/> must preserve that, or such handlers break silently.
	/// </para>
	/// </summary>
	public IReadOnlyList<string> GetTranscript()
	{
		lock (_lock)
		{
			var lines = new List<string>(_vt.ScrollbackCount + _vt.Height);
			var sb = new StringBuilder(_vt.Width);

			// GetScrollbackLine(0) is the MOST RECENTLY scrolled-off line, so oldest-first
			// means walking the indices downward.
			for (int i = _vt.ScrollbackCount - 1; i >= 0; i--)
				lines.Add(RenderRow(_vt.GetScrollbackLine(i), sb));

			// Scrollback holds only lines that scrolled OFF the screen; the final screenful —
			// where a command's outcome almost always is — still lives in the screen buffer.
			// A transcript of scrollback alone would omit the answer.
			for (int y = 0; y < _vt.Height; y++)
			{
				sb.Clear();
				for (int x = 0; x < _vt.Width; x++)
					AppendCell(sb, _vt.Screen.GetCell(x, y));
				lines.Add(sb.ToString().TrimEnd());
			}

			// The screen's unused rows are entirely blank; without this the transcript of a
			// short command ends in a page of empty lines.
			int end = lines.Count;
			while (end > 0 && lines[end - 1].Length == 0)
				end--;
			lines.RemoveRange(end, lines.Count - end);
			return lines;
		}
	}

	private static string RenderRow(Cell[]? row, StringBuilder sb)
	{
		// A null row (index raced out of range, or width changed under the ring buffer)
		// still occupies its place in time; an empty line keeps the ordering honest.
		if (row == null) return string.Empty;
		sb.Clear();
		for (int x = 0; x < row.Length; x++)
			AppendCell(sb, row[x]);
		return sb.ToString().TrimEnd();
	}

	private static void AppendCell(StringBuilder sb, in Cell cell)
	{
		// A wide glyph occupies two cells; appending its continuation would double it.
		if (cell.IsWideContinuation) return;
		sb.AppendRune(cell.Character);
		if (cell.Combiners != null) sb.Append(cell.Combiners);
	}

	/// <summary>
	/// Ends the terminal session: the child process is signalled, then reaped, and the PTY is
	/// closed. Safe to call from any thread and more than once.
	/// </summary>
	/// <remarks>
	/// Returns without waiting for the child when called from the UI thread — hosts dispose
	/// terminals from <c>OnClosed</c> and key handlers, and a wait there would freeze the whole
	/// application for as long as the child took to die. The child is signalled synchronously;
	/// the blocking reap then happens on the PTY read thread, which the signal has just woken.
	/// <para>
	/// A host that needs teardown to be complete before continuing — a test, or an app shutting
	/// down — calls <see cref="WaitForExit(int)"/> afterwards.
	/// </para>
	/// </remarks>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
		_log?.LogDebug("TerminalControl.Dispose", "Terminal");

		// Wake the reader. On Linux this signals the child's process group, which is the only
		// thing that unblocks a pending read; on Windows it closes the ConPTY. Either way it
		// returns at once.
		_pty.BeginShutdown();

		// The woken reader finishes teardown, which is what keeps this call from blocking. The
		// one case where nobody else will is a reader that had already exited — then its reap
		// has either run or is running, and the backend's own guard makes this call a no-op if
		// it has. Join(0) is a poll, not a wait.
		if (_readThread.Join(0))
			_pty.Dispose();
	}

	/// <summary>
	/// Blocks until the terminal session has fully torn down — the child reaped, the PTY closed,
	/// and the read thread finished — or until <paramref name="timeoutMs"/> elapses.
	/// </summary>
	/// <param name="timeoutMs">How long to wait, in milliseconds. Use -1 to wait indefinitely.</param>
	/// <returns>True if teardown completed; false if the timeout elapsed first.</returns>
	/// <remarks>
	/// <see cref="Dispose"/> deliberately does not wait, so that disposing from a UI handler
	/// cannot freeze the application. This is the explicit opt-in for callers that do need to
	/// know teardown is finished: an app shutting down, or a test asserting on
	/// <see cref="ExitCode"/>. Never call it from the UI thread while the terminal is running —
	/// the read thread cannot finish if the UI thread is blocked waiting for it.
	/// </remarks>
	public bool WaitForExit(int timeoutMs = 5000)
	{
		if (Thread.CurrentThread == _readThread) return true;
		return _readThread.Join(timeoutMs);
	}

	// ── IInteractiveControl / IFocusableControl ──────────────────────────────

	/// <inheritdoc/>
	public bool HasFocus
	{
		get => ((IFocusableControl)this).ComputeHasFocus();
	}

	/// <inheritdoc/>
	public bool IsEnabled { get; set; } = true;

	/// <summary>Terminal emulation needs Tab characters passed through.</summary>
	public bool WantsTabKey => true;

	/// <inheritdoc/>
	public bool CanReceiveFocus => IsEnabled;

	/// <inheritdoc/>
	public bool ProcessKey(ConsoleKeyInfo key)
	{
		// Shift+PageUp/Down: scroll through scrollback history
		if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
		{
			if (key.Key == ConsoleKey.PageUp)
			{
				lock (_lock)
				{
					int pageSize = Math.Max(1, _vt.Height);
					_scrollOffset = Math.Min(_scrollOffset + pageSize, _vt.ScrollbackCount);
				}
				Invalidate(Invalidation.Relayout);
				return true;
			}
			if (key.Key == ConsoleKey.PageDown)
			{
				lock (_lock)
				{
					int pageSize = Math.Max(1, _vt.Height);
					_scrollOffset = Math.Max(0, _scrollOffset - pageSize);
				}
				Invalidate(Invalidation.Relayout);
				return true;
			}
		}

		// Any other key while scrolled back: snap to bottom first
		lock (_lock)
		{
			if (_scrollOffset > 0)
			{
				_scrollOffset = 0;
				Invalidate(Invalidation.Relayout);
			}
		}

		bool appCursor;
		lock (_lock) appCursor = _vt.AppCursorKeys;

		var bytes = EncodeKey(key, appCursor);
		if (bytes.Length == 0) return false;

		// Nothing is left to type at once the session has ended. Scrollback keys above still work
		// — the final screen stays readable after exit by design — but input has nowhere to go.
		if (_disposed != 0) return true;
		_pty.Write(bytes, bytes.Length);
		return true;
	}

	// ── IMouseAwareControl ───────────────────────────────────────────────────

	/// <inheritdoc/>
	public bool WantsMouseEvents => true;
	/// <inheritdoc/>
	public bool CanFocusWithMouse => true;

#pragma warning disable CS0067
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseClick;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseDoubleClick;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseRightClick;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseEnter;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseLeave;
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseMove;
#pragma warning restore CS0067

	/// <inheritdoc/>
	public bool ProcessMouseEvent(MouseEventArgs args)
	{
		int mouseMode; bool mouseSgr;
		lock (_lock) { mouseMode = _vt.MouseMode; mouseSgr = _vt.MouseSgr; }

		// When terminal app has NOT enabled mouse reporting, use wheel for scrollback
		if (mouseMode == 0)
		{
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				lock (_lock)
					_scrollOffset = Math.Min(
						_scrollOffset + Configuration.ControlDefaults.DefaultTerminalScrollWheelLines,
						_vt.ScrollbackCount);
				Invalidate(Invalidation.Relayout);
				return true;
			}
			if (args.HasFlag(MouseFlags.WheeledDown))
			{
				lock (_lock)
					_scrollOffset = Math.Max(0,
						_scrollOffset - Configuration.ControlDefaults.DefaultTerminalScrollWheelLines);
				Invalidate(Invalidation.Relayout);
				return true;
			}
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}
			return false;
		}

		int col = args.Position.X + 1;
		int row = args.Position.Y + 1;

		int mods = 0;
		if (args.HasFlag(MouseFlags.ButtonShift)) mods |= 4;
		if (args.HasFlag(MouseFlags.ButtonAlt)) mods |= 8;
		if (args.HasFlag(MouseFlags.ButtonCtrl)) mods |= 16;

		if (args.HasFlag(MouseFlags.WheeledUp))
		{ SendMouse(64 | mods, col, row, false, mouseSgr); return true; }
		if (args.HasFlag(MouseFlags.WheeledDown))
		{ SendMouse(65 | mods, col, row, false, mouseSgr); return true; }

		if (args.HasFlag(MouseFlags.Button1Pressed))
		{ SendMouse(0 | mods, col, row, false, mouseSgr); return true; }
		if (args.HasFlag(MouseFlags.Button2Pressed))
		{ SendMouse(1 | mods, col, row, false, mouseSgr); return true; }
		if (args.HasFlag(MouseFlags.Button3Pressed))
		{ SendMouse(2 | mods, col, row, false, mouseSgr); return true; }

		if (args.HasFlag(MouseFlags.Button1Released))
		{ SendMouse(0 | mods, col, row, true, mouseSgr); return true; }
		if (args.HasFlag(MouseFlags.Button2Released))
		{ SendMouse(1 | mods, col, row, true, mouseSgr); return true; }
		if (args.HasFlag(MouseFlags.Button3Released))
		{ SendMouse(2 | mods, col, row, true, mouseSgr); return true; }

		if (mouseMode >= 1002)
		{
			if (args.HasFlag(MouseFlags.Button1Dragged))
			{ SendMouse(32 | mods, col, row, false, mouseSgr); return true; }
			if (args.HasFlag(MouseFlags.Button2Dragged))
			{ SendMouse(33 | mods, col, row, false, mouseSgr); return true; }
			if (args.HasFlag(MouseFlags.Button3Dragged))
			{ SendMouse(34 | mods, col, row, false, mouseSgr); return true; }
		}

		if (mouseMode >= 1003 && args.HasFlag(MouseFlags.ReportMousePosition))
		{ SendMouse(35 | mods, col, row, false, mouseSgr); return true; }

		return false;
	}

	private void SendMouse(int btn, int col, int row, bool release, bool sgr)
	{
		byte[] seq;
		if (sgr)
		{
			char suffix = release ? 'm' : 'M';
			seq = Encoding.ASCII.GetBytes($"\x1b[<{btn};{col};{row}{suffix}");
		}
		else
		{
			if (col > 222 || row > 222) return;
			int b = release ? 3 : btn;
			seq = [0x1B, (byte)'[', (byte)'M',
				   (byte)(32 + b), (byte)(32 + col), (byte)(32 + row)];
		}
		if (_disposed != 0) return;
		_pty.Write(seq, seq.Length);
	}

	// ── IWindowControl ───────────────────────────────────────────────────────

	/// <inheritdoc/>
	public int? ContentWidth { get; } = null;
	/// <inheritdoc/>
	public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Stretch;
	/// <inheritdoc/>
	public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Fill;
	/// <inheritdoc/>
	public IContainer? Container { get; set; }
	/// <inheritdoc/>
	public Margin Margin { get; set; } = new Margin(0, 0, 0, 0);
	/// <inheritdoc/>
	public StickyPosition StickyPosition { get; set; } = StickyPosition.None;
	/// <inheritdoc/>
	public string? Name { get; set; }
	/// <inheritdoc/>
	public object? Tag { get; set; }
	/// <inheritdoc/>
	public bool Visible { get; set; } = true;
	/// <inheritdoc/>
	public int? Width { get; set; }
	/// <inheritdoc/>
	public int? Height { get; set; }

	/// <inheritdoc/>
	public int ActualX => _actualX;
	/// <inheritdoc/>
	public int ActualY => _actualY;
	/// <inheritdoc/>
	public int ActualWidth => _actualWidth;
	/// <inheritdoc/>
	public int ActualHeight => _actualHeight;

	/// <inheritdoc/>
	public System.Drawing.Size GetLogicalContentSize()
		=> new(_vt.Width, _vt.Height);

	/// <inheritdoc/>
	public void Invalidate(Invalidation work) => Container?.Invalidate(work, this);

	// ── IDOMPaintable ────────────────────────────────────────────────────────

	/// <inheritdoc/>
	public LayoutSize MeasureDOM(LayoutConstraints constraints)
		=> new(
			Math.Clamp(_vt.Width, constraints.MinWidth, constraints.MaxWidth),
			Math.Clamp(_vt.Height, constraints.MinHeight, constraints.MaxHeight));

	/// <inheritdoc/>
	public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
						 Color defaultFg, Color defaultBg)
	{
		_actualX = bounds.X;
		_actualY = bounds.Y;
		_actualWidth = bounds.Width;
		_actualHeight = bounds.Height;

		lock (_lock)
		{
			if (bounds.Width != _vt.Width || bounds.Height != _vt.Height)
			{
				// The VT machine is always resized: its buffers outlive the PTY so the final
				// screen reflows with the window after the process has gone. Only the PTY side
				// is skipped once the session has ended — there is no child left to tell.
				_vt.Resize(bounds.Width, bounds.Height);
				if (_disposed == 0)
					_pty.Resize(bounds.Height, bounds.Width);
				// Clamp scroll offset after resize
				_scrollOffset = Math.Min(_scrollOffset, _vt.ScrollbackCount);

				// After a programmatic reparent (dock/undock), bash's readline
				// won't redraw because SA_RESTART keeps read() blocked through
				// SIGWINCH. Ctrl-L (clear-screen) unblocks read() and forces
				// readline to redraw the prompt at the new width.
				if (_nudgeReadlineOnResize && _disposed == 0)
				{
					_nudgeReadlineOnResize = false;
					_pty.Write(new byte[] { 0x0c }, 1);
				}
			}

			if (_scrollOffset == 0)
			{
				// Live view: render directly from screen buffer
				buffer.CopyFrom(
					_vt.Screen,
					new LayoutRect(0, 0,
						Math.Min(bounds.Width, _vt.Width),
						Math.Min(bounds.Height, _vt.Height)),
					bounds.X, bounds.Y);

				if (_vt.CursorVisible)
				{
					int cx = _vt.CursorX, cy = _vt.CursorY;
					if (cx < bounds.Width && cy < bounds.Height)
					{
						var cell = _vt.Screen.GetCell(cx, cy);
						buffer.SetNarrowCell(bounds.X + cx, bounds.Y + cy,
									   cell.Character, cell.Background, cell.Foreground);
					}
				}
			}
			else
			{
				// Scrolled back: composite scrollback lines + top of screen buffer.
				// scrollOffset lines from scrollback at top, rest from screen.
				int scrollbackRows = Math.Min(_scrollOffset, bounds.Height);
				int screenRows = bounds.Height - scrollbackRows;
				int vtWidth = Math.Min(bounds.Width, _vt.Width);

				// Top portion: scrollback lines (most recent scrollback at bottom of this section)
				for (int row = 0; row < scrollbackRows; row++)
				{
					// scrollbackRows-1-row: row 0 gets the oldest of the visible scrollback lines
					int scrollbackIndex = _scrollOffset - 1 - row;
					var line = _vt.GetScrollbackLine(scrollbackIndex);
					if (line != null)
					{
						int lineWidth = Math.Min(vtWidth, line.Length);
						for (int x = 0; x < lineWidth; x++)
						{
							var c = line[x];
							buffer.SetNarrowCell(bounds.X + x, bounds.Y + row,
										   c.Character, c.Foreground, c.Background);
						}
						// Fill remainder if line is narrower than current width
						for (int x = lineWidth; x < bounds.Width; x++)
							buffer.SetNarrowCell(bounds.X + x, bounds.Y + row, ' ', defaultFg, defaultBg);
					}
					else
					{
						// No scrollback data for this row — blank it
						for (int x = 0; x < bounds.Width; x++)
							buffer.SetNarrowCell(bounds.X + x, bounds.Y + row, ' ', defaultFg, defaultBg);
					}
				}

				// Bottom portion: top rows of the live screen buffer
				if (screenRows > 0)
				{
					buffer.CopyFrom(
						_vt.Screen,
						new LayoutRect(0, 0, vtWidth, screenRows),
						bounds.X, bounds.Y + scrollbackRows);
				}

				// No cursor shown when scrolled back — user is viewing history
			}
		}
	}

	// ── Key encoding: ConsoleKeyInfo → xterm-256color escape sequences ───────

	private static byte[] EncodeKey(ConsoleKeyInfo key, bool appCursorKeys)
	{
		if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
		{
			return key.Key switch
			{
				ConsoleKey.C => [0x03],
				ConsoleKey.D => [0x04],
				ConsoleKey.Z => [0x1A],
				ConsoleKey.L => [0x0C],
				ConsoleKey.A => [0x01],
				ConsoleKey.E => [0x05],
				ConsoleKey.U => [0x15],
				ConsoleKey.W => [0x17],
				ConsoleKey.K => [0x0B],
				ConsoleKey.R => [0x12],
				_ => EncodeChar(key.KeyChar),
			};
		}

		return key.Key switch
		{
			ConsoleKey.Enter => [0x0D],
			ConsoleKey.Backspace => [0x7F],
			ConsoleKey.Tab => [0x09],
			ConsoleKey.Escape => [0x1B],

			ConsoleKey.UpArrow => appCursorKeys ? [0x1B, (byte)'O', (byte)'A'] : [0x1B, (byte)'[', (byte)'A'],
			ConsoleKey.DownArrow => appCursorKeys ? [0x1B, (byte)'O', (byte)'B'] : [0x1B, (byte)'[', (byte)'B'],
			ConsoleKey.RightArrow => appCursorKeys ? [0x1B, (byte)'O', (byte)'C'] : [0x1B, (byte)'[', (byte)'C'],
			ConsoleKey.LeftArrow => appCursorKeys ? [0x1B, (byte)'O', (byte)'D'] : [0x1B, (byte)'[', (byte)'D'],

			ConsoleKey.Home => [0x1B, (byte)'O', (byte)'H'],
			ConsoleKey.End => [0x1B, (byte)'O', (byte)'F'],

			ConsoleKey.Delete => [0x1B, (byte)'[', (byte)'3', (byte)'~'],
			ConsoleKey.PageUp => [0x1B, (byte)'[', (byte)'5', (byte)'~'],
			ConsoleKey.PageDown => [0x1B, (byte)'[', (byte)'6', (byte)'~'],
			ConsoleKey.Insert => [0x1B, (byte)'[', (byte)'2', (byte)'~'],

			ConsoleKey.F1 => [0x1B, (byte)'O', (byte)'P'],
			ConsoleKey.F2 => [0x1B, (byte)'O', (byte)'Q'],
			ConsoleKey.F3 => [0x1B, (byte)'O', (byte)'R'],
			ConsoleKey.F4 => [0x1B, (byte)'O', (byte)'S'],
			ConsoleKey.F5 => [0x1B, (byte)'[', (byte)'1', (byte)'5', (byte)'~'],
			ConsoleKey.F6 => [0x1B, (byte)'[', (byte)'1', (byte)'7', (byte)'~'],
			ConsoleKey.F7 => [0x1B, (byte)'[', (byte)'1', (byte)'8', (byte)'~'],
			ConsoleKey.F8 => [0x1B, (byte)'[', (byte)'1', (byte)'9', (byte)'~'],
			ConsoleKey.F9 => [0x1B, (byte)'[', (byte)'2', (byte)'0', (byte)'~'],
			ConsoleKey.F10 => [0x1B, (byte)'[', (byte)'2', (byte)'1', (byte)'~'],
			ConsoleKey.F11 => [0x1B, (byte)'[', (byte)'2', (byte)'3', (byte)'~'],
			ConsoleKey.F12 => [0x1B, (byte)'[', (byte)'2', (byte)'4', (byte)'~'],

			_ => EncodeChar(key.KeyChar),
		};
	}

	private static byte[] EncodeChar(char ch)
		=> ch != 0 ? Encoding.UTF8.GetBytes([ch]) : [];
}
