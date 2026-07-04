// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Identifies the role of a participant in a chat transcript.
	/// </summary>
	public enum ChatRole
	{
		/// <summary>A message from the human user.</summary>
		User,

		/// <summary>A message from the AI assistant.</summary>
		Assistant,

		/// <summary>A system-level prompt or instruction.</summary>
		System,

		/// <summary>Output from a tool or function call.</summary>
		Tool,

		/// <summary>An error message.</summary>
		Error
	}

	/// <summary>
	/// Opaque, value-typed handle to a message in a <c>ChatTranscriptControl</c>.
	/// Equality is by the wrapped integer value.
	/// The default value (Value = 0) is intentionally not a valid id; AddMessage-assigned ids start at 1.
	/// </summary>
	public readonly struct ChatMessageId : IEquatable<ChatMessageId>
	{
		/// <summary>Gets the underlying integer value of this handle.</summary>
		public int Value { get; }

		/// <summary>Initialises a new <see cref="ChatMessageId"/> with the given value.</summary>
		internal ChatMessageId(int value)
		{
			Value = value;
		}

		/// <inheritdoc/>
		public bool Equals(ChatMessageId other) => Value == other.Value;

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is ChatMessageId other && Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => Value.GetHashCode();

		/// <summary>Equality operator.</summary>
		public static bool operator ==(ChatMessageId left, ChatMessageId right) => left.Equals(right);

		/// <summary>Inequality operator.</summary>
		public static bool operator !=(ChatMessageId left, ChatMessageId right) => !left.Equals(right);

		/// <inheritdoc/>
		public override string ToString() => $"ChatMessageId({Value})";
	}

	/// <summary>
	/// Defines the visual presentation of messages belonging to a specific <see cref="ChatRole"/>.
	/// All properties are init-only; create a new instance (or use <c>with</c> expressions) to customise.
	/// </summary>
	public sealed class ChatRoleStyle
	{
		/// <summary>
		/// The <see cref="SharpConsoleUI.Themes.ColorRole"/> used to colour the message panel.
		/// Defaults to <see cref="SharpConsoleUI.Themes.ColorRole.Default"/>.
		/// </summary>
		public ColorRole ColorRole { get; init; } = ColorRole.Default;

		/// <summary>
		/// An explicit border colour override. When <c>null</c> the colour is derived from
		/// <see cref="ColorRole"/> and the active theme.
		/// </summary>
		public Color? BorderColor { get; init; }

		/// <summary>
		/// The header/border style of the message panel.
		/// Defaults to <see cref="CollapsibleHeaderStyle.Borderless"/>.
		/// </summary>
		public CollapsibleHeaderStyle HeaderStyle { get; init; } = CollapsibleHeaderStyle.Borderless;

		/// <summary>
		/// Horizontal alignment of the header text inside the panel header row.
		/// Defaults to <see cref="HorizontalAlignment.Left"/>.
		/// </summary>
		public HorizontalAlignment HeaderAlignment { get; init; } = HorizontalAlignment.Left;

		/// <summary>
		/// Whether to render a header row for this role's messages.
		/// Defaults to <c>true</c>.
		/// </summary>
		public bool ShowHeader { get; init; } = true;

		/// <summary>
		/// Whether messages for this role can be collapsed by the user.
		/// Defaults to <c>false</c>.
		/// </summary>
		public bool Collapsible { get; init; } = false;

		/// <summary>
		/// When <see cref="Collapsible"/> is <c>true</c>, whether messages start in the
		/// collapsed (header-only) state.  Useful for verbose roles such as System or Tool.
		/// Defaults to <c>false</c>.
		/// </summary>
		public bool StartCollapsed { get; init; } = false;

		/// <summary>
		/// Optional factory that produces the header text from the role and an optional author
		/// name.  When <c>null</c> a built-in label is used ("You", "Assistant", …).
		/// </summary>
		public Func<ChatRole, string?, string>? Header { get; init; }

		/// <summary>
		/// The outer margin applied to each message panel, creating visual spacing between
		/// messages.  Defaults to <c>new Margin(0, 0, 0, 1)</c> (one blank line below).
		/// </summary>
		public Margin Margin { get; init; } = new(0, 0, 0, 1);

		/// <summary>
		/// Whether the message body is rendered as Markdown (wrapped in a
		/// <c>[markdown]…[/]</c> markup tag).  Defaults to <c>true</c>.
		/// </summary>
		public bool Markdown { get; init; } = true;

		/// <summary>
		/// Optional gradient applied to the header text.  When non-<c>null</c> the header
		/// is rendered with a colour sweep from <c>From</c> to <c>To</c>.
		/// </summary>
		public (Color From, Color To)? HeaderGradient { get; init; }

		/// <summary>
		/// Optional background colour for the message body area.  May carry an alpha value
		/// (via <c>Color.WithAlpha</c>) so the compositor blends it over the window background.
		/// When <c>null</c> the panel's default background is used.
		/// </summary>
		public Color? Background { get; init; }

		/// <summary>
		/// Per-role override for whether this role's message body allows text selection.
		/// <c>null</c> (default) inherits the control-level <see cref="ChatTranscriptControl.MessagesSelectable"/>;
		/// <c>true</c> forces selection on for this role even when the control master is off;
		/// <c>false</c> forces it off even when the master is on.
		/// </summary>
		public bool? Selectable { get; init; }

		/// <summary>
		/// Optional actions seeded onto every message of this role at creation time (unless the
		/// <c>AddMessage</c> call supplies its own explicit actions, which override these).
		/// <c>null</c> (default) seeds no actions.
		/// </summary>
		public System.Collections.Generic.IReadOnlyList<ChatMessageAction>? DefaultActions { get; init; }
	}
}
