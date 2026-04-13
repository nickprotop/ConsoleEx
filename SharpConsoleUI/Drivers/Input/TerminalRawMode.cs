// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Text;

namespace SharpConsoleUI.Drivers.Input
{
	/// <summary>
	/// Manages raw terminal mode on Unix via tcgetattr/tcsetattr.
	/// Provides cfmakeraw-equivalent setup plus raw libc write() for output,
	/// completely bypassing .NET's Console infrastructure to eliminate the
	/// "Linux: Input Echo Leak" issue.
	/// </summary>
	/// <remarks>
	/// When active: input comes from /dev/tty via raw read(), output goes to
	/// stdout fd 1 via raw write(), Console.Out is redirected to /dev/null.
	/// This ensures zero .NET Console code runs on the hot path.
	/// </remarks>
	internal static class TerminalRawMode
	{
		// Terminal control constants.
		// StdinFd/StdoutFd default to the standard stream fds (0 and 1). They are
		// overridden by ResolveTtyFds() at EnterRawMode() time when stdin or stdout
		// is redirected (script/pipeline mode) — in that case both point at a single
		// /dev/tty fd opened RDWR, so the TUI reads keystrokes and writes frames to
		// the real terminal while fd 0/1 remain free for the script's data pipeline.
		internal static int StdinFd { get; private set; } = 0;
		private static int StdoutFd = 1;
		private const int TCSANOW = 0;

		// c_lflag bits to disable (input processing)
		private const uint ECHO = 0x00000008;
		private const uint ICANON = 0x00000002;
		private const uint ISIG = 0x00000001;
		private const uint IEXTEN = 0x00008000;

		// c_oflag bits to disable (output processing)
		private const uint OPOST = 0x00000001;
		private const ulong OPOST_MAC = 0x00000001;

		// c_iflag bits to disable
		private const uint ICRNL = 0x00000100;
		private const uint IXON = 0x00000400;
		private const uint INLCR = 0x00000040;
		private const uint IGNCR = 0x00000080;
		private const uint BRKINT = 0x00000002;
		private const uint ISTRIP = 0x00000020;
		private const uint PARMRK = 0x00000008;

		// macOS has different values for some flags
		private const ulong ECHO_MAC = 0x00000008;
		private const ulong ICANON_MAC = 0x00000100;
		private const ulong ISIG_MAC = 0x00000080;
		private const ulong IEXTEN_MAC = 0x00000400;
		private const ulong ICRNL_MAC = 0x00000100;
		private const ulong IXON_MAC = 0x00000200;
		private const ulong INLCR_MAC = 0x00000040;
		private const ulong IGNCR_MAC = 0x00000080;
		private const ulong BRKINT_MAC = 0x00000002;
		private const ulong ISTRIP_MAC = 0x00000020;
		private const ulong PARMRK_MAC = 0x00000008;

		// VMIN/VTIME indices
		private const byte VMIN_INDEX_LINUX = 6;
		private const byte VTIME_INDEX_LINUX = 5;
		private const byte VMIN_INDEX_MACOS = 16;
		private const byte VTIME_INDEX_MACOS = 17;

		private static bool _isRawModeActive;
		private static bool _hasSavedTermios;
		private static FileStream? _ttyStream;
		private static TextWriter? _savedConsoleOut;
		// fd we opened ourselves via open("/dev/tty"); -1 if we're using fd 0/1 directly
		private static int _ownedTtyFd = -1;

		// Linux termios: flags are uint (4 bytes each), c_line (1 byte), c_cc[32], speeds are uint
		[StructLayout(LayoutKind.Sequential)]
		private struct LinuxTermios
		{
			public uint c_iflag;
			public uint c_oflag;
			public uint c_cflag;
			public uint c_lflag;
			public byte c_line;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
			public byte[] c_cc;
			public uint c_ispeed;
			public uint c_ospeed;
		}

		// macOS termios: flags are ulong (8 bytes each), c_cc[20], speeds are ulong
		[StructLayout(LayoutKind.Sequential)]
		private struct MacTermios
		{
			public ulong c_iflag;
			public ulong c_oflag;
			public ulong c_cflag;
			public ulong c_lflag;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
			public byte[] c_cc;
			public ulong c_ispeed;
			public ulong c_ospeed;
		}

		private static LinuxTermios _savedLinuxTermios;
		private static MacTermios _savedMacTermios;

