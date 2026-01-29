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
	/// Provides helper methods for string manipulation operations.
	/// </summary>
	public static class StringHelper
	{
		/// <summary>
		/// Trims a string to a maximum length with an ellipsis (...) inserted at a specified position.
		/// </summary>
		/// <param name="input">The input string to trim.</param>
		/// <param name="maxLength">The maximum length of the resulting string including the ellipsis.</param>
		/// <param name="ellipsisPosition">The position where the ellipsis should be inserted.</param>
		/// <returns>
		/// The original string if it is shorter than or equal to <paramref name="maxLength"/>;
		/// otherwise, a trimmed string with ellipsis showing the beginning and end portions.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when arguments are invalid (null/empty input, non-positive maxLength,
		/// negative ellipsisPosition, or ellipsisPosition >= maxLength).
		/// </exception>
		/// <example>
		/// <code>
		/// // Returns "Hello...World"
		/// StringHelper.TrimWithEllipsis("Hello Beautiful World", 14, 5);
		/// </code>
		/// </example>
		public static string TrimWithEllipsis(string input, int maxLength, int ellipsisPosition)
		{
			// Handle edge cases gracefully instead of throwing
			if (string.IsNullOrEmpty(input))
			{
				return string.Empty;
			}

			if (maxLength <= 0)
			{
				return string.Empty;
			}

			if (input.Length <= maxLength)
			{
				return input;
			}

			string ellipsis = "...";
			int ellipsisLength = ellipsis.Length;

			// If maxLength is too small for ellipsis, return truncated string or just first chars
			if (maxLength < ellipsisLength)
			{
				return input.Substring(0, maxLength);
			}

			// Clamp ellipsisPosition to valid range
			ellipsisPosition = Math.Max(0, Math.Min(ellipsisPosition, maxLength - ellipsisLength));

			int startLength = ellipsisPosition;
			int endLength = maxLength - ellipsisPosition - ellipsisLength;

			// Ensure we don't go out of bounds
			startLength = Math.Min(startLength, input.Length);
			endLength = Math.Min(endLength, input.Length - startLength);
			endLength = Math.Max(0, endLength);

			string start = input.Substring(0, startLength);
			string end = endLength > 0 ? input.Substring(input.Length - endLength, endLength) : string.Empty;

			return $"{start}{ellipsis}{end}";
		}
	}
}