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
		private static bool? _supportsKittyGraphics;

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
		/// Whether the terminal supports the Kitty graphics protocol for image display.
		/// Defaults to false until probed.
		/// </summary>
		public static bool SupportsKittyGraphics
		{
			get => _supportsKittyGraphics ?? false;
		}

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
			// Wait for ESC
			int b = readByte();
			if (b != 0x1b) return -1;

			// Wait for '['
			b = readByte();
			if (b != '[') return -1;

			// Read digits for row (skip it)
			b = readByte();
			while (b >= '0' && b <= '9')
				b = readByte();

			// Expect ';'
			if (b != ';') return -1;

			// Read digits for column
			int col = 0;
			b = readByte();
			while (b >= '0' && b <= '9')
			{
				col = col * 10 + (b - '0');
				b = readByte();
			}

			// Expect 'R'
			if (b != 'R') return -1;

			return col;
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
