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
	/// Extension methods for StringBuilder to support Rune (supplementary plane) characters.
	/// </summary>
	public static class StringBuilderExtensions
	{
		/// <summary>
		/// Appends a Rune to the StringBuilder, encoding as a surrogate pair if needed.
		/// </summary>
		public static StringBuilder AppendRune(this StringBuilder sb, Rune rune)
		{
			Span<char> buf = stackalloc char[2];
			int charsWritten = rune.EncodeToUtf16(buf);
			for (int i = 0; i < charsWritten; i++)
				sb.Append(buf[i]);
			return sb;
		}
	}
}