		// P/Invoke declarations for libc terminal control
		[DllImport("libc", SetLastError = true)]
		private static extern int tcgetattr(int fd, out LinuxTermios termios);

		[DllImport("libc", SetLastError = true, EntryPoint = "tcgetattr")]
		private static extern int tcgetattr_mac(int fd, out MacTermios termios);

		[DllImport("libc", SetLastError = true)]
		private static extern int tcsetattr(int fd, int optionalActions, in LinuxTermios termios);

		[DllImport("libc", SetLastError = true, EntryPoint = "tcsetattr")]
		private static extern int tcsetattr_mac(int fd, int optionalActions, in MacTermios termios);

		[DllImport("libc", SetLastError = true)]
		private static extern int open([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

		[DllImport("libc", SetLastError = true)]
		private static extern int close(int fd);

		[DllImport("libc", SetLastError = true)]
		private static extern int isatty(int fd);

		private const int O_RDONLY = 0;
		private const int O_RDWR = 2;

		// tcflush constants
		private const int TCIFLUSH = 0;

		[DllImport("libc", SetLastError = true)]
		private static extern int tcflush(int fd, int queueSelector);

		/// <summary>
		/// Flushes pending input bytes from stdin. Called during cleanup
		/// to prevent stale mouse/key events from leaking after restore.
		/// </summary>
		public static void FlushStdin()
		{
			try { tcflush(StdinFd, TCIFLUSH); } catch { }
		}

		[DllImport("libc", SetLastError = true)]
		private static extern unsafe int write(int fd, byte* buf, int count);

		[DllImport("libc", SetLastError = true)]
		private static extern unsafe int read(int fd, byte* buf, int count);

		[StructLayout(LayoutKind.Sequential)]
		private struct PollFd
		{
			public int fd;
			public short events;
			public short revents;
		}

		private const short POLLIN = 0x0001;

		[DllImport("libc", SetLastError = true)]
		private static extern int poll(ref PollFd fds, int nfds, int timeout);

		/// <summary>
		/// Reads a single byte from stdin with a timeout using poll() + read().
		/// Returns -1 on timeout or error. This is the correct way to do
		/// non-blocking reads on raw file descriptors — unlike .NET's ReadAsync,
		/// poll() actually respects the timeout at the kernel level.
		/// </summary>
		public static int ReadByteWithTimeout(int timeoutMs)
		{
			var pfd = new PollFd { fd = StdinFd, events = POLLIN, revents = 0 };
			int ret = poll(ref pfd, 1, timeoutMs);
			if (ret <= 0)
				return -1; // Timeout or error

			if ((pfd.revents & POLLIN) == 0)
				return -1;

			unsafe
			{
				byte b;
				int n = read(StdinFd, &b, 1);
				return n == 1 ? b : -1;
			}
		}

		// Reusable byte buffer that grows to fit the largest frame output.
		// Thread safety: only used under _consoleLock in all call sites.
		[ThreadStatic]
		private static byte[]? _writeBuffer;

		/// <summary>
		/// Ensures _writeBuffer is at least the requested size.
		/// Grows geometrically to avoid repeated allocations during ramp-up.
		/// </summary>
		private static byte[] EnsureWriteBuffer(int requiredBytes)
		{
			var buf = _writeBuffer;
			if (buf != null && buf.Length >= requiredBytes)
				return buf;

			// Grow to at least double or the required size, whichever is larger
			int newSize = buf != null
				? Math.Max(buf.Length * 2, requiredBytes)
				: Math.Max(4096, requiredBytes);
			_writeBuffer = new byte[newSize];
			return _writeBuffer;
		}

		/// <summary>
		/// Writes raw bytes to stdout fd 1 via libc write().
		/// </summary>
		private static unsafe void WriteBytes(byte[] buffer, int count)
		{
			fixed (byte* ptr = buffer)
			{
				int offset = 0;
				while (offset < count)
				{
					int written = write(StdoutFd, ptr + offset, count - offset);
					if (written < 0)
						break; // EINTR or error — best effort
					offset += written;
				}
			}
		}

		/// <summary>
		/// Writes a string directly to stdout fd 1 via libc write(), completely
		/// bypassing .NET's Console/StreamWriter/SyncTextWriter infrastructure.
		/// This eliminates any possibility of .NET runtime code touching termios
		/// during output operations.
		/// </summary>
		public static void WriteStdout(string text)
		{
			if (string.IsNullOrEmpty(text))
				return;

			int byteCount = Encoding.UTF8.GetByteCount(text);
			byte[] buffer = EnsureWriteBuffer(byteCount);
			int encoded = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
			WriteBytes(buffer, encoded);
		}

		/// <summary>
		/// Writes a StringBuilder directly to stdout fd 1 via libc write(),
		/// without allocating an intermediate string via ToString().
		/// Iterates over the StringBuilder's internal chunks and encodes
		/// each one into the reusable byte buffer.
		/// </summary>
		public static void WriteStdout(StringBuilder sb)
		{
			if (sb == null || sb.Length == 0)
				return;

			// Worst case: 3 bytes per char for most text (BMP), 4 for supplementary.
			// We grow the buffer once and reuse for subsequent chunks.
			byte[] buffer = EnsureWriteBuffer(sb.Length * 3);
			var encoder = Encoding.UTF8.GetEncoder();

			foreach (var chunk in sb.GetChunks())
			{
				var span = chunk.Span;
				if (span.Length == 0)
					continue;

				// Ensure buffer can hold this chunk
				int maxBytes = Encoding.UTF8.GetMaxByteCount(span.Length);
				if (buffer.Length < maxBytes)
				{
					buffer = EnsureWriteBuffer(maxBytes);
				}

				encoder.Convert(span, buffer.AsSpan(), flush: false, out _, out int bytesUsed, out _);
				if (bytesUsed > 0)
					WriteBytes(buffer, bytesUsed);
			}

			// Flush any remaining encoder state
			encoder.Convert(ReadOnlySpan<char>.Empty, buffer.AsSpan(), flush: true, out _, out int finalBytes, out _);
			if (finalBytes > 0)
				WriteBytes(buffer, finalBytes);
		}

		/// <summary>
		/// Whether raw terminal mode is currently active.
		/// Used by ConsoleBuffer to skip FIX flags and lock contention.
		/// </summary>
		public static bool IsRawModeActive => _isRawModeActive;

		/// <summary>
		/// Returns a FileStream for reading from /dev/tty in raw mode.
		/// This avoids conflicts with .NET's internal stdin management.
		/// Returns null if raw mode is not active.
		/// </summary>
		public static FileStream? TtyInputStream => _ttyStream;

		/// <summary>
		/// Re-applies raw mode settings to both fds. Call periodically to counteract
		/// any .NET runtime code that may restore terminal echo behind our back.
		/// </summary>
		public static void EnforceRawMode()
		{
			if (!_isRawModeActive || !_hasSavedTermios)
				return;

			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					if (tcgetattr_mac(StdinFd, out var current) == 0)
					{
						current.c_lflag &= ~(ECHO_MAC | ICANON_MAC | ISIG_MAC | IEXTEN_MAC);
						current.c_iflag &= ~(ICRNL_MAC | IXON_MAC | INLCR_MAC | IGNCR_MAC | BRKINT_MAC | ISTRIP_MAC | PARMRK_MAC);
						current.c_oflag &= ~OPOST_MAC;
						tcsetattr_mac(StdinFd, TCSANOW, in current);
					}
				}
				else
				{
					if (tcgetattr(StdinFd, out var current) == 0)
					{
						current.c_lflag &= ~(ECHO | ICANON | ISIG | IEXTEN);
						current.c_iflag &= ~(ICRNL | IXON | INLCR | IGNCR | BRKINT | ISTRIP | PARMRK);
						current.c_oflag &= ~OPOST;
						tcsetattr(StdinFd, TCSANOW, in current);
					}
				}
			}
			catch
			{
				// Best effort
			}
		}

