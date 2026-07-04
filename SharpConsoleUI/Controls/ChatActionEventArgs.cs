// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>Event data for a chat message action that was activated.</summary>
	public class ChatActionEventArgs : System.EventArgs
	{
		/// <summary>Initialises a new <see cref="ChatActionEventArgs"/>.</summary>
		/// <param name="messageId">The message the action belongs to.</param>
		/// <param name="action">The action that was activated.</param>
		public ChatActionEventArgs(ChatMessageId messageId, ChatMessageAction action)
		{ MessageId = messageId; Action = action; }

		/// <summary>The message the action belongs to.</summary>
		public ChatMessageId MessageId { get; }

		/// <summary>The action that was activated.</summary>
		public ChatMessageAction Action { get; }
	}

	/// <summary>Event data for a toggle-variant chat message action whose pressed state changed.</summary>
	public sealed class ChatActionToggledEventArgs : ChatActionEventArgs
	{
		/// <summary>Initialises a new <see cref="ChatActionToggledEventArgs"/>.</summary>
		/// <param name="messageId">The message the action belongs to.</param>
		/// <param name="action">The toggle action.</param>
		/// <param name="isPressed">The new pressed state.</param>
		public ChatActionToggledEventArgs(ChatMessageId messageId, ChatMessageAction action, bool isPressed)
			: base(messageId, action) { IsPressed = isPressed; }

		/// <summary>The new pressed state of the toggle action.</summary>
		public bool IsPressed { get; }
	}
}
