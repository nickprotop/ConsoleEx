// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A piece of a <see cref="PromptControl"/>'s input that shows a short label, stands for a
	/// longer value, and is edited as one unit.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The case it exists for: a chat composer wants a long paste to appear as
	/// <c>[Pasted text #1 +18 lines]</c> rather than eighteen lines filling the box, while what is
	/// sent is the full text.
	/// </para>
	/// <para>
	/// Immutable once inserted. There is no way to edit one in place: a host changes a token by
	/// removing it, or by expanding it to ordinary text.
	/// </para>
	/// <para>
	/// A token's value is text, never another token. Nesting is not supported.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// prompt.PasteFilter = text =&gt;
	/// {
	///     int lines = text.Count(c =&gt; c == '\n') + 1;
	///     return lines &gt; 3
	///         ? new PromptToken($"[Pasted text #1 +{lines} lines]", text, "paste")
	///         : null;   // short pastes stay ordinary text
	/// };
	/// </code>
	/// </example>
	public sealed record PromptToken
	{
		/// <summary>Initializes a new instance of the <see cref="PromptToken"/> class.</summary>
		/// <param name="label">What is drawn in the box.</param>
		/// <param name="value">What the token stands for.</param>
		/// <param name="tag">An optional host string. The control never reads it.</param>
		/// <exception cref="ArgumentNullException"><paramref name="label"/> or <paramref name="value"/> is null.</exception>
		public PromptToken(string label, string value, string? tag = null)
		{
			Label = label ?? throw new ArgumentNullException(nameof(label));
			Value = value ?? throw new ArgumentNullException(nameof(value));
			Tag = tag;
		}

		/// <summary>What is drawn in the box.</summary>
		public string Label { get; }

		/// <summary>
		/// What the token stands for. This is what <see cref="PromptControl.Input"/> returns in the
		/// token's place, and what a copy of a selection covering it puts on the clipboard.
		/// </summary>
		public string Value { get; }

		/// <summary>An optional host string, so a host can tell kinds of token apart.</summary>
		public string? Tag { get; }
	}

	/// <summary>
	/// A piece of a <see cref="PromptControl"/>'s content: either a run of ordinary text or a
	/// <see cref="PromptToken"/>. Exactly one of <see cref="Text"/> and <see cref="Token"/> is set.
	/// </summary>
	public readonly record struct PromptSegment
	{
		private PromptSegment(string? text, PromptToken? token)
		{
			Text = text;
			Token = token;
		}

		/// <summary>The run of ordinary text, or null when this segment is a token.</summary>
		public string? Text { get; }

		/// <summary>The token, or null when this segment is ordinary text.</summary>
		public PromptToken? Token { get; }

		/// <summary>Whether this segment is a token rather than ordinary text.</summary>
		public bool IsToken => Token is not null;

		/// <summary>Creates a segment holding a run of ordinary text.</summary>
		/// <param name="text">The text. May be empty.</param>
		/// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
		public static PromptSegment FromText(string text)
			=> new(text ?? throw new ArgumentNullException(nameof(text)), null);

		/// <summary>Creates a segment holding a token.</summary>
		/// <param name="token">The token.</param>
		/// <exception cref="ArgumentNullException"><paramref name="token"/> is null.</exception>
		public static PromptSegment FromToken(PromptToken token)
			=> new(null, token ?? throw new ArgumentNullException(nameof(token)));
	}
}