		/// <summary>
		/// Detects whether stdin and stdout are both TTYs. If either is redirected
		/// (pipe, file, /dev/null), opens /dev/tty as a single RDWR fd and points
		/// StdinFd and StdoutFd at it. This lets the TUI run correctly even when
		/// the process's stdin/stdout are used for piped data.
		/// </summary>
		/// <returns>True if the fd resolution succeeded (either streams are TTYs,
		/// or /dev/tty was opened successfully). False if /dev/tty could not be
		/// opened — in which case the caller should refuse to enter raw mode.</returns>
		private static bool ResolveTtyFds()
		{
			// Fast path: both standard streams are TTYs, use fd 0/1 as today.
			bool stdinIsTty = isatty(0) == 1;
			bool stdoutIsTty = isatty(1) == 1;

			if (stdinIsTty && stdoutIsTty)
			{
				StdinFd = 0;
				StdoutFd = 1;
				_ownedTtyFd = -1;
				return true;
			}

			// At least one of stdin/stdout is redirected. Open /dev/tty for
			// UI I/O so the pipes remain free for the script's data. A single
			// RDWR fd is the pattern used by fzf, gum, dialog, etc.
			int fd = open("/dev/tty", O_RDWR);
			if (fd < 0)
			{
				// No controlling terminal — the process cannot run a TUI at all.
				// Happens in cron jobs, systemd services without TTY, setsid + /dev/null, etc.
				return false;
			}

			StdinFd = fd;
			StdoutFd = fd;
			_ownedTtyFd = fd;
			return true;
		}

