// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class PromptControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled) return false;

			int cursorPos = CurrentCursorPosition;
			bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);
			bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
			bool alt = key.Modifiers.HasFlag(ConsoleModifiers.Alt);

			// --- Ctrl combinations (readline-style) ---
			if (ctrl)
			{
				switch (key.Key)
				{
					case ConsoleKey.A: // Ctrl+A: select all
						_selectionAnchor = 0;
						MoveCursorTo(_input.Length);
						return true;

					case ConsoleKey.E: // Ctrl+E: cursor to end
						ClearSelection();
						MoveCursorTo(_input.Length);
						return true;

					case ConsoleKey.C: // Ctrl+C: copy selection
						if (HasSelection)
							ClipboardHelper.SetText(SelectedText!);
						return true;

					case ConsoleKey.X: // Ctrl+X: cut
						if (ReadOnly) return true;
						if (HasSelection)
						{
							ClipboardHelper.SetText(SelectedText!);
							DeleteSelection();
							RaiseInputChanged();
						}
						return true;

					case ConsoleKey.K: // Ctrl+K: kill from cursor to end of line
						if (ReadOnly) return true;
						{
							int lineEnd = LogicalLineEnd(cursorPos);
							if (cursorPos < lineEnd)
							{
								_input = _input.Remove(cursorPos, lineEnd - cursorPos);
								InvalidateWrapCache();
								Invalidate(Invalidation.Relayout);
								RaiseInputChanged();
							}
						}
						return true;

					case ConsoleKey.U: // Ctrl+U: kill from start of line to cursor
						if (ReadOnly) return true;
						{
							int lineStart = LogicalLineStart(cursorPos);
							if (cursorPos > lineStart)
							{
								_input = _input.Remove(lineStart, cursorPos - lineStart);
								InvalidateWrapCache();
								MoveCursorTo(lineStart);
								RaiseInputChanged();
							}
						}
						return true;

					case ConsoleKey.W: // Ctrl+W: kill word backward
						if (ReadOnly) return true;
						if (cursorPos > 0)
						{
							int wordStart = FindWordBoundaryLeft(cursorPos);
							_input = _input.Remove(wordStart, cursorPos - wordStart);
							InvalidateWrapCache();
							MoveCursorTo(wordStart);
							RaiseInputChanged();
						}
						return true;

					case ConsoleKey.L: // Ctrl+L: insert newline (the chord that needs no reassembly)
						if (ReadOnly) return true;
						if (_multiline)
						{
							InsertNewline();
							return true;
						}
						return false;

					case ConsoleKey.LeftArrow: // Ctrl+Left: word left
						if (cursorPos > 0)
						{
							PrepareSelection(shift, cursorPos);
							MoveCursorTo(FindWordBoundaryLeft(cursorPos));
						}
						return true;

					case ConsoleKey.RightArrow: // Ctrl+Right: word right
						if (cursorPos < _input.Length)
						{
							PrepareSelection(shift, cursorPos);
							MoveCursorTo(FindWordBoundaryRight(cursorPos));
						}
						return true;

					case ConsoleKey.Home: // Ctrl+Home: start of value
						PrepareSelection(shift, cursorPos);
						MoveCursorTo(0);
						return true;

					case ConsoleKey.End: // Ctrl+End: end of value
						PrepareSelection(shift, cursorPos);
						MoveCursorTo(_input.Length);
						return true;
				}
			}

			// --- Standard keys ---
			if (key.Key == ConsoleKey.Enter)
			{
				return ProcessEnter(ctrl, alt, shift);
			}
			else if (key.Key == ConsoleKey.Backspace)
			{
				if (ReadOnly) return true;
				if (HasSelection)
				{
					DeleteSelection();
					RaiseInputChanged();
					return true;
				}
				if (cursorPos > 0)
				{
					_input = _input.Remove(cursorPos - 1, 1);
					InvalidateWrapCache();
					MoveCursorTo(cursorPos - 1);
					RaiseInputChanged();
				}
				return true;
			}
			else if (key.Key == ConsoleKey.Delete)
			{
				if (ReadOnly) return true;
				if (HasSelection)
				{
					DeleteSelection();
					RaiseInputChanged();
					return true;
				}
				if (cursorPos < _input.Length)
				{
					_input = _input.Remove(cursorPos, 1);
					InvalidateWrapCache();
					Invalidate(Invalidation.Relayout);
					RaiseInputChanged();
				}
				return true;
			}
			else if (key.Key == ConsoleKey.Home)
			{
				PrepareSelection(shift, cursorPos);
				// Multiline goes to the start of the visual row, which is what Home means in a box
				// that wraps; single-line keeps its historical whole-value meaning.
				MoveCursorTo(_multiline ? VisualRowStart(cursorPos) : 0);
				return true;
			}
			else if (key.Key == ConsoleKey.End)
			{
				PrepareSelection(shift, cursorPos);
				MoveCursorTo(_multiline ? VisualRowEnd(cursorPos) : _input.Length);
				return true;
			}
			else if (key.Key == ConsoleKey.LeftArrow && cursorPos > 0)
			{
				PrepareSelection(shift, cursorPos);
				MoveCursorTo(cursorPos - 1);
				return true;
			}
			else if (key.Key == ConsoleKey.RightArrow && cursorPos < _input.Length)
			{
				PrepareSelection(shift, cursorPos);
				MoveCursorTo(cursorPos + 1);
				return true;
			}
			else if (key.Key == ConsoleKey.UpArrow)
			{
				// In a wrapping prompt the arrow first walks the rows it can see. Only once the caret
				// is already on the top row does Up mean "the line before this one" — which is how a
				// modern chat box behaves, and it keeps history reachable without a second chord.
				PrepareSelection(shift, cursorPos);
				if (_multiline && TryMoveCaretVertical(-1))
					return true;
				if (_historyEnabled && _historyIndex > 0)
				{
					_historyIndex--;
					ReplaceFromHistory(_history[_historyIndex]);
					return true;
				}
				return _multiline;
			}
			else if (key.Key == ConsoleKey.DownArrow)
			{
				PrepareSelection(shift, cursorPos);
				if (_multiline && TryMoveCaretVertical(1))
					return true;
				if (_historyEnabled && _historyIndex < _history.Count)
				{
					_historyIndex++;
					ReplaceFromHistory(_historyIndex < _history.Count ? _history[_historyIndex] : string.Empty);
					return true;
				}
				return _multiline;
			}
			else if (key.Key == ConsoleKey.Tab && _tabCompleter != null)
			{
				return ProcessTabCompletion(cursorPos);
			}
			else if (key.Key == ConsoleKey.Escape)
			{
				this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Keyboard);
				Invalidate(Invalidation.Relayout);
				return true;
			}
			else if (!char.IsControl(key.KeyChar))
			{
				if (ReadOnly) return true;
				InsertCharacter(key.KeyChar);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Resolves what Enter means from <see cref="EnterBehavior"/> and the modifiers.
		/// <para>
		/// Under <see cref="EnterBehavior.Submit"/> plain Enter always submits — the point of the
		/// default — and a modified Enter inserts the newline when multiline. Shift and Ctrl are
		/// accepted alongside Alt because the Windows console reports them; no Unix terminal does,
		/// which is why Alt (delivered as ESC then the key) and Ctrl+L exist as the spellings that
		/// actually arrive there.
		/// </para>
		/// </summary>
		private bool ProcessEnter(bool ctrl, bool alt, bool shift)
		{
			bool modified = ctrl || alt || shift;
			bool wantsNewline = _enterBehavior == EnterBehavior.Submit
				? _multiline && modified
				: _multiline && !ctrl;

			if (wantsNewline)
			{
				if (ReadOnly) return true;
				InsertNewline();
				return true;
			}

			Submit();
			return true;
		}

		/// <summary>
		/// Raises <see cref="Entered"/> for the current value, records history, and applies
		/// <see cref="UnfocusOnEnter"/>.
		/// </summary>
		private void Submit()
		{
			if (_historyEnabled)
				AddHistory(_input);

			Core.AsyncEvent.Raise(Entered, EnteredAsync, this, _input, Container?.GetConsoleWindowSystem?.LogService);

			if (UnfocusOnEnter)
			{
				_cursorPosition = 0;
				this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Keyboard);
			}
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>Swaps the value for a history entry and parks the caret at its end.</summary>
		private void ReplaceFromHistory(string value)
		{
			_input = ApplyMaxLength(value);
			ClearSelection();
			InvalidateWrapCache();
			MoveCursorTo(_input.Length);
			RaiseInputChanged();
		}

		private bool ProcessTabCompletion(int cursorPos)
		{
			if (ReadOnly) return false;

			var completions = _tabCompleter!(_input, cursorPos)?.ToList();
			if (completions == null || completions.Count == 0)
				return false; // no matches — let focus leave

			if (completions.Count == 1)
			{
				if (completions[0] == _input)
					return false; // already complete — let focus leave
				_input = ApplyMaxLength(completions[0]);
				InvalidateWrapCache();
				MoveCursorTo(_input.Length);
				RaiseInputChanged();
				return true;
			}

			// Multiple completions: find common prefix and insert it
			var prefix = CommonPrefix(completions);
			if (prefix.Length > _input.Length)
			{
				_input = ApplyMaxLength(prefix);
				InvalidateWrapCache();
				MoveCursorTo(_input.Length);
				RaiseInputChanged();
				return true;
			}

			// Common prefix didn't advance — can't complete further, let focus leave
			return false;
		}

		/// <summary>
		/// Finds the longest common prefix among a list of strings.
		/// </summary>
		private static string CommonPrefix(List<string> strings)
		{
			if (strings.Count == 0) return string.Empty;
			var prefix = strings[0];
			for (int i = 1; i < strings.Count; i++)
			{
				// Walk by UTF-16 code unit but never cut inside a surrogate pair: if the code units
				// diverge on the low half of a pair, back the boundary up to before the high half so
				// the common prefix never ends mid-astral-character (emoji, non-BMP).
				int j = 0;
				while (j < prefix.Length && j < strings[i].Length && prefix[j] == strings[i][j]) j++;
				if (j > 0 && char.IsHighSurrogate(prefix[j - 1]))
					j--;
				prefix = prefix.Substring(0, j);
			}
			return prefix;
		}

		/// <summary>Start of the logical (newline-delimited) line containing <paramref name="pos"/>.</summary>
		private int LogicalLineStart(int pos)
		{
			if (!_multiline || pos <= 0) return 0;
			int idx = _input.LastIndexOf('\n', Math.Min(pos - 1, _input.Length - 1));
			return idx < 0 ? 0 : idx + 1;
		}

		/// <summary>End of the logical (newline-delimited) line containing <paramref name="pos"/>.</summary>
		private int LogicalLineEnd(int pos)
		{
			if (!_multiline) return _input.Length;
			int idx = _input.IndexOf('\n', Math.Clamp(pos, 0, _input.Length));
			return idx < 0 ? _input.Length : idx;
		}
	}
}
