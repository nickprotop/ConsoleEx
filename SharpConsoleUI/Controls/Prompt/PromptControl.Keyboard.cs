// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

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
						MoveCursorTo(DisplayText.Length);
						return true;

					case ConsoleKey.E: // Ctrl+E: cursor to end
						ClearSelection();
						MoveCursorTo(DisplayText.Length);
						return true;

					case ConsoleKey.C: // Ctrl+C: copy selection
									   // The VALUE projection: a chip pasted into another application has to carry
									   // what it stands for, since its label means nothing outside this control.
						if (HasSelection)
							ClipboardHelper.SetText(SelectedValueText!);
						return true;

					case ConsoleKey.X: // Ctrl+X: cut
						if (ReadOnly) return true;
						if (HasSelection)
						{
							ClipboardHelper.SetText(SelectedValueText!);
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
								RemoveDisplayText(cursorPos, lineEnd - cursorPos);
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
								RemoveDisplayText(lineStart, cursorPos - lineStart);
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
							RemoveDisplayText(wordStart, cursorPos - wordStart);
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
						if (cursorPos < DisplayText.Length)
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
						MoveCursorTo(DisplayText.Length);
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
					// A token behind the caret goes whole. TryGetTokenSpanAt is asked about the index
					// one back, which is INSIDE the label of a token the caret sits just after, and so
					// reports exactly the token Backspace should take.
					//
					// RemoveDisplayText would take that token whole either way, since its splice drops
					// any token the range touches. What this lookup is actually for is the CARET: it
					// gives MoveCursorTo the token's start, where the caret belongs, rather than
					// cursorPos - 1, which is a label-length too far along once the label has gone.
					int removeFrom = cursorPos - 1;
					int removeLength = 1;
					if (TryGetTokenSpanAt(cursorPos - 1, out int tokenStart, out int tokenLength, out _))
					{
						removeFrom = tokenStart;
						removeLength = tokenLength;
					}

					RemoveDisplayText(removeFrom, removeLength);
					InvalidateWrapCache();
					MoveCursorTo(removeFrom);
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
				if (cursorPos < DisplayText.Length)
				{
					// Mirror of Backspace: the index one FORWARD is inside the label of a token the
					// caret sits just before, so the whole token goes rather than its first character.
					//
					// Here the splice is sufficient on its own (the caret does not move, so there is
					// no second thing to get right), and removing this lookup breaks no test. It is
					// kept so the removal length says locally what Delete means, instead of leaning on
					// a non-obvious overlap rule in another method.
					int removeLength = 1;
					if (TryGetTokenSpanAt(cursorPos + 1, out int tokenStart, out int tokenLength, out _) && tokenStart == cursorPos)
						removeLength = tokenLength;

					RemoveDisplayText(cursorPos, removeLength);
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
				MoveCursorTo(_multiline ? VisualRowEnd(cursorPos) : DisplayText.Length);
				return true;
			}
			else if (key.Key == ConsoleKey.LeftArrow && cursorPos > 0)
			{
				PrepareSelection(shift, cursorPos);
				MoveCursorTo(cursorPos - 1);
				return true;
			}
			else if (key.Key == ConsoleKey.RightArrow && cursorPos < DisplayText.Length)
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
					// Leaving the draft position: stash what the user was writing so Down can bring it
					// back. Only their OWN text is worth stashing — an untouched history entry would
					// just shadow itself.
					StashDraftIfLeavingDraftPosition();
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
					// An edit made while browsing is the user's text too: stash it before moving on, so
					// walking down past it returns the edit rather than silently dropping it.
					StashDraftIfLeavingDraftPosition();
					_historyIndex++;
					if (_historyIndex < _history.Count)
					{
						ReplaceFromHistory(_history[_historyIndex]);
					}
					else
					{
						// Past the newest entry is the DRAFT position — restore the stash rather than
						// clearing the line, which is what made a stray Down destructive.
						RestoreDraft();
					}
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
				AddHistory(ValueText, _segments.ToArray());

			Core.AsyncEvent.Raise(Entered, EnteredAsync, this, ValueText, Container?.GetConsoleWindowSystem?.LogService);

			if (UnfocusOnEnter)
			{
				_cursorPosition = 0;
				this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Keyboard);
			}
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>Swaps the value for a history entry and parks the caret at its end.</summary>
		private void ReplaceFromHistory(IReadOnlyList<PromptSegment> entry)
		{
			SetSegmentsClamped(entry);
			ClearSelection();
			InvalidateWrapCache();
			MoveCursorTo(DisplayText.Length);
			RaiseInputChanged();
			// AFTER the notification: RaiseInputChanged marks the buffer a draft for every ordinary
			// mutation, but this one MIRRORS a history entry rather than being the user's own text, so
			// a further Up has nothing worth stashing.
			_bufferIsDraft = false;
		}

		/// <summary>
		/// Stashes the buffer as the pending draft when the user is about to leave the draft position
		/// (or an entry they have edited). A no-op for an untouched history entry — that is what makes
		/// browsing on from a loaded entry work, where a "buffer is non-empty" guard would refuse.
		/// </summary>
		private void StashDraftIfLeavingDraftPosition()
		{
			if (!_bufferIsDraft)
				return;

			// Segments, not display text: a token in an unsent draft has to come back as a token,
			// or walking into history and back out would silently turn it into its own label.
			_draftStash = _segments.ToArray();
			_draftStashCursor = _cursorPosition;
			// The edit has been banked; the buffer about to be loaded is a history entry again.
			_bufferIsDraft = false;
		}

		/// <summary>
		/// Restores the stashed draft (text and caret) at the draft position past the newest entry.
		/// With nothing stashed this yields an empty line — the same result the old unconditional
		/// clear produced, which is correct when the user had typed nothing.
		/// </summary>
		private void RestoreDraft()
		{
			IReadOnlyList<PromptSegment> entry = _draftStash ?? Array.Empty<PromptSegment>();
			int cursor = _draftStash != null ? _draftStashCursor : 0;
			int length = ProjectSegments(entry, label: true).Length;

			SetSegmentsClamped(entry);
			_draftStash = null;
			ClearSelection();
			InvalidateWrapCache();
			MoveCursorTo(cursor);
			RaiseInputChanged();
			// AFTER the notification, for the same reason as ReplaceFromHistory. Restored text is the
			// user's own again (so a subsequent Up re-stashes it), but an empty line is not a draft.
			_bufferIsDraft = length > 0;
		}

		private bool ProcessTabCompletion(int cursorPos)
		{
			if (ReadOnly) return false;

			var completions = _tabCompleter!(DisplayText, cursorPos)?.ToList();
			if (completions == null || completions.Count == 0)
				return false; // no matches — let focus leave

			if (completions.Count == 1)
			{
				if (completions[0] == DisplayText)
					return false; // already complete — let focus leave
				SetDisplayText(ApplyMaxLength(completions[0]));
				// Clear the selection, as the Input setter and ReplaceFromHistory already do when they
				// replace the text wholesale: the anchor points into the string just discarded, and
				// MoveCursorTo clamps only the CURSOR, so a stale anchor past the new length would drive
				// the next DeleteSelection out of range.
				ClearSelection();
				InvalidateWrapCache();
				MoveCursorTo(DisplayText.Length);
				RaiseInputChanged();
				return true;
			}

			// Multiple completions: find common prefix and insert it
			var prefix = CommonPrefix(completions);
			if (prefix.Length > DisplayText.Length)
			{
				SetDisplayText(ApplyMaxLength(prefix));
				ClearSelection(); // same reason as the single-completion branch above
				InvalidateWrapCache();
				MoveCursorTo(DisplayText.Length);
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
			int idx = DisplayText.LastIndexOf('\n', Math.Min(pos - 1, DisplayText.Length - 1));
			return idx < 0 ? 0 : idx + 1;
		}

		/// <summary>End of the logical (newline-delimited) line containing <paramref name="pos"/>.</summary>
		private int LogicalLineEnd(int pos)
		{
			if (!_multiline) return DisplayText.Length;
			int idx = DisplayText.IndexOf('\n', Math.Clamp(pos, 0, DisplayText.Length));
			return idx < 0 ? DisplayText.Length : idx;
		}
	}
}
