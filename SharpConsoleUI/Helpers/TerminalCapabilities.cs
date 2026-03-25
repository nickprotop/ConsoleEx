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
		/// Probes the terminal to determine if VS16 (U+FE0F) widens emoji.
		/// Writes a test character, queries cursor position via DSR, and compares.
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
		/// Resets all cached capabilities (for testing).
		/// </summary>
		internal static void Reset()
		{
			_supportsVS16Widening = null;
		}

		/// <summary>
		/// Verifies the terminal responds to a DSR (Device Status Report) query.
		/// Unlike <see cref="Probe"/> which swallows failures, this returns false
		/// on timeout — indicating the terminal is not processing escape sequences.
		/// </summary>
		/// <param name="write">Action to write escape sequences to the terminal.</param>
		/// <param name="readByte">Function to read a single byte with timeout. Returns -1 on timeout.</param>
		/// <returns>True if the terminal responded, false if it timed out.</returns>
		public static bool VerifyTerminalResponds(Action<string> write, Func<int> readByte)
		{
			try
			{
				write("\x1b[6n");
				int col = ReadDSRColumn(readByte);
				return col >= 0;
			}
			catch
			{
				return false;
			}
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
	}
}
