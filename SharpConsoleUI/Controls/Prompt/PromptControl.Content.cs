// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class PromptControl
	{
		#region Value

		/// <summary>
		/// Collapses every newline spelling to a single space. Single-line mode stores one line, and
		/// a space is what keeps a pasted paragraph readable rather than run together.
		/// </summary>
		private static string FlattenNewlines(string text)
			=> text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

		/// <summary>
		/// Applies <see cref="MaxLength"/> to a candidate value, truncating rather than rejecting so
		/// a long paste fills the field instead of doing nothing.
		/// </summary>
		private string ApplyMaxLength(string text)
			=> _maxLength.HasValue && text.Length > _maxLength.Value
				? text.Substring(0, _maxLength.Value)
				: text;

		/// <summary>How many more characters the value can accept, or int.MaxValue when unbounded.</summary>
		private int RemainingCapacity
			=> _maxLength.HasValue ? Math.Max(0, _maxLength.Value - _input.Length) : int.MaxValue;

		/// <summary>
		/// Sets the input text and positions the cursor at the end.
		/// </summary>
		/// <param name="input">The text to set as input.</param>
		public void SetInput(string? input)
		{
			// Sanitize: single-line mode stores one line — collapse newlines to spaces.
			if (input != null && !_multiline && input.Contains('\n'))
				input = FlattenNewlines(input);

			input = ApplyMaxLength(input ?? string.Empty);

			int newCursorPos = input.Length;

			_input = input;

			// Set cursor and scroll via services (single source of truth)
			_cursorPosition = newCursorPos;
			_horizontalScrollOffset = 0;
			_verticalScrollOffset = 0;
			ClearSelection();
			InvalidateWrapCache();

			Invalidate(Invalidation.Relayout);
			RaiseInputChanged();
		}

		/// <summary>Inserts pasted text (IPasteTarget).</summary>
		/// <remarks>
		/// In single-line mode newlines are flattened to spaces, as they always have been. In
		/// multiline mode the text is inserted verbatim, which is the point of the mode: a pasted
		/// block keeps its shape.
		/// </remarks>
		public void Paste(string text)
		{
			if (string.IsNullOrEmpty(text)) return;
			if (ReadOnly || !IsEnabled) return;

			if (!_multiline)
				text = FlattenNewlines(text);
			else
				text = text.Replace("\r\n", "\n").Replace('\r', '\n');

			DeleteSelection();

			int room = RemainingCapacity;
			if (room <= 0) return;
			if (text.Length > room) text = text.Substring(0, room);

			_input = _input.Insert(_cursorPosition, text);
			InvalidateWrapCache();
			MoveCursorTo(_cursorPosition + text.Length);
			RaiseInputChanged();
		}

		/// <summary>
		/// Inserts a single typed character at the cursor, replacing any selection.
		/// Returns false when the value is already at <see cref="MaxLength"/>.
		/// </summary>
		private bool InsertCharacter(char c)
		{
			if (HasSelection)
				DeleteSelection();
			if (RemainingCapacity <= 0)
				return false;

			int cursorPos = _cursorPosition;
			_input = _input.Insert(cursorPos, c.ToString());
			ClearSelection();
			InvalidateWrapCache();
			MoveCursorTo(cursorPos + 1);
			RaiseInputChanged();
			return true;
		}

		/// <summary>Inserts a newline at the cursor. Multiline mode only.</summary>
		private bool InsertNewline()
		{
			if (!_multiline) return false;
			return InsertCharacter('\n');
		}

		#endregion

		#region Selection

		/// <summary>
		/// Gets the selected text, or null if no selection.
		/// </summary>
		public string? SelectedText
		{
			get
			{
				if (_selectionAnchor < 0) return null;
				int start = Math.Min(_selectionAnchor, _cursorPosition);
				int end = Math.Max(_selectionAnchor, _cursorPosition);
				if (start == end) return null;
				return _input.Substring(start, end - start);
			}
		}

		/// <summary>
		/// Gets whether there is an active text selection.
		/// </summary>
		/// <remarks>
		/// The anchor is bounded against the CURRENT text, not just compared with the cursor: an anchor
		/// outlives any replacement that does not clear it, and this property gates both the delete path
		/// and the renderer's highlight range (which would otherwise paint a selection past the end of
		/// the text). An anchor past the end therefore reads as "no selection" rather than a range the
		/// callers must each re-validate.
		/// </remarks>
		public bool HasSelection =>
			_selectionAnchor >= 0 && _selectionAnchor <= _input.Length && _selectionAnchor != _cursorPosition;

		/// <summary>
		/// Clears the current selection.
		/// </summary>
		private void ClearSelection()
		{
			_selectionAnchor = -1;
		}

		/// <summary>
		/// Deletes the selected text and positions the cursor at the selection start.
		/// </summary>
		private void DeleteSelection()
		{
			if (!HasSelection) return;

			// CLAMP both ends into the current text before touching it. The anchor is written from many
			// paths and outlives any replacement that does not clear it, so an out-of-range range must be
			// a no-op here rather than an exception: DeleteSelection runs under ProcessKey, whose caller
			// is the input loop in ConsoleWindowSystem.Run(), so throwing takes the whole application down
			// over one keystroke. Clearing the stale anchor on the way out stops it recurring.
			int start = Math.Clamp(Math.Min(_selectionAnchor, _cursorPosition), 0, _input.Length);
			int end = Math.Clamp(Math.Max(_selectionAnchor, _cursorPosition), 0, _input.Length);
			if (end <= start)
			{
				_selectionAnchor = -1;
				return;
			}

			_input = _input.Remove(start, end - start);
			_selectionAnchor = -1;
			InvalidateWrapCache();
			MoveCursorTo(start);
		}

		/// <summary>
		/// Starts or extends a selection ahead of a cursor move. Called with <paramref name="extend"/>
		/// true for the shifted form of a navigation key.
		/// </summary>
		private void PrepareSelection(bool extend, int cursorPos)
		{
			if (extend)
			{
				if (_selectionAnchor < 0) _selectionAnchor = cursorPos;
			}
			else
			{
				ClearSelection();
			}
		}

		#endregion

		#region Cursor

		/// <summary>
		/// Moves the cursor to the specified position and adjusts scroll.
		/// </summary>
		private void MoveCursorTo(int position)
		{
			position = Math.Clamp(position, 0, _input.Length);
			_cursorPosition = position;

			if (!_multiline)
			{
				// Single line: keep the caret inside the visible column window, measured in DISPLAY
				// columns so a field of wide characters scrolls by what it actually renders.
				int effectiveWidth = _effectiveInputWidth > 0 ? _effectiveInputWidth : (_inputWidth ?? int.MaxValue);
				int cursorColumn = UnicodeWidth.CharOffsetToColumn(_input, position);
				int scrollColumn = UnicodeWidth.CharOffsetToColumn(_input, _horizontalScrollOffset);
				if (cursorColumn < scrollColumn)
					SetScrollOffset(position);
				else if (effectiveWidth != int.MaxValue && cursorColumn >= scrollColumn + effectiveWidth)
					SetScrollOffset(UnicodeWidth.ColumnToCharOffset(_input, Math.Max(0, cursorColumn - effectiveWidth + 1)));
			}
			else
			{
				EnsureCaretRowVisible();
			}

			Invalidate(Invalidation.Relayout);
		}

		private void SetScrollOffset(int value)
		{
			int newOffset = Math.Max(0, value);
			// Set scroll position via service (single source of truth)
			_horizontalScrollOffset = newOffset;
		}

		/// <summary>
		/// Finds the start of the word to the left of the given position.
		/// </summary>
		private int FindWordBoundaryLeft(int pos)
		{
			if (pos <= 0) return 0;
			int i = pos - 1;
			// Skip whitespace
			while (i > 0 && char.IsWhiteSpace(_input[i])) i--;
			// Skip word characters
			while (i > 0 && !char.IsWhiteSpace(_input[i - 1])) i--;
			return i;
		}

		/// <summary>
		/// Finds the end of the word to the right of the given position.
		/// </summary>
		private int FindWordBoundaryRight(int pos)
		{
			if (pos >= _input.Length) return _input.Length;
			int i = pos;
			// Skip current word
			while (i < _input.Length && !char.IsWhiteSpace(_input[i])) i++;
			// Skip whitespace
			while (i < _input.Length && char.IsWhiteSpace(_input[i])) i++;
			return i;
		}

		#endregion

		#region History

		/// <summary>
		/// Clears the command history.
		/// </summary>
		public void ClearHistory()
		{
			_history.Clear();
			_historyIndex = 0;
			// The stash is positioned relative to the history that just went away; keeping it would let
			// a Down restore a draft into a browse that no longer exists.
			_draftStash = null;
		}

		/// <summary>
		/// The recorded history, oldest first.
		/// </summary>
		/// <remarks>
		/// <para>Read-only, and a SNAPSHOT rather than a live view: the internal list is mutated on
		/// every submit and trimmed past <see cref="MaxHistoryEntries"/>, so handing out the list
		/// itself would let a caller observe it changing underneath them — or mutate it and desync
		/// the ↑/↓ index that walks it.</para>
		/// <para>Exposed so history can outlive the control: an application that wants ↑ to reach
		/// what was typed in a PREVIOUS session has to be able to read what was typed in this one.
		/// <see cref="RecordHistory"/> is the other half — read them out at shutdown, feed them back
		/// at startup.</para>
		/// </remarks>
		public IReadOnlyList<string> History => _history.ToArray();

		/// <summary>
		/// Records a value in the history as though it had been submitted here.
		/// </summary>
		/// <remarks>
		/// <para>For an application that submits on its OWN terms rather than through this control's
		/// Enter — one that intercepts the key upstream (a window-level PreviewKeyPressed, say, to
		/// give Enter a meaning the control cannot know) and then reads <see cref="Input"/> itself.
		/// Submit() is never reached on that path, so nothing was ever recorded and ↑/↓ recalled an
		/// empty history: the control offered the feature and only its own key could feed it.</para>
		/// <para>Same rules as an ordinary submit — empty values and consecutive duplicates are
		/// ignored, and the oldest entries drop past <see cref="MaxHistoryEntries"/>.</para>
		/// </remarks>
		/// <param name="value">The value to record.</param>
		public void RecordHistory(string value) => AddHistory(value);

		/// <summary>
		/// Records a submitted value. Skips a repeat of the immediately previous entry, and drops the
		/// oldest entries once the history exceeds <see cref="MaxHistoryEntries"/> — without which a
		/// command line accumulates every line ever typed for the life of the process.
		/// </summary>
		private void AddHistory(string value)
		{
			// Committing ends the browse: the pending draft was either sent or abandoned, so it must not
			// survive to be restored by a later Down. Cleared before the empty/duplicate early-outs
			// below, since those still end the browse.
			_draftStash = null;

			if (string.IsNullOrEmpty(value)) return;
			if (_history.Count > 0 && _history[^1] == value)
			{
				_historyIndex = _history.Count;
				return;
			}
			_history.Add(value);
			TrimHistory();
			_historyIndex = _history.Count;
		}

		private void TrimHistory()
		{
			int excess = _history.Count - _maxHistoryEntries;
			if (excess > 0)
			{
				_history.RemoveRange(0, excess);
				if (_historyIndex > _history.Count) _historyIndex = _history.Count;
			}
		}

		#endregion
	}
}
