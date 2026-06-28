// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Shared chrome-formatting helpers for flow hosts and primitive dialogs.
	/// </summary>
	internal static class FlowChromeFormat
	{
		/// <summary>
		/// Formats a window title from a <see cref="FlowChrome"/>, appending the step indicator
		/// (e.g. <c>"Title (2/4)"</c> or <c>"Title (2)"</c>) when present.
		/// </summary>
		internal static string FormatTitle(FlowChrome chrome)
		{
			if (chrome.StepIndicator is { } s)
			{
				return s.Count is { } c
					? $"{chrome.Title} ({s.Index}/{c})"
					: $"{chrome.Title} ({s.Index})";
			}

			return chrome.Title;
		}
	}
}
