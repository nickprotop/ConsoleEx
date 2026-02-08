// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Triple-click: select entire line
			if (args.HasFlag(MouseFlags.Button1TripleClicked))
			{
				if (_hasFocus)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					_hasSelection = true;
					_selectionStartX = 0;
					_selectionStartY = _cursorY;
					_selectionEndX = _lines[_cursorY].Length;
					_selectionEndY = _cursorY;
					_cursorX = _lines[_cursorY].Length;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Double-click: select word
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				if (_hasFocus)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					var (wordStart, wordEnd) = WordBoundaryHelper.FindWordAt(_lines[_cursorY], _cursorX);
					_hasSelection = wordStart != wordEnd;
					_selectionStartX = wordStart;
					_selectionStartY = _cursorY;
					_selectionEndX = wordEnd;
					_selectionEndY = _cursorY;
					_cursorX = wordEnd;
					Container?.Invalidate(true);
				}
				MouseDoubleClick?.Invoke(this, args);
				return true;
			}

			// Mouse button pressed: start potential drag
			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				if (_hasFocus && !_readOnly)
				{
					IsEditing = true;
					PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
					_hasSelection = true;
					_selectionStartX = _cursorX;
					_selectionStartY = _cursorY;
					_selectionEndX = _cursorX;
					_selectionEndY = _cursorY;
					_isDragging = true;
					EnsureCursorVisible();
					Container?.Invalidate(true);
				}
				return true;
			}

			// Single click
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				if (_hasFocus && !_readOnly)
				{
					IsEditing = true;
					PositionCursorFromMouse(args.Position.X, args.Position.Y);
				}
				_isDragging = false;
				MouseClick?.Invoke(this, args);
				return true;
			}

			// Mouse button released: end drag
			if (args.HasFlag(MouseFlags.Button1Released))
			{
				_isDragging = false;
				return true;
			}

			// Mouse drag: extend selection
			if (args.HasFlag(MouseFlags.ReportMousePosition) && _isDragging)
			{
				PositionCursorFromMouseCore(args.Position.X, args.Position.Y);
				_selectionEndX = _cursorX;
				_selectionEndY = _cursorY;
				_hasSelection = (_selectionStartX != _selectionEndX || _selectionStartY != _selectionEndY);
				EnsureCursorVisible();
				Container?.Invalidate(true);
				return true;
			}

			// Mouse wheel up
			if (args.HasFlag(MouseFlags.WheeledUp))
			{
				int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, _verticalScrollOffset);
				if (scrollAmount > 0)
				{
					_skipUpdateScrollPositionsInRender = true;
					_verticalScrollOffset -= scrollAmount;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Mouse wheel down
			if (args.HasFlag(MouseFlags.WheeledDown))
			{
				int totalLines = GetTotalWrappedLineCount();
				int maxScroll = Math.Max(0, totalLines - GetEffectiveViewportHeight());
				int scrollAmount = Math.Min(ControlDefaults.DefaultScrollWheelLines, maxScroll - _verticalScrollOffset);
				if (scrollAmount > 0)
				{
					_skipUpdateScrollPositionsInRender = true;
					_verticalScrollOffset += scrollAmount;
					Container?.Invalidate(true);
				}
				return true;
			}

			// Regular mouse move (no drag)
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return false;
			}

			return false;
		}

		/// <summary>
		/// Core positioning: maps control-relative mouse coordinates to cursor position.
		/// Does NOT clear selection or invalidate — caller decides.
		/// </summary>
		private void PositionCursorFromMouseCore(int mouseX, int mouseY)
		{
			int relX = mouseX - _margin.Left - GetGutterWidth();
			int relY = mouseY - _margin.Top;

			if (relX < 0) relX = 0;
			if (relY < 0) relY = 0;

			if (_wrapMode == WrapMode.NoWrap)
			{
				_cursorY = Math.Min(_lines.Count - 1, relY + _verticalScrollOffset);
				_cursorX = Math.Min(_lines[_cursorY].Length, relX + _horizontalScrollOffset);
			}
			else
			{
				var wrappedLines = GetWrappedLines(SafeEffectiveWidth);
				int wrappedIndex = Math.Clamp(relY + _verticalScrollOffset, 0, wrappedLines.Count - 1);

				var wl = wrappedLines[wrappedIndex];
				_cursorY = wl.SourceLineIndex;
				_cursorX = Math.Min(wl.SourceCharOffset + relX, wl.SourceCharOffset + wl.Length);
				_cursorX = Math.Min(_cursorX, _lines[_cursorY].Length);
			}
		}

		/// <summary>
		/// Maps control-relative mouse coordinates to cursor position, clears selection.
		/// Used for simple click positioning.
		/// </summary>
		private void PositionCursorFromMouse(int mouseX, int mouseY)
		{
			PositionCursorFromMouseCore(mouseX, mouseY);
			ClearSelection();
			EnsureCursorVisible();
			Container?.Invalidate(true);
		}
	}
}
