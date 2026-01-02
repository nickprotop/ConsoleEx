// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Centralized service for managing edit state in text controls.
	/// Provides cursor management, selection, and undo/redo support.
	/// </summary>
	public class EditStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ConcurrentDictionary<IWindowControl, EditState> _editStates = new();
		private readonly ConcurrentDictionary<IWindowControl, Stack<EditOperation>> _undoStacks = new();
		private readonly ConcurrentDictionary<IWindowControl, Stack<EditOperation>> _redoStacks = new();
		private readonly ConcurrentQueue<(IWindowControl Control, EditState State)> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private const int MaxUndoStackSize = 100;
		private bool _isDisposed;

		#region Events

		/// <summary>
		/// Event fired when any control's edit state changes
		/// </summary>
		public event EventHandler<EditStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Event fired when cursor position changes
		/// </summary>
		public event EventHandler<EditStateChangedEventArgs>? CursorMoved;

		/// <summary>
		/// Event fired when selection changes
		/// </summary>
		public event EventHandler<EditStateChangedEventArgs>? SelectionChanged;

		/// <summary>
		/// Event fired when text content changes
		/// </summary>
		public event EventHandler<TextChangedEventArgs>? TextChanged;

		#endregion

		#region State Management

		/// <summary>
		/// Gets the edit state for a control
		/// </summary>
		public EditState GetEditState(IWindowControl control)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			return _editStates.GetValueOrDefault(control, EditState.Empty);
		}

		/// <summary>
		/// Gets the cursor position for a control
		/// </summary>
		public TextPosition GetCursorPosition(IWindowControl control)
		{
			return GetEditState(control).CursorPosition;
		}

		/// <summary>
		/// Gets the selection for a control
		/// </summary>
		public TextSelection GetSelection(IWindowControl control)
		{
			return GetEditState(control).Selection;
		}

		/// <summary>
		/// Checks if a control has an active selection
		/// </summary>
		public bool HasSelection(IWindowControl control)
		{
			return GetEditState(control).HasSelection;
		}

		/// <summary>
		/// Sets the cursor position
		/// </summary>
		public void SetCursorPosition(IWindowControl control, int line, int column, EditChangeReason reason = EditChangeReason.Programmatic)
		{
			SetCursorPosition(control, new TextPosition(line, column), reason);
		}

		/// <summary>
		/// Sets the cursor position
		/// </summary>
		public void SetCursorPosition(IWindowControl control, TextPosition position, EditChangeReason reason = EditChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetEditState(control);

				if (previousState.CursorPosition == position)
					return;

				var newState = previousState with
				{
					CursorPosition = position,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireCursorMoved(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Sets the selection
		/// </summary>
		public void SetSelection(IWindowControl control, TextPosition start, TextPosition end, EditChangeReason reason = EditChangeReason.Programmatic)
		{
			SetSelection(control, new TextSelection(start, end), reason);
		}

		/// <summary>
		/// Sets the selection
		/// </summary>
		public void SetSelection(IWindowControl control, TextSelection selection, EditChangeReason reason = EditChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetEditState(control);
				bool hasSelection = !selection.IsEmpty;

				if (previousState.Selection == selection && previousState.HasSelection == hasSelection)
					return;

				var newState = previousState with
				{
					Selection = selection,
					HasSelection = hasSelection,
					CursorPosition = selection.End, // Cursor follows selection end
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireSelectionChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Clears the selection
		/// </summary>
		public void ClearSelection(IWindowControl control, EditChangeReason reason = EditChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetEditState(control);

				if (!previousState.HasSelection)
					return;

				var newState = previousState with
				{
					Selection = TextSelection.Empty,
					HasSelection = false,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireSelectionChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Sets the editing mode
		/// </summary>
		public void SetEditingMode(IWindowControl control, bool isEditing, EditChangeReason reason = EditChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetEditState(control);

				if (previousState.IsEditing == isEditing)
					return;

				var newState = previousState with
				{
					IsEditing = isEditing,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Sets the scroll position
		/// </summary>
		public void SetScrollPosition(IWindowControl control, int horizontalOffset, int verticalOffset, EditChangeReason reason = EditChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetEditState(control);

				if (previousState.HorizontalScrollOffset == horizontalOffset &&
				    previousState.VerticalScrollOffset == verticalOffset)
					return;

				var newState = previousState with
				{
					HorizontalScrollOffset = horizontalOffset,
					VerticalScrollOffset = verticalOffset,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Updates the line count
		/// </summary>
		public void SetLineCount(IWindowControl control, int lineCount)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetEditState(control);

				if (previousState.LineCount == lineCount)
					return;

				var newState = previousState with
				{
					LineCount = lineCount,
					UpdateTime = DateTime.UtcNow
				};

				_editStates[control] = newState;
			}
		}

		/// <summary>
		/// Removes all state for a control (when control is disposed)
		/// </summary>
		public void UnregisterControl(IWindowControl control)
		{
			if (control == null)
				return;

			_editStates.TryRemove(control, out _);
			_undoStacks.TryRemove(control, out _);
			_redoStacks.TryRemove(control, out _);
		}

		#endregion

		#region Undo/Redo

		/// <summary>
		/// Records an edit operation for undo
		/// </summary>
		public void RecordOperation(IWindowControl control, EditOperation operation)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var undoStack = _undoStacks.GetOrAdd(control, _ => new Stack<EditOperation>());

				undoStack.Push(operation);

				// Limit stack size
				while (undoStack.Count > MaxUndoStackSize)
				{
					// Remove oldest items by converting to array, skipping oldest, and rebuilding
					var items = undoStack.ToArray().Take(MaxUndoStackSize).Reverse().ToArray();
					undoStack.Clear();
					foreach (var item in items)
					{
						undoStack.Push(item);
					}
				}

				// Clear redo stack when new operation is recorded
				if (_redoStacks.TryGetValue(control, out var redoStack))
				{
					redoStack.Clear();
				}

				// Fire text changed event
				FireTextChanged(control, operation);
			}
		}

		/// <summary>
		/// Checks if undo is available
		/// </summary>
		public bool CanUndo(IWindowControl control)
		{
			return _undoStacks.TryGetValue(control, out var stack) && stack.Count > 0;
		}

		/// <summary>
		/// Checks if redo is available
		/// </summary>
		public bool CanRedo(IWindowControl control)
		{
			return _redoStacks.TryGetValue(control, out var stack) && stack.Count > 0;
		}

		/// <summary>
		/// Gets the next undo operation (without removing it)
		/// </summary>
		public EditOperation? PeekUndo(IWindowControl control)
		{
			if (_undoStacks.TryGetValue(control, out var stack) && stack.Count > 0)
			{
				return stack.Peek();
			}

			return null;
		}

		/// <summary>
		/// Pops the next undo operation
		/// </summary>
		public EditOperation? PopUndo(IWindowControl control)
		{
			lock (_lock)
			{
				if (_undoStacks.TryGetValue(control, out var undoStack) && undoStack.Count > 0)
				{
					var operation = undoStack.Pop();

					// Push to redo stack
					var redoStack = _redoStacks.GetOrAdd(control, _ => new Stack<EditOperation>());
					redoStack.Push(operation);

					return operation;
				}

				return null;
			}
		}

		/// <summary>
		/// Pops the next redo operation
		/// </summary>
		public EditOperation? PopRedo(IWindowControl control)
		{
			lock (_lock)
			{
				if (_redoStacks.TryGetValue(control, out var redoStack) && redoStack.Count > 0)
				{
					var operation = redoStack.Pop();

					// Push back to undo stack
					var undoStack = _undoStacks.GetOrAdd(control, _ => new Stack<EditOperation>());
					undoStack.Push(operation);

					return operation;
				}

				return null;
			}
		}

		/// <summary>
		/// Clears undo/redo history for a control
		/// </summary>
		public void ClearUndoHistory(IWindowControl control)
		{
			if (_undoStacks.TryGetValue(control, out var undoStack))
			{
				undoStack.Clear();
			}

			if (_redoStacks.TryGetValue(control, out var redoStack))
			{
				redoStack.Clear();
			}
		}

		/// <summary>
		/// Gets the undo stack size for a control
		/// </summary>
		public int GetUndoStackSize(IWindowControl control)
		{
			return _undoStacks.TryGetValue(control, out var stack) ? stack.Count : 0;
		}

		/// <summary>
		/// Gets the redo stack size for a control
		/// </summary>
		public int GetRedoStackSize(IWindowControl control)
		{
			return _redoStacks.TryGetValue(control, out var stack) ? stack.Count : 0;
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent edit state history for debugging
		/// </summary>
		public IReadOnlyList<(IWindowControl Control, EditState State)> GetHistory()
		{
			return _stateHistory.ToArray();
		}

		/// <summary>
		/// Clears the state history
		/// </summary>
		public void ClearHistory()
		{
			while (_stateHistory.TryDequeue(out _)) { }
		}

		/// <summary>
		/// Gets a debug string representation of edit state for a control
		/// </summary>
		public string GetDebugInfo(IWindowControl control)
		{
			var state = GetEditState(control);
			return $"Edit: Control={control.GetType().Name}, " +
			       $"Cursor=({state.CursorPosition.Line}, {state.CursorPosition.Column}), " +
			       $"HasSelection={state.HasSelection}, " +
			       $"Editing={state.IsEditing}, " +
			       $"Undo={GetUndoStackSize(control)}, " +
			       $"Redo={GetRedoStackSize(control)}";
		}

		#endregion

		#region Private Helpers

		private void UpdateStateInternal(IWindowControl control, EditState previousState, EditState newState, EditChangeReason reason)
		{
			_editStates[control] = newState;

			// Add to history
			_stateHistory.Enqueue((control, newState));
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			// Fire state changed event
			var args = new EditStateChangedEventArgs(control, previousState, newState, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					StateChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireCursorMoved(IWindowControl control, EditState previousState, EditState newState, EditChangeReason reason)
		{
			var args = new EditStateChangedEventArgs(control, previousState, newState, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					CursorMoved?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireSelectionChanged(IWindowControl control, EditState previousState, EditState newState, EditChangeReason reason)
		{
			var args = new EditStateChangedEventArgs(control, previousState, newState, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					SelectionChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireTextChanged(IWindowControl control, EditOperation operation)
		{
			var args = new TextChangedEventArgs(control, operation);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					TextChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		#endregion

		#region IDisposable

		public void Dispose()
		{
			if (_isDisposed)
				return;

			_isDisposed = true;
			ClearHistory();
			_editStates.Clear();
			_undoStacks.Clear();
			_redoStacks.Clear();

			// Clear event handlers
			StateChanged = null;
			CursorMoved = null;
			SelectionChanged = null;
			TextChanged = null;
		}

		#endregion
	}
}
