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
	/// <summary>Passed to a message action's click handler. Lets the handler drive the message's footer
	/// without capturing the control.</summary>
	public sealed class ChatActionContext
	{
		private readonly System.Action<string, NotificationSeverity?> _setStatus;
		private readonly System.Action _hideActions;
		private readonly System.Action<bool> _setPressed;

		/// <summary>Initialises a new <see cref="ChatActionContext"/>.</summary>
		/// <param name="messageId">The message the action belongs to.</param>
		/// <param name="action">The action being dispatched.</param>
		/// <param name="setStatus">Delegate that sets the message's status row.</param>
		/// <param name="hideActions">Delegate that removes the message's actions row.</param>
		/// <param name="setPressed">Delegate that sets a toggle action's pressed state.</param>
		public ChatActionContext(ChatMessageId messageId, ChatMessageAction action,
			System.Action<string, NotificationSeverity?> setStatus, System.Action hideActions, System.Action<bool> setPressed)
		{
			MessageId = messageId; Action = action;
			_setStatus = setStatus; _hideActions = hideActions; _setPressed = setPressed;
		}

		/// <summary>The message this action belongs to.</summary>
		public ChatMessageId MessageId { get; }

		/// <summary>The action being dispatched.</summary>
		public ChatMessageAction Action { get; }

		/// <summary>Sets this message's status row.</summary>
		/// <param name="text">The status text.</param>
		/// <param name="severity">Optional severity that tints the status text.</param>
		public void SetStatus(string text, NotificationSeverity? severity = null) => _setStatus(text, severity);

		/// <summary>Removes this message's actions row.</summary>
		public void HideActions() => _hideActions();

		/// <summary>For toggle actions: sets the pressed state.</summary>
		/// <param name="pressed">The new pressed state.</param>
		public void SetPressed(bool pressed) => _setPressed(pressed);
	}
}
