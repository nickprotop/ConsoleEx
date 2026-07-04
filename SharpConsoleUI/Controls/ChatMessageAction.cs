// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>Visual variant of a <see cref="ChatMessageAction"/> button.</summary>
	public enum ChatActionVariant
	{
		/// <summary>Neutral, default styling.</summary>
		Default,

		/// <summary>Accent styling for the primary / recommended action.</summary>
		Primary,

		/// <summary>Destructive styling (e.g. delete / stop).</summary>
		Danger,

		/// <summary>Stateful on/off button that tracks a pressed state.</summary>
		Toggle
	}

	/// <summary>What happens to a message's actions row after a non-toggle action is pressed.</summary>
	public enum ChatActionAfterPress
	{
		/// <summary>Leave the actions row in place.</summary>
		None,

		/// <summary>Remove the actions row after the press.</summary>
		Hide
	}

	/// <summary>A single actionable button attached to a chat message's footer. The host owns what the
	/// action does via <see cref="OnClick"/>; the control renders and dispatches it.</summary>
	public sealed record ChatMessageAction
	{
		/// <summary>Stable identifier — used for state updates, toggle restore, and removal.</summary>
		public required string Id { get; init; }

		/// <summary>Button text (markup allowed).</summary>
		public required string Label { get; init; }

		/// <summary>Optional leading glyph (e.g. a narrow icon). Prepended to the label.</summary>
		public string? Icon { get; init; }

		/// <summary>Visual variant. <see cref="ChatActionVariant.Toggle"/> makes it a stateful on/off button.</summary>
		public ChatActionVariant Variant { get; init; } = ChatActionVariant.Default;

		/// <summary>For <see cref="ChatActionVariant.Toggle"/>: the initial pressed state.</summary>
		public bool IsPressed { get; init; }

		/// <summary>Whether the button is enabled.</summary>
		public bool Enabled { get; init; } = true;

		/// <summary>For non-toggle actions: what happens to the actions row after a press.</summary>
		public ChatActionAfterPress AfterPress { get; init; } = ChatActionAfterPress.None;

		/// <summary>Optional grouping key; a separator is drawn between adjacent groups.</summary>
		public string? Group { get; init; }

		/// <summary>Synchronous click handler. Receives a context to set status / hide actions / toggle.</summary>
		public System.Action<ChatActionContext>? OnClick { get; init; }

		/// <summary>Asynchronous click handler (used instead of / in addition to <see cref="OnClick"/>).</summary>
		public System.Func<ChatActionContext, System.Threading.Tasks.Task>? OnClickAsync { get; init; }
	}
}
