// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Drawing;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Interface for objects that support scrolling
	/// </summary>
	public interface IScrollable
	{
		/// <summary>
		/// Gets the total content height
		/// </summary>
		int ContentHeight { get; }

		/// <summary>
		/// Gets the total content width
		/// </summary>
		int ContentWidth { get; }

		/// <summary>
		/// Gets the visible viewport height
		/// </summary>
		int ViewportHeight { get; }

		/// <summary>
		/// Gets the visible viewport width
		/// </summary>
		int ViewportWidth { get; }
	}

	/// <summary>
	/// Centralized service for managing scroll state across scrollable elements.
	/// Provides a unified scroll model for windows and controls.
	/// </summary>
	public class ScrollStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ConcurrentDictionary<object, ScrollState> _scrollStates = new();
		private readonly ConcurrentQueue<(object Scrollable, ScrollState State)> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		#region Events

		/// <summary>
		/// Event fired when any scrollable's scroll state changes
		/// </summary>
		public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

		#endregion

		#region Scroll State Management

		/// <summary>
		/// Gets the scroll state for a scrollable element
		/// </summary>
		public ScrollState GetScrollState(object scrollable)
		{
			if (scrollable == null)
				throw new ArgumentNullException(nameof(scrollable));

			return _scrollStates.GetValueOrDefault(scrollable, ScrollState.Empty);
		}

		/// <summary>
		/// Gets the vertical scroll offset for a scrollable element
		/// </summary>
		public int GetVerticalOffset(object scrollable)
		{
			return GetScrollState(scrollable).VerticalOffset;
		}

		/// <summary>
		/// Gets the horizontal scroll offset for a scrollable element
		/// </summary>
		public int GetHorizontalOffset(object scrollable)
		{
			return GetScrollState(scrollable).HorizontalOffset;
		}

		/// <summary>
		/// Gets the scroll position as a Point
		/// </summary>
		public Point GetScrollPosition(object scrollable)
		{
			var state = GetScrollState(scrollable);
			return new Point(state.HorizontalOffset, state.VerticalOffset);
		}

		/// <summary>
		/// Sets the vertical scroll offset
		/// </summary>
		public void SetVerticalOffset(object scrollable, int offset, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			if (scrollable == null)
				throw new ArgumentNullException(nameof(scrollable));

			lock (_lock)
			{
				var previousState = GetScrollState(scrollable);
				offset = Math.Max(0, Math.Min(offset, previousState.MaxVerticalOffset));

				if (previousState.VerticalOffset == offset)
					return;

				var newState = previousState with
				{
					VerticalOffset = offset,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(scrollable, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Sets the horizontal scroll offset
		/// </summary>
		public void SetHorizontalOffset(object scrollable, int offset, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			if (scrollable == null)
				throw new ArgumentNullException(nameof(scrollable));

			lock (_lock)
			{
				var previousState = GetScrollState(scrollable);
				offset = Math.Max(0, Math.Min(offset, previousState.MaxHorizontalOffset));

				if (previousState.HorizontalOffset == offset)
					return;

				var newState = previousState with
				{
					HorizontalOffset = offset,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(scrollable, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Sets both scroll offsets at once
		/// </summary>
		public void SetScrollPosition(object scrollable, int horizontalOffset, int verticalOffset, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			if (scrollable == null)
				throw new ArgumentNullException(nameof(scrollable));

			lock (_lock)
			{
				var previousState = GetScrollState(scrollable);
				horizontalOffset = Math.Max(0, Math.Min(horizontalOffset, previousState.MaxHorizontalOffset));
				verticalOffset = Math.Max(0, Math.Min(verticalOffset, previousState.MaxVerticalOffset));

				if (previousState.HorizontalOffset == horizontalOffset && previousState.VerticalOffset == verticalOffset)
					return;

				var newState = previousState with
				{
					HorizontalOffset = horizontalOffset,
					VerticalOffset = verticalOffset,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(scrollable, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Scrolls by a relative amount
		/// </summary>
		public void ScrollBy(object scrollable, int horizontalDelta, int verticalDelta, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			var state = GetScrollState(scrollable);
			SetScrollPosition(scrollable, state.HorizontalOffset + horizontalDelta, state.VerticalOffset + verticalDelta, reason);
		}

		/// <summary>
		/// Scrolls to make a specific row visible
		/// </summary>
		public void ScrollRowIntoView(object scrollable, int row, ScrollChangeReason reason = ScrollChangeReason.ScrollIntoView)
		{
			if (scrollable == null)
				throw new ArgumentNullException(nameof(scrollable));

			lock (_lock)
			{
				var state = GetScrollState(scrollable);

				if (state.IsRowVisible(row))
					return;

				int newOffset;
				if (row < state.VerticalOffset)
				{
					// Row is above viewport - scroll up
					newOffset = row;
				}
				else
				{
					// Row is below viewport - scroll down
					newOffset = row - state.ViewportHeight + 1;
				}

				SetVerticalOffset(scrollable, newOffset, reason);
			}
		}

		/// <summary>
		/// Scrolls to make a specific column visible
		/// </summary>
		public void ScrollColumnIntoView(object scrollable, int column, ScrollChangeReason reason = ScrollChangeReason.ScrollIntoView)
		{
			if (scrollable == null)
				throw new ArgumentNullException(nameof(scrollable));

			lock (_lock)
			{
				var state = GetScrollState(scrollable);

				if (state.IsColumnVisible(column))
					return;

				int newOffset;
				if (column < state.HorizontalOffset)
				{
					// Column is to the left of viewport - scroll left
					newOffset = column;
				}
				else
				{
					// Column is to the right of viewport - scroll right
					newOffset = column - state.ViewportWidth + 1;
				}

				SetHorizontalOffset(scrollable, newOffset, reason);
			}
		}

		/// <summary>
		/// Scrolls to the top
		/// </summary>
		public void ScrollToTop(object scrollable, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			SetVerticalOffset(scrollable, 0, reason);
		}

		/// <summary>
		/// Scrolls to the bottom
		/// </summary>
		public void ScrollToBottom(object scrollable, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			var state = GetScrollState(scrollable);
			SetVerticalOffset(scrollable, state.MaxVerticalOffset, reason);
		}

		/// <summary>
		/// Scrolls to the left edge
		/// </summary>
		public void ScrollToLeft(object scrollable, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			SetHorizontalOffset(scrollable, 0, reason);
		}

		/// <summary>
		/// Scrolls to the right edge
		/// </summary>
		public void ScrollToRight(object scrollable, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			var state = GetScrollState(scrollable);
			SetHorizontalOffset(scrollable, state.MaxHorizontalOffset, reason);
		}

		/// <summary>
		/// Resets scroll to origin (0, 0)
		/// </summary>
		public void ResetScroll(object scrollable, ScrollChangeReason reason = ScrollChangeReason.Programmatic)
		{
			SetScrollPosition(scrollable, 0, 0, reason);
		}

		#endregion

		#region Content/Viewport Management

		/// <summary>
		/// Updates the content and viewport dimensions for a scrollable
		/// </summary>
		public void UpdateDimensions(object scrollable, int contentWidth, int contentHeight, int viewportWidth, int viewportHeight)
		{
			if (scrollable == null)
				throw new ArgumentNullException(nameof(scrollable));

			lock (_lock)
			{
				var previousState = GetScrollState(scrollable);

				var newState = previousState with
				{
					ContentWidth = contentWidth,
					ContentHeight = contentHeight,
					ViewportWidth = viewportWidth,
					ViewportHeight = viewportHeight,
					// Clamp scroll offsets to new bounds
					HorizontalOffset = Math.Min(previousState.HorizontalOffset, Math.Max(0, contentWidth - viewportWidth)),
					VerticalOffset = Math.Min(previousState.VerticalOffset, Math.Max(0, contentHeight - viewportHeight)),
					UpdateTime = DateTime.UtcNow
				};

				if (previousState != newState)
				{
					UpdateStateInternal(scrollable, previousState, newState, ScrollChangeReason.ContentChange);
				}
			}
		}

		/// <summary>
		/// Updates dimensions from an IScrollable interface
		/// </summary>
		public void UpdateDimensionsFromScrollable(IScrollable scrollable)
		{
			UpdateDimensions(scrollable, scrollable.ContentWidth, scrollable.ContentHeight,
			                 scrollable.ViewportWidth, scrollable.ViewportHeight);
		}

		/// <summary>
		/// Removes all state for a scrollable element (when element is disposed)
		/// </summary>
		public void UnregisterScrollable(object scrollable)
		{
			if (scrollable == null)
				return;

			_scrollStates.TryRemove(scrollable, out _);
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent scroll state history for debugging
		/// </summary>
		public IReadOnlyList<(object Scrollable, ScrollState State)> GetHistory()
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
		/// Gets a debug string representation of scroll state for a scrollable
		/// </summary>
		public string GetDebugInfo(object scrollable)
		{
			var state = GetScrollState(scrollable);
			return $"Scroll: Type={scrollable.GetType().Name}, " +
			       $"Offset=({state.HorizontalOffset}, {state.VerticalOffset}), " +
			       $"Content=({state.ContentWidth}x{state.ContentHeight}), " +
			       $"Viewport=({state.ViewportWidth}x{state.ViewportHeight})";
		}

		#endregion

		#region Private Helpers

		private void UpdateStateInternal(object scrollable, ScrollState previousState, ScrollState newState, ScrollChangeReason reason)
		{
			_scrollStates[scrollable] = newState;

			// Add to history
			_stateHistory.Enqueue((scrollable, newState));
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			// Fire event
			var args = new ScrollChangedEventArgs(scrollable, previousState, newState, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ScrollChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		#endregion

		#region IDisposable

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_isDisposed)
				return;

			_isDisposed = true;
			ClearHistory();
			_scrollStates.Clear();

			// Clear event handlers
			ScrollChanged = null;
		}

		#endregion
	}
}
