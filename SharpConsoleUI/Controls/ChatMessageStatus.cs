// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;

namespace SharpConsoleUI.Controls
{
	/// <summary>Content for a chat message's status row. The simple case is a single left item (text +
	/// severity); the region lists allow richer status/metadata layouts.</summary>
	public sealed record ChatMessageStatus
	{
		/// <summary>Initialises a new <see cref="ChatMessageStatus"/>.</summary>
		/// <param name="text">The primary status text (left region, single item).</param>
		/// <param name="severity">Optional severity that tints the status text color.</param>
		public ChatMessageStatus(string text, NotificationSeverity? severity = null)
		{ Text = text; Severity = severity; }

		/// <summary>Primary status text (left region, single item).</summary>
		public string Text { get; init; } = "";

		/// <summary>Optional severity — tints the status text color.</summary>
		public NotificationSeverity? Severity { get; init; }

		/// <summary>Optional extra left-region items (beyond <see cref="Text"/>).</summary>
		public System.Collections.Generic.IReadOnlyList<StatusBarItem>? Left { get; init; }

		/// <summary>Optional center-region items.</summary>
		public System.Collections.Generic.IReadOnlyList<StatusBarItem>? Center { get; init; }

		/// <summary>Optional right-region items (e.g. token count / timestamp).</summary>
		public System.Collections.Generic.IReadOnlyList<StatusBarItem>? Right { get; init; }
	}
}
