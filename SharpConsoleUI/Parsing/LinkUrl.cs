// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Text;

namespace SharpConsoleUI.Parsing
{
    /// <summary>
    /// Percent-escapes a URL so it can safely live inside a <c>[link=…]</c> markup tag,
    /// and reverses that escaping. The markup parser reads a tag by scanning to the next
    /// <c>]</c>, so the escaped form must contain no literal <c>]</c>. <c>%</c> is encoded,
    /// making <see cref="Escape"/>/<see cref="Unescape"/> exact inverses.
    /// </summary>
    public static class LinkUrl
    {
        private const char FirstPrintableAscii = ' '; // 0x20 — chars below this are control chars

        /// <summary>Percent-encodes <c>% [ ] space</c> and control chars (&lt; 0x20). A URL with none passes through unchanged.</summary>
        /// <param name="url">The raw URL to escape. May be <c>null</c> or empty.</param>
        /// <returns>The escaped URL containing no literal <c>]</c>. Returns <see cref="string.Empty"/> for a <c>null</c> or empty input.</returns>
        public static string Escape(string url)
        {
            if (string.IsNullOrEmpty(url)) return url ?? string.Empty;

            var sb = new StringBuilder(url.Length);
            foreach (char c in url)
            {
                switch (c)
                {
                    case '%': sb.Append("%25"); break;
                    case '[': sb.Append("%5B"); break;
                    case ']': sb.Append("%5D"); break;
                    case ' ': sb.Append("%20"); break;
                    default:
                        if (c < FirstPrintableAscii) sb.Append('%').Append(((int)c).ToString("X2"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Reverses <see cref="Escape"/>. Decodes each <c>%XX</c> hex pair; a malformed tail is left literal.</summary>
        /// <param name="escaped">The escaped string produced by <see cref="Escape"/>. May be <c>null</c> or empty.</param>
        /// <returns>The decoded URL. Returns <see cref="string.Empty"/> for a <c>null</c> or empty input.</returns>
        public static string Unescape(string escaped)
        {
            if (string.IsNullOrEmpty(escaped) || escaped.IndexOf('%') < 0)
                return escaped ?? string.Empty;

            var sb = new StringBuilder(escaped.Length);
            int i = 0;
            while (i < escaped.Length)
            {
                // A valid escape needs two hex digits after '%', at i+1 and i+2.
                // The highest index touched is i+2, so it must be a valid index: i + 2 < length.
                if (escaped[i] == '%' && i + 2 < escaped.Length
                    && Uri.IsHexDigit(escaped[i + 1]) && Uri.IsHexDigit(escaped[i + 2]))
                {
                    int hi = Convert.ToInt32(escaped.Substring(i + 1, 2), 16);
                    sb.Append((char)hi);
                    i += 3;
                }
                else
                {
                    sb.Append(escaped[i]);
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
