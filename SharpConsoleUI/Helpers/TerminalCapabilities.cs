// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Detects terminal rendering capabilities at runtime.
	/// Probed once during driver initialization; results are cached for the session.
	/// </summary>
	public static class TerminalCapabilities
	{
		private static bool? _supportsVS16Widening;
		private static bool? _supportsUnicode16Widths;
		private static bool? _supportsZwjLigation;
		private static bool? _supportsKittyGraphics;
		private static bool? _isRemoteSession;
		private static bool? _isTmux;
		private static bool? _isScreen;
		private static bool? _osc52Override; // explicit override; null = auto

		/// <summary>
		/// Whether the terminal renders emoji+VS16 (U+FE0F) as 2 columns.
		/// When false, VS16 is ignored by the terminal and emoji stay width 1.
		/// Defaults to true (modern terminal assumption) until probed.
		/// </summary>
		public static bool SupportsVS16Widening
		{
			get => _supportsVS16Widening ?? true;
		}

		/// <summary>
		/// Whether the terminal renders Unicode 16.0 newly-widened characters
		/// (e.g. U+2630 ☰ trigrams) as 2 columns.
		/// When false, these characters are treated as width 1 (Unicode 15.0 behavior).
		/// Defaults to false (most terminals haven't adopted Unicode 16.0 widths yet).
		/// </summary>
		public static bool SupportsUnicode16Widths
		{
			get => _supportsUnicode16Widths ?? false;
		}

		/// <summary>
		/// Whether the terminal renders a ZWJ emoji sequence (e.g. 👨‍👩‍👧‍👦) as a SINGLE 2-column glyph
		/// rather than as its component emoji side by side. Modern terminals ligate ZWJ sequences;
		/// some legacy terminals draw each component separately. Defaults to true (modern assumption)
		/// until probed via <see cref="Probe"/>. Note: ZWJ-cluster width is inherently terminal-dependent —
		/// there is no universally-correct value; the probe makes the library match the actual terminal.
		/// </summary>
		public static bool SupportsZwjLigation
		{
			get => _supportsZwjLigation ?? true;
		}

		/// <summary>
		/// Whether the terminal supports the Kitty graphics protocol for image display.
		/// Defaults to false until probed.
		/// </summary>
		public static bool SupportsKittyGraphics
		{
			get => _supportsKittyGraphics ?? false;
		}

		/// <summary>Whether the session looks remote (SSH_TTY or SSH_CONNECTION set).</summary>
		public static bool IsRemoteSession => _isRemoteSession ?? false;

		/// <summary>Whether running under tmux (TMUX set). OSC 52 must be passthrough-wrapped.</summary>
		public static bool IsTmux => _isTmux ?? false;

		/// <summary>Whether running under GNU screen (STY set). OSC 52 is unreliable here.</summary>
		public static bool IsScreen => _isScreen ?? false;

		/// <summary>
		/// Whether OSC 52 clipboard writes should be attempted. Defaults to true unless under
		/// screen. An explicit override (see <see cref="SetOsc52Override"/>) wins.
		/// </summary>
		public static bool SupportsOsc52 => _osc52Override ?? !IsScreen;

		/// <summary>Forces OSC 52 support on/off, or pass null to restore auto-detection.</summary>
		public static void SetOsc52Override(bool? value) => _osc52Override = value;

		/// <summary>Reads SSH/tmux/screen environment variables once and caches the result.</summary>
		internal static void DetectClipboardEnvironment()
		{
			_isRemoteSession =
				!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_TTY")) ||
				!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_CONNECTION"));
			_isTmux = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TMUX"));
			_isScreen = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STY"));
		}

		/// <summary>Test hook: re-runs detection (cache reset) so env-var cases are independent.</summary>
		internal static void DetectClipboardEnvironmentForTests() => DetectClipboardEnvironment();

		/// <summary>
		/// Probes the terminal to determine rendering capabilities.
		/// Tests VS16 emoji widening and Unicode 16.0 width changes.
		/// Must be called after raw mode is entered and before input loops start.
		/// </summary>
		/// <param name="write">Action to write escape sequences to the terminal.</param>
		/// <param name="readByte">Function to read a single byte from stdin with timeout.
		/// Returns -1 on timeout or error.</param>
		public static void Probe(Action<string> write, Func<int> readByte)
		{
			try
			{
				_supportsVS16Widening = ProbeVS16(write, readByte);
			}
			catch
			{
				// If probing fails, assume modern terminal
				_supportsVS16Widening = true;
			}

			try
			{
				_supportsZwjLigation = ProbeZwjLigation(write, readByte);
			}
			catch
			{
				// If probing fails, assume modern terminal (ligates ZWJ).
				_supportsZwjLigation = true;
			}

			try
			{
				_supportsUnicode16Widths = ProbeUnicode16Width(write, readByte);
			}
			catch
			{
				// If probing fails, assume terminal hasn't adopted Unicode 16.0 widths
				_supportsUnicode16Widths = false;
			}

			try
			{
				_supportsKittyGraphics = ProbeKittyGraphics(write, readByte);
			}
			catch
			{
				_supportsKittyGraphics = IsKittyTerminalByEnvironment();
			}

			if (_supportsKittyGraphics == false)
				_supportsKittyGraphics = IsKittyTerminalByEnvironment();

			// DRAIN WHATEVER IS STILL IN FLIGHT.
			//
			// Draining the FAILURE paths inside ReadDSRColumn is not enough, because the leak also
			// happens when every probe SUCCEEDS. Four queries go out; each reader consumes exactly
			// one reply and returns. A reply that arrives after its reader gave up — the per-byte
			// timeout here is 150ms, and a loaded machine beats that — is left queued with nothing
			// expecting it, and the next consumer is the application's key parser.
			//
			// That is what shipped: `3R ;3R [46;3R` painted into a live transcript. The fragments are
			// the tell — a WHOLE `ESC[46;3R` is recognisable and can be discarded by the parser, but
			// a tail whose ESC was already eaten by a probe reader is indistinguishable from someone
			// typing it.
			//
			// So after probing, read until the stream goes quiet. Every byte here is by definition a
			// reply nobody is waiting for: real user input cannot have arrived yet, since probing
			// runs before the input loop starts (see this method's remarks).
			DrainPendingReplies(readByte);
		}

		/// <summary>
		/// Consumes any probe replies still queued once probing is done, so none of them reach the
		/// application's input parser.
		/// </summary>
		/// <param name="readByte">Byte source; returns -1 on timeout or error.</param>
		private static void DrainPendingReplies(Func<int> readByte)
		{
			// The first -1 ends it: the source signals "nothing there" by timing out, so one quiet
			// read means the queue is empty. The cap bounds the pathological case of a terminal
			// streaming bytes forever, and is far above the handful of replies four probes produce.
			const int MaxDrainBytes = 256;

			try
			{
				for (var i = 0; i < MaxDrainBytes; i++)
					if (readByte() < 0) return;
			}
			catch
			{
				// A read that throws is the same as a read that finds nothing: there is no reply left
				// to consume, and probing must never be the reason startup fails.
			}
		}

		/// <summary>
		/// Allows manual override of the VS16 widening capability.
		/// Useful for testing or when the terminal is known ahead of time.
		/// </summary>
		public static void SetVS16Widening(bool supported)
		{
			_supportsVS16Widening = supported;
		}

		/// <summary>
		/// Allows manual override of the Unicode 16.0 width capability.
		/// Useful for testing or when the terminal is known ahead of time.
		/// </summary>
		public static void SetUnicode16Widths(bool supported)
		{
			_supportsUnicode16Widths = supported;
		}

		/// <summary>
		/// Allows manual override of the Kitty graphics capability.
		/// Useful for testing or when the terminal is known ahead of time.
		/// </summary>
		public static void SetKittyGraphics(bool supported)
		{
			_supportsKittyGraphics = supported;
		}

		/// <summary>
		/// Resets all cached capabilities (for testing).
		/// </summary>
		internal static void Reset()
		{
			_supportsVS16Widening = null;
			_supportsUnicode16Widths = null;
			_supportsKittyGraphics = null;
		}

		private static bool ProbeVS16(Action<string> write, Func<int> readByte)
		{
			// Strategy:
			// 1. Move cursor to column 1 with \r
			// 2. Write a VS16-widenable character + VS16: ✌️ (U+270C + U+FE0F)
			// 3. Query cursor position with DSR: ESC[6n → response: ESC[row;colR
			// 4. Erase the probe text: \r + ESC[K (clear line)
			//
			// If col == 3 → terminal rendered 2 columns → VS16 supported
			// If col == 2 → terminal rendered 1 column → VS16 not supported

			// Step 1-3: Write test char and query position
			write("\r\u270C\uFE0F\x1b[6n");

			// Step 4: Read DSR response: ESC [ row ; col R
			int col = ReadDSRColumn(readByte);

			// Step 5: Clean up probe output
			write("\r\x1b[K");

			if (col < 0)
				return true; // Timeout/error → assume modern

			return col >= 3; // col is 1-based; 3 means cursor at column 3 → char was 2 wide
		}

		/// <summary>
		/// Probes whether the terminal ligates a ZWJ emoji sequence into a single 2-column glyph.
		/// Writes the family emoji (👨‍👩‍👧‍👦) and queries the cursor column via DSR (ESC[6n).
		/// Ligating terminals leave the cursor at column 3 (2 wide); non-ligating terminals advance to
		/// column 9 (four 2-wide emoji side by side). Mirrors <see cref="ProbeVS16"/>.
		/// </summary>
		private static bool ProbeZwjLigation(Action<string> write, Func<int> readByte)
		{
			// \r → column 1; family ZWJ sequence; ESC[6n → cursor position report.
			write("\r\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466\x1b[6n");
			int col = ReadDSRColumn(readByte);
			write("\r\x1b[K"); // clean up probe output
			return InterpretZwjProbeColumn(col);
		}

		/// <summary>Interprets a DSR column from the ZWJ probe. col ≤ 3 (and ≥ 1) → ligated;
		/// a larger column → not ligated; col &lt; 0 (timeout) → assume modern (ligating).</summary>
		private static bool InterpretZwjProbeColumn(int col)
		{
			if (col < 0) return true;   // timeout/error → assume modern
			return col <= 3;            // 1-based; ≤3 means the cluster rendered as ≤2 columns
		}

		/// <summary>Test-only: exercise the ZWJ probe column interpretation without a live terminal.</summary>
		internal static bool ProbeZwjLigationForTest(int dsrColumn) => InterpretZwjProbeColumn(dsrColumn);

		/// <summary>
		/// Probes whether the terminal renders Unicode 16.0 newly-widened characters as 2 columns.
		/// Tests U+2630 (☰ TRIGRAM FOR HEAVEN), which changed from width 1 to 2 in Unicode 16.0.
		/// </summary>
		private static bool ProbeUnicode16Width(Action<string> write, Func<int> readByte)
		{
			// Write ☰ (U+2630) and query cursor position.
			// Unicode 15.0: width 1 → cursor at column 2
			// Unicode 16.0: width 2 → cursor at column 3
			write("\r\u2630\x1b[6n");

			int col = ReadDSRColumn(readByte);

			// Clean up probe output
			write("\r\x1b[K");

			if (col < 0)
				return false; // Timeout/error → assume pre-Unicode 16.0

			return col >= 3; // col 3 means 2-wide rendering (Unicode 16.0)
		}

		/// <summary>
		/// Reads a DSR (Device Status Report) response and extracts the column number.
		/// Expected format: ESC [ row ; col R
		/// Returns -1 on timeout or parse error.
		/// </summary>
		private static int ReadDSRColumn(Func<int> readByte)
		{
			// EVERY FAILURE PATH DRAINS. A bare `return -1` here abandons the rest of the reply in
			// the input buffer, and whatever reads next finds `[46;3R` sitting there — the key parser
			// does not recognise it, so it paints it as typed text. Seen live: `R;3R 6;3R [46;3R`
			// smeared across the transcript, one reply re-entered at four different offsets, each
			// giving up on a different byte and leaving a different tail behind.
			//
			// Desync happens whenever something else writes to the shared TTY while a probe is in
			// flight — a child process that emits its own escape sequences, most often. The probe
			// cannot prevent that; it can refuse to leave debris.

			// Wait for ESC
			int b = readByte();
			if (b != 0x1b) return Drain(readByte, b);

			// Wait for '['
			b = readByte();
			if (b != '[') return Drain(readByte, b);

			// Read digits for row (skip it)
			b = readByte();
			while (b >= '0' && b <= '9')
				b = readByte();

			// Expect ';'
			if (b != ';') return Drain(readByte, b);

			// Read digits for column
			int col = 0;
			b = readByte();
			while (b >= '0' && b <= '9')
			{
				col = col * 10 + (b - '0');
				b = readByte();
			}

			// Expect 'R'
			if (b != 'R') return Drain(readByte, b);

			return col;
		}

		/// <summary>
		/// Consumes bytes through the end of a malformed cursor-position reply, so no partial escape
		/// sequence is left for the next reader to print.
		/// </summary>
		/// <param name="readByte">Byte source; returns -1 on timeout or error.</param>
		/// <param name="current">The byte that failed the parse — already read, still to be judged.</param>
		/// <returns>Always -1: the caller could not read a column, whatever was drained.</returns>
		private static int Drain(Func<int> readByte, int current)
		{
			// 'R' terminates a CPR. Bounded so a stream carrying no terminator — a timeout, or bytes
			// that were never a reply at all — cannot spin here; the cap is far above any real reply
			// (`ESC[999;999R` is 11 bytes) and small enough to be imperceptible.
			const int MaxDrain = 32;

			for (var i = 0; i < MaxDrain && current >= 0 && current != 'R'; i++)
				current = readByte();

			return -1;
		}

		/// <summary>
		/// Probes whether the terminal supports Kitty graphics protocol.
		/// Sends a query action and checks for the OK response.
		/// </summary>
		private static bool ProbeKittyGraphics(Action<string> write, Func<int> readByte)
		{
			write(Imaging.KittyProtocol.BuildQueryCommand());

			int b = readByte();
			if (b != 0x1b) return false;

			b = readByte();
			if (b != '_') return false;

			const int MaxProbeResponseBytes = 4096;
			var response = new System.Text.StringBuilder(32);
			int prev = 0;
			while (true)
			{
				b = readByte();
				if (b < 0) return false;

				if (prev == 0x1b && b == '\\')
					break;

				if (b != 0x1b)
					response.Append((char)b);

				if (response.Length > MaxProbeResponseBytes)
					return false;

				prev = b;
			}

			return response.ToString().Contains("OK");
		}

		/// <summary>
		/// Checks environment variables for known Kitty-compatible terminals.
		/// </summary>
		private static bool IsKittyTerminalByEnvironment()
		{
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KITTY_PID")))
				return true;
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEZTERM_PANE")))
				return true;
			return false;
		}
	}
}
