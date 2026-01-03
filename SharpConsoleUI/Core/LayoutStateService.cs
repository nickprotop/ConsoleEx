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
	/// Event arguments for layout changes.
	/// </summary>
	public class LayoutChangedEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the control whose layout changed.
		/// </summary>
		public IWindowControl Control { get; }

		/// <summary>
		/// Gets the previous layout state.
		/// </summary>
		public LayoutState PreviousState { get; }

		/// <summary>
		/// Gets the new layout state.
		/// </summary>
		public LayoutState NewState { get; }

		/// <summary>
		/// Gets the reason for the layout change.
		/// </summary>
		public LayoutChangeReason Reason { get; }

		/// <summary>
		/// Gets a value indicating whether the available width changed.
		/// </summary>
		public bool WidthChanged => PreviousState.AvailableWidth != NewState.AvailableWidth;

		/// <summary>
		/// Gets a value indicating whether the available height changed.
		/// </summary>
		public bool HeightChanged => PreviousState.AvailableHeight != NewState.AvailableHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="LayoutChangedEventArgs"/> class.
		/// </summary>
		/// <param name="control">The control whose layout changed.</param>
		/// <param name="previousState">The previous layout state.</param>
		/// <param name="newState">The new layout state.</param>
		/// <param name="reason">The reason for the layout change.</param>
		public LayoutChangedEventArgs(IWindowControl control, LayoutState previousState, LayoutState newState, LayoutChangeReason reason)
		{
			Control = control;
			PreviousState = previousState;
			NewState = newState;
			Reason = reason;
		}
	}

	/// <summary>
	/// Centralized service for managing layout state across controls.
	/// Provides smart invalidation and layout negotiation support.
	/// </summary>
	public class LayoutStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ConcurrentDictionary<IWindowControl, LayoutState> _layoutStates = new();
		private readonly ConcurrentQueue<(IWindowControl Control, LayoutState State)> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		#region Events

		/// <summary>
		/// Occurs when any control's layout state changes.
		/// </summary>
		public event EventHandler<LayoutChangedEventArgs>? LayoutChanged;

		#endregion

		#region Layout State Management

		/// <summary>
		/// Gets the layout state for a control.
		/// </summary>
		/// <param name="control">The control to get layout state for.</param>
		/// <returns>The layout state for the control, or <see cref="LayoutState.Empty"/> if not found.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
		public LayoutState GetLayoutState(IWindowControl control)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			return _layoutStates.GetValueOrDefault(control, LayoutState.Empty);
		}

		/// <summary>
		/// Gets the layout requirements for a control.
		/// </summary>
		/// <param name="control">The control to get requirements for.</param>
		/// <returns>The layout requirements for the control.</returns>
		public LayoutRequirements GetRequirements(IWindowControl control)
		{
			return GetLayoutState(control).Requirements;
		}

		/// <summary>
		/// Gets the available width for a control.
		/// </summary>
		/// <param name="control">The control to get available width for.</param>
		/// <returns>The available width, or null if not set.</returns>
		public int? GetAvailableWidth(IWindowControl control)
		{
			return GetLayoutState(control).AvailableWidth;
		}

		/// <summary>
		/// Gets the available height for a control.
		/// </summary>
		/// <param name="control">The control to get available height for.</param>
		/// <returns>The available height, or null if not set.</returns>
		public int? GetAvailableHeight(IWindowControl control)
		{
			return GetLayoutState(control).AvailableHeight;
		}

		/// <summary>
		/// Updates the available space for a control.
		/// </summary>
		/// <param name="control">The control to update.</param>
		/// <param name="availableWidth">The new available width.</param>
		/// <param name="availableHeight">The new available height.</param>
		/// <param name="reason">The reason for the change.</param>
		/// <returns>True if the space actually changed (control needs re-render); otherwise, false.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
		public bool UpdateAvailableSpace(IWindowControl control, int? availableWidth, int? availableHeight,
			LayoutChangeReason reason = LayoutChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetLayoutState(control);

				if (!previousState.HasSpaceChanged(availableWidth, availableHeight))
					return false;

				var newState = previousState with
				{
					AvailableWidth = availableWidth,
					AvailableHeight = availableHeight,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
				return true;
			}
		}

		/// <summary>
		/// Updates the layout requirements for a control.
		/// </summary>
		/// <param name="control">The control to update.</param>
		/// <param name="requirements">The new layout requirements.</param>
		/// <param name="reason">The reason for the change.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
		public void UpdateRequirements(IWindowControl control, LayoutRequirements requirements,
			LayoutChangeReason reason = LayoutChangeReason.RequirementsChange)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetLayoutState(control);

				if (previousState.Requirements == requirements)
					return;

				var newState = previousState with
				{
					Requirements = requirements,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);
			}
		}

		/// <summary>
		/// Updates the actual rendered dimensions for a control.
		/// </summary>
		/// <param name="control">The control to update.</param>
		/// <param name="actualWidth">The actual rendered width.</param>
		/// <param name="actualHeight">The actual rendered height.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
		public void UpdateActualDimensions(IWindowControl control, int actualWidth, int actualHeight)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetLayoutState(control);

				if (previousState.ActualWidth == actualWidth && previousState.ActualHeight == actualHeight)
					return;

				var newState = previousState with
				{
					ActualWidth = actualWidth,
					ActualHeight = actualHeight,
					UpdateTime = DateTime.UtcNow
				};

				// Don't fire event for actual dimension updates (internal tracking only)
				_layoutStates[control] = newState;
			}
		}

		/// <summary>
		/// Sets the layout allocation for a control after layout calculation.
		/// </summary>
		/// <param name="control">The control to set allocation for.</param>
		/// <param name="allocation">The layout allocation to set.</param>
		/// <param name="reason">The reason for the change.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
		public void SetAllocation(IWindowControl control, LayoutAllocation allocation,
			LayoutChangeReason reason = LayoutChangeReason.Programmatic)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			lock (_lock)
			{
				var previousState = GetLayoutState(control);

				var newState = previousState with
				{
					Allocation = allocation,
					AvailableWidth = allocation.AllocatedWidth,
					AvailableHeight = allocation.AllocatedHeight,
					UpdateTime = DateTime.UtcNow
				};

				UpdateStateInternal(control, previousState, newState, reason);

				// Notify control if it implements ILayoutAware
				if (control is ILayoutAware layoutAware)
				{
					layoutAware.OnLayoutAllocated(allocation);
				}
			}
		}

		/// <summary>
		/// Checks if a control needs re-rendering based on new available space.
		/// </summary>
		/// <param name="control">The control to check.</param>
		/// <param name="newWidth">The new available width.</param>
		/// <param name="newHeight">The new available height.</param>
		/// <returns>True if the control needs re-rendering; otherwise, false.</returns>
		public bool NeedsRerender(IWindowControl control, int? newWidth, int? newHeight)
		{
			var state = GetLayoutState(control);
			return state.NeedsRerender(newWidth, newHeight);
		}

		/// <summary>
		/// Removes all state for a control (when control is disposed).
		/// </summary>
		/// <param name="control">The control to unregister.</param>
		public void UnregisterControl(IWindowControl control)
		{
			if (control == null)
				return;

			_layoutStates.TryRemove(control, out _);
		}

		#endregion

		#region Layout Helpers

		/// <summary>
		/// Calculates the effective width for a control based on requirements and available space.
		/// </summary>
		/// <param name="control">The control to calculate width for.</param>
		/// <param name="availableWidth">The available width constraint.</param>
		/// <returns>The calculated effective width.</returns>
		public int GetEffectiveWidth(IWindowControl control, int? availableWidth)
		{
			var state = GetLayoutState(control);
			var req = state.Requirements;

			// If control has fixed width, use it
			if (req.Width.HasValue)
				return req.ClampWidth(req.Width.Value);

			// If stretch, use all available space
			if (req.IsStretch && availableWidth.HasValue)
				return req.ClampWidth(availableWidth.Value);

			// Otherwise use available width, clamped to constraints
			return req.ClampWidth(availableWidth ?? 80);
		}

		/// <summary>
		/// Calculates the effective height for a control based on requirements and available space.
		/// </summary>
		/// <param name="control">The control to calculate height for.</param>
		/// <param name="availableHeight">The available height constraint.</param>
		/// <returns>The calculated effective height.</returns>
		public int GetEffectiveHeight(IWindowControl control, int? availableHeight)
		{
			var state = GetLayoutState(control);
			var req = state.Requirements;

			// If control has fixed height, use it
			if (req.Height.HasValue)
				return req.ClampHeight(req.Height.Value);

			// Otherwise use available height, clamped to constraints
			return req.ClampHeight(availableHeight ?? 25);
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent layout state history for debugging.
		/// </summary>
		/// <returns>A read-only list of control and layout state pairs.</returns>
		public IReadOnlyList<(IWindowControl Control, LayoutState State)> GetHistory()
		{
			return _stateHistory.ToArray();
		}

		/// <summary>
		/// Clears the state history.
		/// </summary>
		public void ClearHistory()
		{
			while (_stateHistory.TryDequeue(out _)) { }
		}

		/// <summary>
		/// Gets a debug string representation of layout state for a control.
		/// </summary>
		/// <param name="control">The control to get debug info for.</param>
		/// <returns>A formatted string containing the control's layout state information.</returns>
		public string GetDebugInfo(IWindowControl control)
		{
			var state = GetLayoutState(control);
			return $"Layout: Type={control.GetType().Name}, " +
			       $"Available=({state.AvailableWidth}x{state.AvailableHeight}), " +
			       $"Actual=({state.ActualWidth}x{state.ActualHeight}), " +
			       $"Requirements=(W:{state.Requirements.Width}, Min:{state.Requirements.MinWidth}, Max:{state.Requirements.MaxWidth})";
		}

		#endregion

		#region Private Helpers

		private void UpdateStateInternal(IWindowControl control, LayoutState previousState, LayoutState newState, LayoutChangeReason reason)
		{
			_layoutStates[control] = newState;

			// Add to history
			_stateHistory.Enqueue((control, newState));
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			// Fire event
			var args = new LayoutChangedEventArgs(control, previousState, newState, reason);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					LayoutChanged?.Invoke(this, args);
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
			_layoutStates.Clear();

			// Clear event handlers
			LayoutChanged = null;
		}

		#endregion
	}
}
