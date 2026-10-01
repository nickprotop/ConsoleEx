// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Windows ConPTY backend.
/// Uses <c>CreatePseudoConsole</c> (kernel32) and two anonymous pipes for I/O.
/// Requires Windows 10 build 17763 (October 2018 Update) or later.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPtyBackend : IPtyBackend
{
	private IntPtr _hPcon = IntPtr.Zero;
	private IntPtr _hProcess = IntPtr.Zero;
	private int _processId;
	private Stream? _inputStream;   // parent writes keyboard input here
	private Stream? _outputStream;  // parent reads terminal output from here
	private readonly ILogService? _log;
	private int _disposed = 0;
	private int _shutdownBegun = 0;

	public WindowsPtyBackend(string exe, string[]? args, int rows, int cols, string? workingDirectory = null, ILogService? logService = null)
	{
		_log = logService;
		_log?.LogInfo($"WindowsPtyBackend: creating ConPTY ({rows}x{cols}) for exe='{exe}' cwd='{workingDirectory ?? "(inherit)"}'", "PTY");
		// ── 1. Create anonymous pipes ─────────────────────────────────────────
		//   Input pipe:  parent writes → ConPTY reads (keyboard input)
		//   Output pipe: ConPTY writes → parent reads (terminal output)
		if (!WinPtyNative.CreatePipe(out var inRead, out var inWrite, IntPtr.Zero, 0))
			throw new InvalidOperationException(
				$"CreatePipe (input) failed: {Marshal.GetLastWin32Error()}");
		if (!WinPtyNative.CreatePipe(out var outRead, out var outWrite, IntPtr.Zero, 0))
			throw new InvalidOperationException(
				$"CreatePipe (output) failed: {Marshal.GetLastWin32Error()}");

		// ── 2. Create the ConPTY ──────────────────────────────────────────────
		//   ConPTY takes ownership of inRead and outWrite (the PTY-side handles).
		var size = new WinPtyNative.COORD { X = (short)cols, Y = (short)rows };
		int hr = WinPtyNative.CreatePseudoConsole(size, inRead, outWrite, 0, out _hPcon);
		if (hr != 0)
			throw new InvalidOperationException(
				$"CreatePseudoConsole failed: HRESULT 0x{hr:X8}");

		// Parent no longer needs the PTY-side handles; ConPTY owns them now.
		inRead.Dispose();
		outWrite.Dispose();

		// ── 3. Wrap parent-side handles as streams ───────────────────────────
		_inputStream = new FileStream(inWrite, FileAccess.Write, bufferSize: 4096);
		_outputStream = new FileStream(outRead, FileAccess.Read, bufferSize: 4096);

		// ── 4. Build STARTUPINFOEX with PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE ──
		var siEx = new WinPtyNative.STARTUPINFOEX();
		siEx.StartupInfo.cb = Marshal.SizeOf<WinPtyNative.STARTUPINFOEX>();

		// Get the required size for the attribute list, then allocate and initialise.
		nint attrListSize = 0;
		WinPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
		var attrList = Marshal.AllocHGlobal(attrListSize);
		try
		{
			if (!WinPtyNative.InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
				throw new InvalidOperationException(
					$"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

			// The PSEUDOCONSOLE attribute is special: the HPCON handle is passed
			// BY VALUE as lpValue (the kernel treats lpValue itself as the HPCON),
			// not by address like most other attributes (e.g. PARENT_PROCESS).
			// Passing &handle here instead makes the attribute malformed, so the
			// child never attaches to the ConPTY and spawns its own console window.
			// Matches the official Win32 EchoCon / managed GUIConsole samples.
			// _hPcon stays alive (field) until ClosePseudoConsole in Dispose().
			if (!WinPtyNative.UpdateProcThreadAttribute(
					attrList, 0,
					WinPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
					_hPcon,
					(nint)IntPtr.Size,
					IntPtr.Zero, IntPtr.Zero))
				throw new InvalidOperationException(
					$"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

			siEx.lpAttributeList = attrList;

			// ── 5. Start the child process ────────────────────────────────────
			string cmdLine = BuildCommandLine(exe, args);
			if (!WinPtyNative.CreateProcess(
					null, cmdLine,
					IntPtr.Zero, IntPtr.Zero,
					false,
					WinPtyNative.EXTENDED_STARTUPINFO_PRESENT,
					IntPtr.Zero, workingDirectory,
					ref siEx, out var pi))
			{
				throw new InvalidOperationException(
					$"CreateProcess({exe}) failed: {Marshal.GetLastWin32Error()}");
			}

			_hProcess = pi.hProcess;
			_processId = pi.dwProcessId;
			WinPtyNative.CloseHandle(pi.hThread); // we don't need the thread handle
			_log?.LogInfo($"WindowsPtyBackend: ConPTY ready, childPid={_processId}", "PTY");
		}
		finally
		{
			WinPtyNative.DeleteProcThreadAttributeList(attrList);
			Marshal.FreeHGlobal(attrList);
		}
	}

	// ── IPtyBackend ──────────────────────────────────────────────────────────

	public int ChildProcessId => _processId;

	public int? ExitCode => _exitCodeSet != 0 ? _exitCode : null;

	// Written by whichever thread reaps the child, read from the UI thread through
	// TerminalControl.ExitCode. The flag is published with Interlocked AFTER the value, so a
	// reader that sees the flag set is guaranteed to see the value too.
	private int _exitCode;
	private int _exitCodeSet;

	/// <summary>How long a closed ConPTY is given to end the child before it is terminated.</summary>
	private const uint GracefulExitMs = 200;

	/// <summary>How long the reap waits after terminating before giving up on a status.</summary>
	private const uint ForcedExitMs = 300;

	/// <summary>Exit status reported for a child this backend had to terminate.</summary>
	private const uint TerminatedExitCode = 1;

	public int Read(byte[] buf, int count)
	{
		if (_outputStream == null) return 0;
		try { return _outputStream.Read(buf, 0, count); }
		catch { return 0; }
	}

	public void Write(byte[] buf, int count)
	{
		if (_inputStream == null) return;
		try { _inputStream.Write(buf, 0, count); _inputStream.Flush(); }
		catch { /* process exited / pipe broken */ }
	}

	public void Resize(int rows, int cols)
	{
		if (_hPcon == IntPtr.Zero) return;
		_log?.LogDebug($"WindowsPtyBackend.Resize({rows}x{cols})", "PTY");
		WinPtyNative.ResizePseudoConsole(
			_hPcon,
			new WinPtyNative.COORD { X = (short)cols, Y = (short)rows });
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Windows differs from Linux here: closing the ConPTY and the output stream genuinely does
	/// unblock a pending read, so no signal is needed to wake the reader. What this method adds
	/// over the old inline teardown is that the wake is now separate from the blocking wait,
	/// letting the caller keep the wait off the UI thread.
	/// </remarks>
	public void BeginShutdown()
	{
		if (Interlocked.Exchange(ref _shutdownBegun, 1) != 0) return;
		_log?.LogDebug($"WindowsPtyBackend.BeginShutdown: closing ConPTY, pid={_processId}", "PTY");

		// Close input first — signals EOF to the child's stdin.
		try { _inputStream?.Dispose(); } catch { }
		_inputStream = null;

		// Close the ConPTY — this closes its internal outWrite handle, which causes
		// the output pipe to deliver EOF to our Read loop.
		var hPcon = Interlocked.Exchange(ref _hPcon, IntPtr.Zero);
		if (hPcon != IntPtr.Zero)
			WinPtyNative.ClosePseudoConsole(hPcon);

		// Close the output stream — interrupts any blocked Read().
		try { _outputStream?.Dispose(); } catch { }
		_outputStream = null;
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
		_log?.LogDebug($"WindowsPtyBackend.Dispose: reaping pid={_processId}", "PTY");

		// In case Dispose was reached without BeginShutdown.
		BeginShutdown();

		if (_hProcess != IntPtr.Zero)
		{
			if (WinPtyNative.WaitForSingleObject(_hProcess, GracefulExitMs) != 0)
			{
				// The child outlived its console. Terminate it rather than leaving it running
				// with no terminal attached, matching the SIGKILL fallback on Linux.
				_log?.LogDebug($"WindowsPtyBackend.Dispose: child survived ConPTY close, terminating pid={_processId}", "PTY");
				try { WinPtyNative.TerminateProcess(_hProcess, TerminatedExitCode); } catch { }
				WinPtyNative.WaitForSingleObject(_hProcess, ForcedExitMs);
			}

			// The status must be read before CloseHandle — afterwards there is nothing to ask.
			// STILL_ACTIVE (259) means the waits above timed out with the process still running:
			// that is "unknown", not an exit status (see WinPtyNative.STILL_ACTIVE for the
			// collision this creates with a real 259).
			if (WinPtyNative.GetExitCodeProcess(_hProcess, out uint exitCode)
				&& exitCode != WinPtyNative.STILL_ACTIVE)
			{
				_exitCode = unchecked((int)exitCode);
				Interlocked.Exchange(ref _exitCodeSet, 1);
			}
			WinPtyNative.CloseHandle(_hProcess);
			_hProcess = IntPtr.Zero;
		}
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Builds a properly-quoted Windows command line string.
	/// Follows the CommandLineToArgvW quoting rules.
	/// </summary>
	private static string BuildCommandLine(string exe, string[]? args)
	{
		var sb = new StringBuilder();
		AppendQuoted(sb, exe);
		if (args != null)
			foreach (var a in args) { sb.Append(' '); AppendQuoted(sb, a); }
		return sb.ToString();
	}

	private static void AppendQuoted(StringBuilder sb, string arg)
	{
		// No special characters — no quoting needed.
		if (arg.Length > 0 && !arg.AsSpan().ContainsAny(" \t\""))
		{
			sb.Append(arg);
			return;
		}

		sb.Append('"');
		for (int i = 0; i < arg.Length;)
		{
			// Count consecutive backslashes.
			int bs = 0;
			while (i < arg.Length && arg[i] == '\\') { bs++; i++; }

			if (i == arg.Length)
			{
				// Trailing backslashes before closing quote must be doubled.
				sb.Append('\\', bs * 2);
			}
			else if (arg[i] == '"')
			{
				// Backslashes before a literal quote must be doubled, plus escape the quote.
				sb.Append('\\', bs * 2 + 1);
				sb.Append('"');
				i++;
			}
			else
			{
				sb.Append('\\', bs);
				sb.Append(arg[i++]);
			}
		}
		sb.Append('"');
	}
}
