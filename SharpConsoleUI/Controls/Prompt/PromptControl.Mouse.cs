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

		/// <summary>The only region this control has: its text.</summary>
		private enum PromptGestureRegion
		{
			Text,
		}

		/// <summary>
		/// Owns the press-to-release gesture, so a drag stays a drag.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A bare <c>_dragging</c> flag plus a per-frame reading of the mouse
		/// flags was not enough, because not every frame between a press and a
		/// release names the button. SGR reports motion with nothing held as base
		/// button 3, which the parser names no button for, so such a frame carries
		/// <c>ReportMousePosition</c> alone - and a drag that only tested that
		/// followed the pointer with no button down, collapsing the selection the
		/// moment the mouse paused.
		/// </para>
		/// <para>
		/// <see cref="MouseGestureCapture{TRegion}"/> answers the question the
		/// flags cannot: it captures on the press and reports every later frame as
		/// <c>Move</c> until a real release arrives, whatever that frame happens to
		/// carry. This is the mechanism <c>MultilineEditControl</c> already uses,
		/// where mouse selection works.
		/// </para>
		/// </remarks>
		private readonly MouseGestureCapture<PromptGestureRegion> _gesture = new();

		/// <inheritdoc/>
		public bool ProcessMouseEvent(Events.MouseEventArgs args)
		{
			if (!IsEnabled) return false;

			// These six events are part of the control's public surface. They were declared and never
			// raised, which made every handler an application wired up dead code; raise them here.
			if (args.HasFlag(Drivers.MouseFlags.MouseEnter)) MouseEnter?.Invoke(this, args);
			if (args.HasFlag(Drivers.MouseFlags.MouseLeave)) MouseLeave?.Invoke(this, args);
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

			// Button1 gesture: press captures, every frame until a real release is a
			// move, and only a release ends it. Routing by PHASE rather than by the
			// flags on each frame is what makes a drag survive a pause: a frame that
			// names no button still routes as Move, because the capture - not the
			// frame - knows the button is down.
			if (args.HasAnyFlag(Drivers.MouseFlags.Button1Pressed, Drivers.MouseFlags.Button1Dragged,
				Drivers.MouseFlags.Button1Released, Drivers.MouseFlags.Button1Clicked)
				|| _gesture.IsCapturing)
			{
				var route = _gesture.Route(args, _ => PromptGestureRegion.Text);

				// A click with no captured press - some drivers and tests deliver one
				// without a separate press. Collapse it into press-then-release so the
				// caret still lands where it was clicked.
				if (route.Phase == GesturePhase.None && args.HasFlag(Drivers.MouseFlags.Button1Clicked))
				{
					BeginSelectionAt(args);
					args.Handled = true;
					return true;
				}

				switch (route.Phase)
				{
					case GesturePhase.Down:
						BeginSelectionAt(args);
						args.Handled = true;
						return true;

					case GesturePhase.Move:
						// Extend to wherever the pointer is now. The anchor set by the
						// press stays put, so the span grows and shrinks with the mouse.
						MoveCursorTo(PositionFromPoint(args.Position));
						args.Handled = true;
						return true;

					case GesturePhase.Up:
						// The selection the drag produced is left exactly as it is.
						return false;
				}
			}

			return false;
		}

		/// <summary>
		/// Takes focus, anchors a fresh selection where the press landed, and
		/// tells the host if the press was on a token.
		/// </summary>
		/// <remarks>
		/// The anchor is a position, not a start: the drag that follows extends it
		/// by moving the caret, which is what makes click-drag select, and a press
		/// that never moves leaves anchor and caret together, which is no
		/// selection at all.
		/// </remarks>
		private void BeginSelectionAt(Events.MouseEventArgs args)
		{
			if (!HasFocus && CanFocusWithMouse)
				this.GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

			int pos = PositionFromPoint(args.Position);

			// A click that lands on a token tells the host and goes no further. The control does
			// not decide what activation means: expanding, previewing or ignoring are all the
			// host's to choose, so the token is left exactly as it was.
			// The half-open span [start, end), not the caret-interior test: a click on the label's
			// leftmost cell is a click on the chip, and a one-column label has no interior at
			// all, so the interior test would make such a chip permanently unclickable.
			if (TryGetTokenSpanCoveringIndex(pos, out int tokenStart, out int tokenLength, out PromptToken clicked))
			{
				RaiseTokenActivated(clicked);

				// The caret still has to go somewhere legal, and it may never rest inside a
				// label. The nearer edge is the one the user was reaching for.
				pos = pos - tokenStart < tokenLength - (pos - tokenStart)
					? tokenStart
					: tokenStart + tokenLength;
			}

			_selectionAnchor = pos;
			_cursorPosition = pos;
			Invalidate(Invalidation.Relayout);

			MouseClick?.Invoke(this, args);
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
				int scrollColumn = UnicodeWidth.CharOffsetToColumn(DisplayText, _horizontalScrollOffset);
				int column = Math.Max(0, clickColumn + scrollColumn);
				return UnicodeWidth.ColumnToCharOffset(DisplayText, column);
			}

			var rows = GetWrappedRows(LastWrapWidth);
			if (rows.Count == 0) return 0;

			int rowIndex = Math.Clamp(point.Y - Margin.Top + _verticalScrollOffset, 0, rows.Count - 1);
			var row = rows[rowIndex];
			// Every row carries the same hanging indent under the prompt, so the mapping is uniform.
			int col = Math.Max(0, point.X - Margin.Left - _lastAlignOffset - promptLength);

			string rowText = DisplayText.Substring(row.Offset, row.Length);
			int within = UnicodeWidth.ColumnToCharOffset(rowText, col);
			return Math.Clamp(row.Offset + within, 0, DisplayText.Length);
		}

		/// <summary>Selects the word under <paramref name="pos"/>, or the whitespace run if that is what is there.</summary>
		private void SelectWordAt(int pos)
		{
			if (DisplayText.Length == 0) return;
			pos = Math.Clamp(pos, 0, DisplayText.Length - 1);

			bool inWhitespace = char.IsWhiteSpace(DisplayText[pos]);
			int start = pos, end = pos;
			while (start > 0 && char.IsWhiteSpace(DisplayText[start - 1]) == inWhitespace) start--;
			while (end < DisplayText.Length && char.IsWhiteSpace(DisplayText[end]) == inWhitespace) end++;

			_selectionAnchor = start;
			MoveCursorTo(end);
		}

		#endregion
	}
}
