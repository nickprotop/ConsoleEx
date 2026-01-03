// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Represents a text position (line and column).
	/// </summary>
	/// <param name="Line">The zero-based line number.</param>
	/// <param name="Column">The zero-based column number.</param>
	public record TextPosition(int Line, int Column)
	{
		/// <summary>
		/// Represents position (0, 0) - the origin position.
		/// </summary>
		public static readonly TextPosition Zero = new(0, 0);

		/// <summary>
		/// Returns true if this position is before the other position
		/// </summary>
		public bool IsBefore(TextPosition other)
		{
			return Line < other.Line || (Line == other.Line && Column < other.Column);
		}

		/// <summary>
		/// Returns true if this position is after the other position
		/// </summary>
		public bool IsAfter(TextPosition other)
		{
			return Line > other.Line || (Line == other.Line && Column > other.Column);
		}
	}

	/// <summary>
	/// Represents a text selection (start and end positions)
	/// </summary>
	public record TextSelection(TextPosition Start, TextPosition End)
	{
		/// <summary>
		/// Empty/no selection
		/// </summary>
		public static readonly TextSelection Empty = new(TextPosition.Zero, TextPosition.Zero);

		/// <summary>
		/// Gets whether this selection is empty
		/// </summary>
		public bool IsEmpty => Start == End;

		/// <summary>
		/// Gets the normalized start (earlier position)
		/// </summary>
		public TextPosition NormalizedStart => Start.IsBefore(End) ? Start : End;

		/// <summary>
		/// Gets the normalized end (later position)
		/// </summary>
		public TextPosition NormalizedEnd => Start.IsBefore(End) ? End : Start;
	}

	/// <summary>
	/// Reason for an edit state change
	/// </summary>
	public enum EditChangeReason
	{
		/// <summary>Changed programmatically</summary>
		Programmatic,
		/// <summary>Changed via keyboard input</summary>
		Keyboard,
		/// <summary>Changed via mouse</summary>
		Mouse,
		/// <summary>Changed via paste operation</summary>
		Paste,
		/// <summary>Changed via undo/redo</summary>
		UndoRedo
	}

	/// <summary>
	/// Immutable record representing the edit state of a text control
	/// </summary>
	public record EditState
	{
		/// <summary>
		/// Current cursor position
		/// </summary>
		public TextPosition CursorPosition { get; init; } = TextPosition.Zero;

		/// <summary>
		/// Current selection (if any)
		/// </summary>
		public TextSelection Selection { get; init; } = TextSelection.Empty;

		/// <summary>
		/// Whether there is an active selection
		/// </summary>
		public bool HasSelection { get; init; }

		/// <summary>
		/// Whether the control is in edit mode
		/// </summary>
		public bool IsEditing { get; init; }

		/// <summary>
		/// Whether the control is read-only
		/// </summary>
		public bool IsReadOnly { get; init; }

		/// <summary>
		/// Vertical scroll offset
		/// </summary>
		public int VerticalScrollOffset { get; init; }

		/// <summary>
		/// Horizontal scroll offset
		/// </summary>
		public int HorizontalScrollOffset { get; init; }

		/// <summary>
		/// Number of lines in the content
		/// </summary>
		public int LineCount { get; init; }

		/// <summary>
		/// Timestamp when this state was created
		/// </summary>
		public DateTime UpdateTime { get; init; }

		/// <summary>
		/// Creates a new edit state
		/// </summary>
		public EditState()
		{
			UpdateTime = DateTime.UtcNow;
		}

		/// <summary>
		/// Empty edit state
		/// </summary>
		public static readonly EditState Empty = new();

		/// <summary>
		/// Gets scroll position as a Point (horizontal, vertical)
		/// </summary>
		public Point ScrollPosition => new(HorizontalScrollOffset, VerticalScrollOffset);
	}

	/// <summary>
	/// Represents an undoable edit operation
	/// </summary>
	public record EditOperation
	{
		/// <summary>
		/// Type of operation
		/// </summary>
		public EditOperationType Type { get; init; }

		/// <summary>
		/// Position where the operation occurred
		/// </summary>
		public TextPosition Position { get; init; } = TextPosition.Zero;

		/// <summary>
		/// Text that was inserted or deleted
		/// </summary>
		public string Text { get; init; } = string.Empty;

		/// <summary>
		/// For replacement operations, the text that was replaced
		/// </summary>
		public string? ReplacedText { get; init; }

		/// <summary>
		/// Selection state before the operation
		/// </summary>
		public TextSelection? SelectionBefore { get; init; }

		/// <summary>
		/// Cursor position before the operation
		/// </summary>
		public TextPosition? CursorBefore { get; init; }

		/// <summary>
		/// Timestamp of the operation
		/// </summary>
		public DateTime Timestamp { get; init; } = DateTime.UtcNow;
	}

	/// <summary>
	/// Types of edit operations for undo/redo
	/// </summary>
	public enum EditOperationType
	{
		/// <summary>Text was inserted</summary>
		Insert,
		/// <summary>Text was deleted</summary>
		Delete,
		/// <summary>Text was replaced (selection + insert)</summary>
		Replace
	}

	/// <summary>
	/// Event arguments for edit state changes
	/// </summary>
	public class EditStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The control whose edit state changed
		/// </summary>
		public IWindowControl Control { get; }

		/// <summary>
		/// The previous edit state
		/// </summary>
		public EditState PreviousState { get; }

		/// <summary>
		/// The new edit state
		/// </summary>
		public EditState NewState { get; }

		/// <summary>
		/// The reason for the change
		/// </summary>
		public EditChangeReason Reason { get; }

		/// <summary>
		/// Whether the cursor position changed
		/// </summary>
		public bool CursorChanged => PreviousState.CursorPosition != NewState.CursorPosition;

		/// <summary>
		/// Whether the selection changed
		/// </summary>
		public bool SelectionChanged => PreviousState.Selection != NewState.Selection ||
		                                 PreviousState.HasSelection != NewState.HasSelection;

		/// <summary>
		/// Whether the scroll position changed
		/// </summary>
		public bool ScrollChanged => PreviousState.VerticalScrollOffset != NewState.VerticalScrollOffset ||
		                              PreviousState.HorizontalScrollOffset != NewState.HorizontalScrollOffset;

		/// <summary>
		/// Initializes a new instance of the <see cref="EditStateChangedEventArgs"/> class.
		/// </summary>
		/// <param name="control">The control whose edit state changed.</param>
		/// <param name="previousState">The previous edit state before the change.</param>
		/// <param name="newState">The new edit state after the change.</param>
		/// <param name="reason">The reason for the edit state change.</param>
		public EditStateChangedEventArgs(IWindowControl control, EditState previousState, EditState newState, EditChangeReason reason)
		{
			Control = control;
			PreviousState = previousState;
			NewState = newState;
			Reason = reason;
		}
	}

	/// <summary>
	/// Event arguments for text changes
	/// </summary>
	public class TextChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The control whose text changed
		/// </summary>
		public IWindowControl Control { get; }

		/// <summary>
		/// The operation that caused the change
		/// </summary>
		public EditOperation Operation { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TextChangedEventArgs"/> class.
		/// </summary>
		/// <param name="control">The control whose text changed.</param>
		/// <param name="operation">The edit operation that caused the change.</param>
		public TextChangedEventArgs(IWindowControl control, EditOperation operation)
		{
			Control = control;
			Operation = operation;
		}
	}
}
