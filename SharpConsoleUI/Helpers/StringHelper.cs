// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	public static class StringHelper
	{
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