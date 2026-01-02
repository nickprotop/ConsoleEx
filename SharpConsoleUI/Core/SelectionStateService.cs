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
	/// Centralized service for managing selection state across controls.
	/// Provides a unified selection model for list-like controls.
	/// </summary>
	public class SelectionStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ConcurrentDictionary<IWindowControl, SelectionState> _selectionStates = new();
		private readonly ConcurrentQueue<(IWindowControl Control, SelectionState State)> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		#region Events

		/// <summary>
		/// Event fired when any control's selection changes
		/// </summary>
		public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

		/// <summary>
		/// Event fired when any control's highlight changes
		/// </summary>
		public event EventHandler<SelectionChangedEventArgs>? HighlightChanged;

		#endregion

		#region Selection Management

		/// <summary>
		/// Gets the selection state for a control
		/// </summary>
		public SelectionState GetSelectionState(IWindowControl control)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			return _selectionStates.GetValueOrDefault(control, SelectionState.Empty);
		}

		/// <summary>
		/// Gets the selected index for a control
		/// </summary>
		public int GetSelectedIndex(IWindowControl control)
		{
			return GetSelectionState(control).SelectedIndex;
		}

		/// <summary>
		/// Gets the highlighted index for a control
		/// </summary>
		public int GetHighlightedIndex(IWindowControl control)
		{
			return GetSelectionState(control).HighlightedIndex;
		}

		/// <summary>
		/// Sets the selected index for a control
		/// </summary>
		public void SetSelectedIndex(IWindowControl control, int index, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (previousState.SelectedIndex == index)
					return;

				var newState = previousState with
				{
					SelectedIndex = index,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireSelectionChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Sets the highlighted index for a control
		/// </summary>
		public void SetHighlightedIndex(IWindowControl control, int index, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (previousState.HighlightedIndex == index)
					return;

				var newState = previousState with
				{
					HighlightedIndex = index,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireHighlightChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Sets both selected and highlighted index for a control
		/// </summary>
		public void SetSelection(IWindowControl control, int selectedIndex, int highlightedIndex, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (previousState.SelectedIndex == selectedIndex && previousState.HighlightedIndex == highlightedIndex)
					return;

				var newState = previousState with
				{
					SelectedIndex = selectedIndex,
					HighlightedIndex = highlightedIndex,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);

				if (previousState.SelectedIndex != selectedIndex)
				{
					FireSelectionChanged(control, previousState, newState, reason);
				}

				if (previousState.HighlightedIndex != highlightedIndex)
				{
					FireHighlightChanged(control, previousState, newState, reason);
				}
			}
		}

		/// <summary>
		/// Clears selection for a control
		/// </summary>
		public void ClearSelection(IWindowControl control, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (previousState.SelectedIndex == -1 && previousState.SelectedIndices.Count == 0)
					return;

				var newState = new SelectionState
				{
					SelectedIndex = -1,
					HighlightedIndex = previousState.HighlightedIndex,
					IsMultiSelect = previousState.IsMultiSelect,
					SelectedIndices = new HashSet<int>(),
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireSelectionChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Clears highlight for a control
		/// </summary>
		public void ClearHighlight(IWindowControl control, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			SetHighlightedIndex(control, -1, reason);
		}

		/// <summary>
		/// Clears all selection state for a control
		/// </summary>
		public void ClearAll(IWindowControl control, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				var newState = SelectionState.Empty with { UpdateTime = DateTime.UtcNow };

				UpdateStateInternal(control, previousState, newState, reason);

				if (previousState.SelectedIndex != -1 || previousState.SelectedIndices.Count > 0)
				{
					FireSelectionChanged(control, previousState, newState, reason);
				}

				if (previousState.HighlightedIndex != -1)
				{
					FireHighlightChanged(control, previousState, newState, reason);
				}
			}
		}

		/// <summary>
		/// Removes all state for a control (when control is disposed)
		/// </summary>
		public void UnregisterControl(IWindowControl control)
		{
			if (control == null)
				return;

			_selectionStates.TryRemove(control, out _);
		}

		#endregion

		#region Multi-Selection Support

		/// <summary>
		/// Enables multi-selection for a control
		/// </summary>
		public void EnableMultiSelect(IWindowControl control)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (previousState.IsMultiSelect)
					return;

				var selectedIndices = new HashSet<int>();
				if (previousState.SelectedIndex >= 0)
				{
					selectedIndices.Add(previousState.SelectedIndex);
				}

				var newState = previousState with
				{
					IsMultiSelect = true,
					SelectedIndices = selectedIndices,
					UpdateTime = DateTime.UtcNow
				};

				_selectionStates[control] = newState;
			}
		}

		/// <summary>
		/// Toggles selection of an index in multi-select mode
		/// </summary>
		public void ToggleSelection(IWindowControl control, int index, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (!previousState.IsMultiSelect)
				{
					// In single-select mode, just set the index
					SetSelectedIndex(control, index, reason);
					return;
				}

				var newIndices = new HashSet<int>(previousState.SelectedIndices);

				if (newIndices.Contains(index))
				{
					newIndices.Remove(index);
				}
				else
				{
					newIndices.Add(index);
				}

				var newState = previousState with
				{
					SelectedIndices = newIndices,
					SelectedIndex = newIndices.Count > 0 ? newIndices.Max() : -1,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireSelectionChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Adds an index to selection in multi-select mode
		/// </summary>
		public void AddToSelection(IWindowControl control, int index, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (!previousState.IsMultiSelect || previousState.SelectedIndices.Contains(index))
					return;

				var newIndices = new HashSet<int>(previousState.SelectedIndices) { index };

				var newState = previousState with
				{
					SelectedIndices = newIndices,
					SelectedIndex = index,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireSelectionChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Removes an index from selection in multi-select mode
		/// </summary>
		public void RemoveFromSelection(IWindowControl control, int index, SelectionChangeReason reason = SelectionChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetSelectionState(control);

				if (!previousState.IsMultiSelect || !previousState.SelectedIndices.Contains(index))
					return;

				var newIndices = new HashSet<int>(previousState.SelectedIndices);
				newIndices.Remove(index);

				var newState = previousState with
				{
					SelectedIndices = newIndices,
					SelectedIndex = newIndices.Count > 0 ? newIndices.Max() : -1,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				FireSelectionChanged(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Gets all selected indices for a control
		/// </summary>
		public IReadOnlySet<int> GetSelectedIndices(IWindowControl control)
		{
			var state = GetSelectionState(control);

			if (state.IsMultiSelect)
			{
				return state.SelectedIndices;
			}

			// For single-select, return a set with just the selected index
			if (state.SelectedIndex >= 0)
			{
				return new HashSet<int> { state.SelectedIndex };
			}

			return new HashSet<int>();
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent selection state history for debugging
		/// </summary>
		public IReadOnlyList<(IWindowControl Control, SelectionState State)> GetHistory()
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
		/// Gets a debug string representation of selection state for a control
		/// </summary>
		public string GetDebugInfo(IWindowControl control)
		{
			var state = GetSelectionState(control);
			return $"Selection: Control={control.GetType().Name}, " +
			       $"Selected={state.SelectedIndex}, " +
			       $"Highlighted={state.HighlightedIndex}, " +
			       $"MultiSelect={state.IsMultiSelect}";
		}

		#endregion

		#region Private Helpers

		private void UpdateStateInternal(IWindowControl control, SelectionState previousState, SelectionState newState, SelectionChangeReason reason)
		{
			_selectionStates[control] = newState;

			// Add to history
			_stateHistory.Enqueue((control, newState));
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}
		}

		private void FireSelectionChanged(IWindowControl control, SelectionState previousState, SelectionState newState, SelectionChangeReason reason)
		{
			var args = new SelectionChangedEventArgs(control, previousState, newState, reason);
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

		private void FireHighlightChanged(IWindowControl control, SelectionState previousState, SelectionState newState, SelectionChangeReason reason)
		{
			var args = new SelectionChangedEventArgs(control, previousState, newState, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					HighlightChanged?.Invoke(this, args);
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
			_selectionStates.Clear();

			// Clear event handlers
			SelectionChanged = null;
			HighlightChanged = null;
		}

		#endregion
	}
}
