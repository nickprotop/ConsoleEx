// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Builds OSC 52 clipboard escape sequences (and the tmux passthrough-wrapped form).
	/// Pure string logic — no I/O. The caller writes the returned sequence to the terminal.
	/// </summary>
	public static class Osc52
	{
		/// <summary>Conservative payload cap: below common terminal OSC 52 ceilings (~74–100KB).</summary>
		public const int DefaultMaxBytes = 74000;

		/// <summary>
		/// Builds an OSC 52 set-clipboard sequence for the clipboard selection ("c").
		/// Returns null when the base64 payload exceeds <paramref name="maxBytes"/> (caller skips emit).
		/// </summary>
		/// <param name="text">The text to place on the clipboard.</param>
		/// <param name="tmuxWrap">Wrap in the tmux DCS passthrough envelope (for sessions under tmux).</param>
		/// <param name="maxBytes">Maximum allowed base64 length; over this returns null.</param>
		public static string? BuildSequence(string text, bool tmuxWrap, int maxBytes)
		{
			string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? string.Empty));
			if (base64.Length > maxBytes)
				return null;

			// OSC 52 set clipboard: ESC ] 52 ; c ; <base64> BEL
			string inner = $"\x1b]52;c;{base64}\x07";
			if (!tmuxWrap)
				return inner;

			// tmux passthrough: ESC P tmux; <inner with every ESC doubled> ESC \
			string doubled = inner.Replace("\x1b", "\x1b\x1b");
			return $"\x1bPtmux;{doubled}\x1b\\";
		}
	}
}
