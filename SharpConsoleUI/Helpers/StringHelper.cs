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
			if (string.IsNullOrEmpty(input) || maxLength <= 0 || ellipsisPosition < 0 || ellipsisPosition >= maxLength)
			{
				throw new ArgumentException("Invalid arguments provided.");
			}

			if (input.Length <= maxLength)
			{
				return input;
			}

			string ellipsis = "...";
			int ellipsisLength = ellipsis.Length;

			if (ellipsisPosition + ellipsisLength > maxLength)
			{
				throw new ArgumentException("Ellipsis position and length exceed maximum length.");
			}

			int startLength = ellipsisPosition;
			int endLength = maxLength - ellipsisPosition - ellipsisLength;

			string start = input.Substring(0, startLength);
			string end = input.Substring(input.Length - endLength, endLength);

			return $"{start}{ellipsis}{end}";
		}
	}
}