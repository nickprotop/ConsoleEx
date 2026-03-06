// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		#region Find/Replace State

		private string? _searchTerm;
		private bool _searchCaseSensitive;
		private bool _searchUseRegex;
		private List<SearchMatch> _searchMatches = new();
		private int _currentMatchIndex = -1;

		private readonly record struct SearchMatch(
			int Line,
			int StartCol,
			int Length
		);

		#endregion

		#region Find/Replace Events

		/// <summary>
		/// Occurs when the search match count changes (after Find, Replace, ReplaceAll, or content changes).
		/// Event argument is the new match count.
		/// </summary>
		public event EventHandler<int>? MatchCountChanged;

		#endregion

		#region Find/Replace Properties

		/// <summary>
		/// Gets the number of search matches found.
		/// </summary>
		public int MatchCount => _searchMatches.Count;

		/// <summary>
		/// Gets the 0-based index of the current match, or -1 if no current match.
		/// </summary>
		public int CurrentMatchIndex => _currentMatchIndex;

		/// <summary>
		/// Gets the current search term, or null if no search is active.
		/// </summary>
		public string? SearchTerm => _searchTerm;

		/// <summary>
		/// Gets whether search highlighting is currently active.
		/// </summary>
		public bool HasActiveSearch => _searchTerm != null && _searchMatches.Count > 0;

		#endregion

		#region Find/Replace Public API

		/// <summary>
		/// Searches the content for all occurrences of the given term.
		/// Highlights all matches and navigates to the first match at or after the cursor position.
		/// </summary>
		/// <param name="term">The text to search for.</param>
		/// <param name="caseSensitive">Whether the search is case-sensitive.</param>
		/// <param name="useRegex">Whether to treat the term as a regular expression.</param>
		/// <returns>The number of matches found.</returns>
		public int Find(string term, bool caseSensitive = false, bool useRegex = false)
		{
			if (string.IsNullOrEmpty(term))
			{
				ClearFind();
				return 0;
			}

			_searchTerm = term;
			_searchCaseSensitive = caseSensitive;
			_searchUseRegex = useRegex;

			int oldCount = _searchMatches.Count;
			ComputeMatches();
			int newCount = _searchMatches.Count;

			if (_searchMatches.Count > 0)
			{
				// Jump to first match at or after cursor
				_currentMatchIndex = FindMatchAtOrAfterCursor();
				NavigateToCurrentMatch();
			}
			else
			{
				_currentMatchIndex = -1;
			}

			Container?.Invalidate(true);

			if (oldCount != newCount)
				MatchCountChanged?.Invoke(this, newCount);

			return _searchMatches.Count;
		}

		/// <summary>
		/// Navigates to the next search match. Wraps around to the beginning.
		/// </summary>
		/// <returns>True if a next match was found, false if no search is active.</returns>
		public bool FindNext()
		{
			if (_searchMatches.Count == 0) return false;

			_currentMatchIndex = (_currentMatchIndex + 1) % _searchMatches.Count;
			NavigateToCurrentMatch();
			Container?.Invalidate(true);
			return true;
		}

		/// <summary>
		/// Navigates to the previous search match. Wraps around to the end.
		/// </summary>
		/// <returns>True if a previous match was found, false if no search is active.</returns>
		public bool FindPrevious()
		{
			if (_searchMatches.Count == 0) return false;

			_currentMatchIndex = (_currentMatchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
			NavigateToCurrentMatch();
			Container?.Invalidate(true);
			return true;
		}

		/// <summary>
		/// Replaces the current match with the given text and advances to the next match.
		/// </summary>
		/// <param name="replacement">The replacement text.</param>
		/// <returns>True if a replacement was made, false if no current match or read-only.</returns>
		public bool Replace(string replacement)
		{
			if (_readOnly || _currentMatchIndex < 0 || _currentMatchIndex >= _searchMatches.Count)
				return false;

			replacement ??= string.Empty;
			replacement = SanitizeInputText(replacement);

			var match = _searchMatches[_currentMatchIndex];

			lock (_contentLock)
			{
				BeginUndoAction();

				// Validate match is still valid
				if (match.Line >= _lines.Count || match.StartCol + match.Length > _lines[match.Line].Length)
				{
					// Match is stale — recompute
					ComputeMatches();
					return false;
				}

				// Check MaxLength
				int lengthDelta = replacement.Length - match.Length;
				if (lengthDelta > 0 && GetRemainingCapacity() < lengthDelta)
					return false;

				_lines[match.Line] = _lines[match.Line].Remove(match.StartCol, match.Length)
					.Insert(match.StartCol, replacement);
				_cursorX = match.StartCol + replacement.Length;
				_cursorY = match.Line;

				InvalidateSyntaxFromLine(match.Line);
				InvalidateWrappedLinesCache();
				CommitUndoAction();
			}

			// Recompute matches after replacement
			int oldCount = _searchMatches.Count;
			ComputeMatches();
			int newCount = _searchMatches.Count;

			// Advance to next match (index stays same since the replaced match is gone)
			if (_searchMatches.Count > 0)
			{
				_currentMatchIndex = Math.Min(_currentMatchIndex, _searchMatches.Count - 1);
				// Find the next match at or after the replacement position
				_currentMatchIndex = FindMatchAtOrAfterCursor();
			}
			else
			{
				_currentMatchIndex = -1;
			}

			EnsureCursorVisible();
			Container?.Invalidate(true);
			ContentChanged?.Invoke(this, GetContent());

			if (oldCount != newCount)
				MatchCountChanged?.Invoke(this, newCount);

			return true;
		}

		/// <summary>
		/// Replaces all search matches with the given text.
		/// </summary>
		/// <param name="replacement">The replacement text.</param>
		/// <returns>The number of replacements made.</returns>
		public int ReplaceAll(string replacement)
		{
			if (_readOnly || _searchMatches.Count == 0)
				return 0;

			replacement ??= string.Empty;
			replacement = SanitizeInputText(replacement);

			int replacementCount;

			lock (_contentLock)
			{
				BeginUndoAction();

				// Replace in reverse order to preserve positions
				var matchesByLine = _searchMatches
					.OrderByDescending(m => m.Line)
					.ThenByDescending(m => m.StartCol)
					.ToList();

				replacementCount = matchesByLine.Count;
				int lowestLine = int.MaxValue;

				foreach (var match in matchesByLine)
				{
					if (match.Line >= _lines.Count || match.StartCol + match.Length > _lines[match.Line].Length)
						continue;

					_lines[match.Line] = _lines[match.Line].Remove(match.StartCol, match.Length)
						.Insert(match.StartCol, replacement);

					if (match.Line < lowestLine) lowestLine = match.Line;
				}

				if (lowestLine < int.MaxValue)
					InvalidateSyntaxFromLine(lowestLine);
				InvalidateWrappedLinesCache();
				CommitUndoAction();
			}

			// Recompute matches (should be 0 unless replacement contains search term)
			int oldCount = _searchMatches.Count;
			ComputeMatches();
			int newCount = _searchMatches.Count;

			_currentMatchIndex = _searchMatches.Count > 0 ? 0 : -1;

			EnsureCursorVisible();
			Container?.Invalidate(true);
			ContentChanged?.Invoke(this, GetContent());

			if (oldCount != newCount)
				MatchCountChanged?.Invoke(this, newCount);

			return replacementCount;
		}

		/// <summary>
		/// Clears the current search, removing all match highlighting.
		/// </summary>
		public void ClearFind()
		{
			bool hadSearch = _searchTerm != null;
			int oldCount = _searchMatches.Count;

			_searchTerm = null;
			_searchMatches.Clear();
			_currentMatchIndex = -1;

			if (hadSearch)
			{
				Container?.Invalidate(true);
				if (oldCount > 0)
					MatchCountChanged?.Invoke(this, 0);
			}
		}

		#endregion

		#region Find/Replace Internals

		private void ComputeMatches()
		{
			_searchMatches.Clear();

			if (string.IsNullOrEmpty(_searchTerm))
				return;

			lock (_contentLock)
			{
				if (_searchUseRegex)
				{
					ComputeRegexMatches();
				}
				else
				{
					ComputeTextMatches();
				}
			}
		}

		private void ComputeTextMatches()
		{
			var comparison = _searchCaseSensitive
				? StringComparison.Ordinal
				: StringComparison.OrdinalIgnoreCase;

			for (int lineIdx = 0; lineIdx < _lines.Count; lineIdx++)
			{
				string line = _lines[lineIdx];
				int searchFrom = 0;
				while (searchFrom <= line.Length - _searchTerm!.Length)
				{
					int found = line.IndexOf(_searchTerm, searchFrom, comparison);
					if (found < 0) break;

					_searchMatches.Add(new SearchMatch(lineIdx, found, _searchTerm.Length));
					searchFrom = found + 1; // Allow overlapping matches
				}
			}
		}

		private void ComputeRegexMatches()
		{
			try
			{
				var options = RegexOptions.None;
				if (!_searchCaseSensitive)
					options |= RegexOptions.IgnoreCase;

				var regex = new Regex(_searchTerm!, options);

				for (int lineIdx = 0; lineIdx < _lines.Count; lineIdx++)
				{
					var matches = regex.Matches(_lines[lineIdx]);
					foreach (Match m in matches)
					{
						if (m.Length > 0)
							_searchMatches.Add(new SearchMatch(lineIdx, m.Index, m.Length));
					}
				}
			}
			catch (RegexParseException)
			{
				// Invalid regex — no matches
			}
		}

		/// <summary>
		/// Finds the index of the first match at or after the current cursor position.
		/// If no match is found after cursor, wraps to 0.
		/// </summary>
		private int FindMatchAtOrAfterCursor()
		{
			for (int i = 0; i < _searchMatches.Count; i++)
			{
				var m = _searchMatches[i];
				if (m.Line > _cursorY || (m.Line == _cursorY && m.StartCol >= _cursorX))
					return i;
			}
			return 0; // Wrap to first match
		}

		private void NavigateToCurrentMatch()
		{
			if (_currentMatchIndex < 0 || _currentMatchIndex >= _searchMatches.Count)
				return;

			var match = _searchMatches[_currentMatchIndex];
			lock (_contentLock)
			{
				_cursorY = match.Line;
				_cursorX = match.StartCol;

				// Select the current match
				_hasSelection = true;
				_selectionStartX = match.StartCol;
				_selectionStartY = match.Line;
				_selectionEndX = match.StartCol + match.Length;
				_selectionEndY = match.Line;
			}

			EnsureCursorVisible();
			CursorPositionChanged?.Invoke(this, (CurrentLine, CurrentColumn));
		}

		/// <summary>
		/// Called after content changes to recompute search matches if a search is active.
		/// </summary>
		internal void RefreshSearchMatches()
		{
			if (_searchTerm == null) return;

			int oldCount = _searchMatches.Count;
			ComputeMatches();
			int newCount = _searchMatches.Count;

			if (_searchMatches.Count > 0)
			{
				_currentMatchIndex = Math.Clamp(_currentMatchIndex, 0, _searchMatches.Count - 1);
			}
			else
			{
				_currentMatchIndex = -1;
			}

			if (oldCount != newCount)
				MatchCountChanged?.Invoke(this, newCount);
		}

		/// <summary>
		/// Tests whether a character position falls within any search match.
		/// Returns (isMatch, isCurrentMatch) for rendering.
		/// </summary>
		internal (bool isMatch, bool isCurrentMatch) GetSearchMatchState(int lineIndex, int charIndex)
		{
			if (_searchMatches.Count == 0) return (false, false);

			for (int i = 0; i < _searchMatches.Count; i++)
			{
				var m = _searchMatches[i];
				if (m.Line != lineIndex) continue;
				if (charIndex >= m.StartCol && charIndex < m.StartCol + m.Length)
					return (true, i == _currentMatchIndex);
			}
			return (false, false);
		}

		#endregion
	}
}