		/// <summary>
		/// Enters raw terminal mode (cfmakeraw equivalent): disables echo, canonical mode,
		/// signal processing, and output post-processing (OPOST).
		/// Sets VMIN=1, VTIME=0 so read() blocks until at least 1 byte is available.
		/// Uses stdin fd 0 directly when stdin/stdout are TTYs (Terminal.Gui v2 pattern);
		/// when either is redirected, opens /dev/tty and uses it for all UI I/O so the
		/// standard streams remain free for the script's data pipeline.
		/// Redirects Console.Out to /dev/null to prevent .NET from writing to stdout.
		/// </summary>
		/// <returns>True if raw mode was successfully entered.</returns>
		public static bool EnterRawMode()
		{
			if (_isRawModeActive)
				return true;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return false;

			// Resolve which fd to use for UI I/O (0/1 fast path, or /dev/tty if redirected).
			if (!ResolveTtyFds())
				return false;

			try
			{
				// Flush any pending input bytes before entering raw mode
				FlushStdin();

				// When using fd 0 directly we follow Terminal.Gui v2's pattern and avoid
				// /dev/tty to prevent byte competition with .NET's internal fd 0 reader.
				// When stdin/stdout are redirected, ResolveTtyFds() has pointed StdinFd
				// at a /dev/tty fd we own and fd 0 is a pipe that .NET will not try to
				// read as a terminal.
				bool result;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					result = EnterRawModeMac();
				else
					result = EnterRawModeLinux();

				if (result)
				{
					// Wrap stdin fd in a FileStream for managed reading
					// ownsHandle: false — we don't own fd 0
					_ttyStream = new FileStream(
						new Microsoft.Win32.SafeHandles.SafeFileHandle((IntPtr)StdinFd, ownsHandle: false),
						FileAccess.Read, bufferSize: 1); // bufferSize=1 prevents .NET buffering

					// Redirect Console.Out to /dev/null to prevent .NET runtime
					// from writing to stdout behind our back
					_savedConsoleOut = Console.Out;
					try { Console.Out.Flush(); } catch { } // Drain any buffered bytes
					Console.SetOut(TextWriter.Null);

					_isRawModeActive = true;
					return true;
				}
				else
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
		}

		private static bool EnterRawModeLinux()
		{
			if (tcgetattr(StdinFd, out var termios) != 0)
				return false;

			_savedLinuxTermios = termios;
			_hasSavedTermios = true;

			// cfmakeraw equivalent: disable input, output, and local processing
			termios.c_lflag &= ~(ECHO | ICANON | ISIG | IEXTEN);
			termios.c_iflag &= ~(ICRNL | IXON | INLCR | IGNCR | BRKINT | ISTRIP | PARMRK);
			termios.c_oflag &= ~OPOST;

			termios.c_cc[VMIN_INDEX_LINUX] = 1;
			termios.c_cc[VTIME_INDEX_LINUX] = 0;

			return tcsetattr(StdinFd, TCSANOW, in termios) == 0;
		}

		private static bool EnterRawModeMac()
		{
			if (tcgetattr_mac(StdinFd, out var termios) != 0)
				return false;

			_savedMacTermios = termios;
			_hasSavedTermios = true;

			// cfmakeraw equivalent: disable input, output, and local processing
			termios.c_lflag &= ~(ECHO_MAC | ICANON_MAC | ISIG_MAC | IEXTEN_MAC);
			termios.c_iflag &= ~(ICRNL_MAC | IXON_MAC | INLCR_MAC | IGNCR_MAC | BRKINT_MAC | ISTRIP_MAC | PARMRK_MAC);
			termios.c_oflag &= ~OPOST_MAC;

			termios.c_cc[VMIN_INDEX_MACOS] = 1;
			termios.c_cc[VTIME_INDEX_MACOS] = 0;

			return tcsetattr_mac(StdinFd, TCSANOW, in termios) == 0;
		}

		// ioctl for window size (replaces Console.WindowWidth/Height)
		[StructLayout(LayoutKind.Sequential)]
		private struct WinSize
		{
			public ushort ws_row;
			public ushort ws_col;
			public ushort ws_xpixel;
			public ushort ws_ypixel;
		}

		private const uint TIOCGWINSZ_LINUX = 0x5413;
		private const uint TIOCGWINSZ_MAC = 0x40087468;

		[DllImport("libc", SetLastError = true)]
		private static extern int ioctl(int fd, uint request, out WinSize winSize);

		// ARM64 variadic workaround: pad registers x2–x7 with dummy ulong args so the
		// real variadic argument (the WinSize pointer) lands on the stack where the
		// ARM64 variadic ABI expects it. See https://github.com/dotnet/runtime/issues/48752
		[DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
		private static extern int ioctl_arm64(
			int fd, uint request,
			ulong __x2, ulong __x3, ulong __x4, ulong __x5, ulong __x6, ulong __x7,
			out WinSize winSize);

		/// <summary>
		/// Gets the terminal window size via ioctl TIOCGWINSZ, bypassing Console.WindowWidth/Height
		/// which goes through ConsolePal and may trigger tcsetattr.
		/// Falls back to Console.WindowWidth/Height if ioctl fails.
		/// </summary>
		public static (int width, int height) GetWindowSize()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return (Console.WindowWidth, Console.WindowHeight);

			uint req = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? TIOCGWINSZ_MAC : TIOCGWINSZ_LINUX;
			int fd = StdinFd;

			// ioctl is a variadic function in libc. On ARM64, variadic arguments use a
			// different calling convention (stack) than fixed arguments (registers).
			// .NET P/Invoke doesn't support variadic calling conventions, so on ARM64 macOS
			// a direct ioctl call crashes with AccessViolationException.
			// Workaround: use a P/Invoke signature that pads registers x2-x7 with dummy
			// fixed args, pushing the real variadic arg onto the stack where the ARM64 ABI
			// expects it. See https://github.com/dotnet/runtime/issues/48752
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
			{
				if (ioctl_arm64(fd, req, 0, 0, 0, 0, 0, 0, out var ws64) == 0 && ws64.ws_col > 0 && ws64.ws_row > 0)
					return (ws64.ws_col, ws64.ws_row);
			}
			else
			{
				if (ioctl(fd, req, out var ws) == 0 && ws.ws_col > 0 && ws.ws_row > 0)
					return (ws.ws_col, ws.ws_row);
			}

			// Fallback — only reached if ioctl fails
			return (Console.WindowWidth, Console.WindowHeight);
		}

		/// <summary>
		/// Restores the terminal to its original settings. Idempotent and safe to call multiple times.
		/// Critical for emergency cleanup to prevent terminal stuck in raw mode on crash.
		/// </summary>
		public static void RestoreTerminal()
		{
			if (!_hasSavedTermios)
				return;

			// Flush pending input before restoring terminal settings
			FlushStdin();

			try
			{
				// Restore stdin fd 0 to saved state
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					tcsetattr_mac(StdinFd, TCSANOW, in _savedMacTermios);
				else
					tcsetattr(StdinFd, TCSANOW, in _savedLinuxTermios);
			}
			catch
			{
				// Swallow — may be called during crash/exit
			}

			_isRawModeActive = false;

			// Restore Console.Out so .NET Console works again for cleanup
			if (_savedConsoleOut != null)
			{
				Console.SetOut(_savedConsoleOut);
				_savedConsoleOut = null;
			}

			// Clean up tty stream. SafeFileHandle was created with ownsHandle:false,
			// so Dispose does not close the underlying fd — we close it ourselves below
			// when we own it (script mode). When StdinFd == 0 the fd must not be closed.
			try { _ttyStream?.Dispose(); } catch { }
			_ttyStream = null;

			// Close /dev/tty fd if we opened it ourselves (script/pipeline mode).
			if (_ownedTtyFd >= 0)
			{
				try { close(_ownedTtyFd); } catch { }
				_ownedTtyFd = -1;
			}

			// Reset to defaults so a subsequent EnterRawMode() starts fresh.
			StdinFd = 0;
			StdoutFd = 1;
		}
	}
}
