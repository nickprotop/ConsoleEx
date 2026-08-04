// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class PromptControl
	{
		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => CanReceiveFocus;

		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseClick;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseDoubleClick;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseRightClick;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseEnter;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseLeave;
		/// <inheritdoc/>
		public event EventHandler<Events.MouseEventArgs>? MouseMove;

		// True between press and release, so a drag extends the selection started by the press.
		private bool _dragging;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(Events.MouseEventArgs args)
		{
			if (!IsEnabled) return false;

			// These six events are part of the control's public surface. They were declared and never
			// raised, which made every handler an application wired up dead code; raise them here.
			if (args.HasFlag(Drivers.MouseFlags.MouseEnter)) MouseEnter?.Invoke(this, args);
			if (args.HasFlag(Drivers.MouseFlags.MouseLeave)) { _dragging = false; MouseLeave?.Invoke(this, args); }
			if (args.HasFlag(Drivers.MouseFlags.ReportMousePosition)) MouseMove?.Invoke(this, args);

			if (args.HasFlag(Drivers.MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(Drivers.MouseFlags.Button1DoubleClicked))
			{
				int pos = PositionFromPoint(args.Position);
				SelectWordAt(pos);
				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			// Drag extends the selection anchored by the press that started it.
			if (_dragging && args.HasFlag(Drivers.MouseFlags.ReportMousePosition) && !args.HasFlag(Drivers.MouseFlags.Button1Released))
			{
				int pos = PositionFromPoint(args.Position);
				MoveCursorTo(pos);
				args.Handled = true;
				return true;
			}

			if (args.HasFlag(Drivers.MouseFlags.Button1Released))
			{
				_dragging = false;
				return false;
			}

			// Focus on click
			if (args.HasFlag(Drivers.MouseFlags.Button1Clicked) ||
				args.HasFlag(Drivers.MouseFlags.Button1Pressed))
			{
				if (!HasFocus && CanFocusWithMouse)
					this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

				int pos = PositionFromPoint(args.Position);

				// A press starts a fresh selection anchored where it landed; the drag above then
				// extends it by moving the caret, which is what makes click-drag select.
				_selectionAnchor = pos;
				_dragging = true;
				_cursorPosition = pos;
				Invalidate(Invalidation.Relayout);

				MouseClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Maps a control-relative point to a character index in the value, in DISPLAY columns —
		/// clicking the second half of a wide character lands on that character, not past it.
		/// </summary>
		private int PositionFromPoint(System.Drawing.Point point)
		{
			int promptLength = Parsing.MarkupParser.StripLength(_prompt ?? string.Empty);

			if (!_multiline)
			{
				int clickColumn = point.X - Margin.Left - _lastAlignOffset - promptLength;
				int scrollColumn = UnicodeWidth.CharOffsetToColumn(_input, _horizontalScrollOffset);
				int column = Math.Max(0, clickColumn + scrollColumn);
				return UnicodeWidth.ColumnToCharOffset(_input, column);
			}

			var rows = GetWrappedRows(LastWrapWidth);
			if (rows.Count == 0) return 0;

			int rowIndex = Math.Clamp(point.Y - Margin.Top + _verticalScrollOffset, 0, rows.Count - 1);
			var row = rows[rowIndex];
			// Every row carries the same hanging indent under the prompt, so the mapping is uniform.
			int col = Math.Max(0, point.X - Margin.Left - _lastAlignOffset - promptLength);

			string rowText = _input.Substring(row.Offset, row.Length);
			int within = UnicodeWidth.ColumnToCharOffset(rowText, col);
			return Math.Clamp(row.Offset + within, 0, _input.Length);
		}

		/// <summary>Selects the word under <paramref name="pos"/>, or the whitespace run if that is what is there.</summary>
		private void SelectWordAt(int pos)
		{
			if (_input.Length == 0) return;
			pos = Math.Clamp(pos, 0, _input.Length - 1);

			bool inWhitespace = char.IsWhiteSpace(_input[pos]);
			int start = pos, end = pos;
			while (start > 0 && char.IsWhiteSpace(_input[start - 1]) == inWhitespace) start--;
			while (end < _input.Length && char.IsWhiteSpace(_input[end]) == inWhitespace) end++;

			_selectionAnchor = start;
			MoveCursorTo(end);
		}

		#endregion
	}
}
