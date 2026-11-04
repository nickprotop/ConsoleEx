// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class PromptControl
	{
		#region Token API

		/// <summary>
		/// Turns a paste into a token, or lets it paste as ordinary text.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Called by <see cref="Paste"/> with the text being pasted. Return a
		/// <see cref="PromptToken"/> to insert that instead of the text, or null to paste as the
		/// control always has.
		/// </para>
		/// <para>
		/// The rule is entirely the host's: how long a paste has to be, how tokens are numbered,
		/// what their labels say. The control owns none of it.
		/// </para>
		/// </remarks>
		/// <example>
		/// <code>
		/// prompt.PasteFilter = text =&gt;
		/// {
		///     int lines = text.Count(c =&gt; c == '\n') + 1;
		///     return lines &gt; 3 ? new PromptToken($"[Pasted text +{lines} lines]", text) : null;
		/// };
		/// </code>
		/// </example>
		public Func<string, PromptToken?>? PasteFilter { get; set; }

		/// <summary>Raised when the user clicks a token.</summary>
		/// <remarks>
		/// The control does not decide what activation means. A host that wants a click to turn the
		/// token back into text calls <see cref="ExpandTokenAtCaret"/> from this handler; one that
		/// wants a preview, a file dialog or nothing at all is equally free to do that.
		/// </remarks>
		public event EventHandler<PromptToken>? TokenActivated;

		/// <summary>
		/// Inserts a token at the caret, replacing any selection.
		/// </summary>
		/// <param name="token">The token to insert.</param>
		/// <exception cref="ArgumentNullException"><paramref name="token"/> is null.</exception>
		/// <remarks>
		/// Refused, leaving the content untouched, when the control is read-only or disabled, or
		/// when the token's label would not fit within <see cref="MaxLength"/>. A token is inserted
		/// whole or not at all: truncating a label would leave a chip that lies about its value.
		/// </remarks>
		public void InsertToken(PromptToken token)
		{
			ArgumentNullException.ThrowIfNull(token);
			if (ReadOnly || !IsEnabled) return;

			if (HasSelection)
				DeleteSelection();

			// MaxLength counts the label, because the label is what occupies the box. A token that
			// does not fit is refused rather than shortened.
			if (_maxLength.HasValue && token.Label.Length > RemainingCapacity)
				return;

			int caret = _cursorPosition;
			SetSegments(SpliceSegments(caret, 0, PromptSegment.FromToken(token)));
			InvalidateWrapCache();
			MoveCursorTo(caret + token.Label.Length);
			RaiseInputChanged();
		}

		/// <summary>
		/// The token the caret is touching, or null when it touches none.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The same token <see cref="ExpandTokenAtCaret"/> would act on, found the same way: the one
		/// behind the caret first, then the one ahead. Reading it changes nothing.
		/// </para>
		/// <para>
		/// For a host whose tokens are not all alike. A composer holding both pasted text and an
		/// attached image wants one key to expand the first and preview the second, and the kind is
		/// the token's own <see cref="PromptToken.Tag"/>. Without this it has to expand the token to
		/// find out what it was, and put the line back when the answer is wrong.
		/// </para>
		/// </remarks>
		public PromptToken? TokenAtCaret
			=> TryGetTokenAtCaret(out _, out _, out PromptToken? token) ? token : null;

		/// <summary>
		/// Replaces the token at the caret with its value, as ordinary text.
		/// </summary>
		/// <returns>
		/// True when a token was expanded. False when the caret is not beside one, the control is
		/// read-only or disabled, or the value would not fit within <see cref="MaxLength"/>. In
		/// every false case nothing changes.
		/// </returns>
		/// <remarks>
		/// The caret is left at the end of the inserted text, where it would be had the value been
		/// typed. A token is immutable, so this is how a host turns one back into something the
		/// user can edit.
		/// </remarks>
		public bool ExpandTokenAtCaret()
		{
			if (ReadOnly || !IsEnabled) return false;

			if (!TryGetTokenAtCaret(out int start, out int length, out PromptToken? token))
				return false;

			// The value replaces the label, and is usually far longer. A cap that would be blown is
			// refused rather than clamped: half a pasted body is not what the user asked for, and
			// truncating it would drop the rest with nothing saying so.
			if (_maxLength.HasValue &&
				DisplayText.Length - length + token!.Value.Length > _maxLength.Value)
			{
				return false;
			}

			SetSegments(SpliceSegments(start, length, PromptSegment.FromText(token!.Value)));
			InvalidateWrapCache();
			MoveCursorTo(start + token.Value.Length);
			RaiseInputChanged();
			return true;
		}

		/// <summary>
		/// The token the caret sits beside, looking back first and then forward.
		/// </summary>
		/// <remarks>
		/// The caret can never rest inside a token, so "at the caret" means one of the two tokens it
		/// could be touching. Behind is preferred because a caret that just stepped over a token is
		/// the common case.
		/// </remarks>
		private bool TryGetTokenAtCaret(out int start, out int length, out PromptToken? token)
		{
			// Walked here rather than asked of TryGetTokenSpanAt, which tests the STRICT interior of
			// a label: that is the right question for "may the caret rest here", and the wrong one
			// here, where the caret is always on an edge and never inside.
			int offset = 0;
			int behindStart = -1, behindLength = 0;
			PromptToken? behindToken = null;
			int aheadStart = -1, aheadLength = 0;
			PromptToken? aheadToken = null;

			foreach (var segment in _segments)
			{
				int segmentLength = segment.IsToken ? segment.Token!.Label.Length : segment.Text!.Length;

				if (segment.IsToken)
				{
					if (offset + segmentLength == _cursorPosition)
					{
						// Touching from behind. Preferred, because a caret that just stepped over a
						// token is the common case, but keep looking in case one also starts here.
						behindStart = offset;
						behindLength = segmentLength;
						behindToken = segment.Token;
					}
					else if (offset == _cursorPosition && aheadToken is null)
					{
						// Remembered rather than returned: a token may also END here, and that one
						// wins. Returning on the first match made the token AHEAD win instead,
						// which is the opposite of what this method promises.
						aheadStart = offset;
						aheadLength = segmentLength;
						aheadToken = segment.Token;
					}
				}

				offset += segmentLength;
			}

			if (behindToken is not null)
			{
				start = behindStart;
				length = behindLength;
				token = behindToken;
				return true;
			}

			if (aheadToken is not null)
			{
				start = aheadStart;
				length = aheadLength;
				token = aheadToken;
				return true;
			}

			start = 0;
			length = 0;
			token = null;
			return false;
		}

		/// <summary>Raises <see cref="TokenActivated"/>.</summary>
		private void RaiseTokenActivated(PromptToken token) => TokenActivated?.Invoke(this, token);

		#endregion
	}
}
