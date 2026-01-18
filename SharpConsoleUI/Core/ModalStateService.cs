// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Centralized service for managing modal window state.
	/// Handles modal window stack, parent-child relationships, and activation blocking.
	/// </summary>
	public class ModalStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ILogService? _logService;
		private ModalState _currentState = ModalState.Empty;
		private readonly List<Window> _modalStack = new();
		private readonly Dictionary<Window, Window> _modalParents = new();
		private readonly ConcurrentQueue<ModalState> _stateHistory = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="ModalStateService"/> class.
		/// </summary>
		/// <param name="logService">Optional log service for diagnostic logging.</param>
		public ModalStateService(ILogService? logService = null)
		{
			_logService = logService;
		}

		#region Properties

		/// <summary>
		/// Gets the current modal state.
		/// </summary>
		public ModalState CurrentState
		{
			get
			{
				lock (_lock)
				{
					return _currentState;
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether any modal windows are open.
		/// </summary>
		public bool HasModals => CurrentState.HasModals;

		/// <summary>
		/// Gets the number of modal windows currently open.
		/// </summary>
		public int ModalCount => CurrentState.ModalCount;

		/// <summary>
		/// Gets the topmost modal window (if any).
		/// </summary>
		public Window? TopmostModal => CurrentState.TopmostModal;

		#endregion

		#region Events

		/// <summary>
		/// Occurs when modal state changes.
		/// </summary>
		public event EventHandler<ModalStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Occurs when a modal window is opened.
		/// </summary>
		public event EventHandler<ModalStateChangedEventArgs>? ModalOpened;

		/// <summary>
		/// Occurs when a modal window is closed.
		/// </summary>
		public event EventHandler<ModalStateChangedEventArgs>? ModalClosed;

		/// <summary>
		/// Occurs when window activation is blocked by a modal.
		/// </summary>
		public event EventHandler<ActivationBlockedEventArgs>? ActivationBlocked;

		#endregion

		#region Modal Management

		/// <summary>
		/// Registers a modal window with its parent.
		/// </summary>
		/// <param name="modal">The modal window.</param>
		/// <param name="parent">The parent window (can be null for orphan modals).</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="modal"/> is null.</exception>
		public void PushModal(Window modal, Window? parent)
		{
			if (modal == null)
				throw new ArgumentNullException(nameof(modal));

			lock (_lock)
			{
				_logService?.LogDebug($"Modal pushed: {modal.Title} (parent: {parent?.Title ?? "None"})", "Modal");

				var previousState = _currentState;

				// Add to stack
				_modalStack.Add(modal);

				// Track parent relationship
				if (parent != null)
				{
					_modalParents[modal] = parent;
				}

				// Update state
				UpdateStateInternal();

				// Fire events
				var args = new ModalStateChangedEventArgs(previousState, _currentState, openedModal: modal);
				FireModalOpened(args);
			}
		}

		/// <summary>
		/// Unregisters a modal window.
		/// </summary>
		/// <param name="modal">The modal window to remove.</param>
		public void PopModal(Window modal)
		{
			if (modal == null)
				return;

			lock (_lock)
			{
				if (!_modalStack.Contains(modal))
					return;

				_logService?.LogDebug($"Modal popped: {modal.Title}", "Modal");

				var previousState = _currentState;

				// Remove from stack
				_modalStack.Remove(modal);

				// Remove parent relationship
				_modalParents.Remove(modal);

				// Update state
				UpdateStateInternal();

				// Fire events
				var args = new ModalStateChangedEventArgs(previousState, _currentState, closedModal: modal);
				FireModalClosed(args);
			}
		}

		/// <summary>
		/// Gets the parent of a modal window.
		/// </summary>
		/// <param name="modal">The modal window.</param>
		/// <returns>The parent window, or null if the modal has no parent.</returns>
		public Window? GetModalParent(Window modal)
		{
			lock (_lock)
			{
				return _modalParents.TryGetValue(modal, out var parent) ? parent : null;
			}
		}

		/// <summary>
		/// Checks if a window is a modal.
		/// </summary>
		/// <param name="window">The window to check.</param>
		/// <returns>True if the window is a modal; otherwise, false.</returns>
		public bool IsModal(Window window)
		{
			lock (_lock)
			{
				return _modalStack.Contains(window);
			}
		}

		/// <summary>
		/// Gets all modal children of a window (direct children only).
		/// </summary>
		/// <param name="parent">The parent window.</param>
		/// <returns>A read-only list of modal children.</returns>
		public IReadOnlyList<Window> GetModalChildren(Window parent)
		{
			lock (_lock)
			{
				return _modalParents
					.Where(kv => kv.Value == parent)
					.Select(kv => kv.Key)
					.ToList();
			}
		}

		/// <summary>
		/// Gets the deepest modal child of a window (recursive).
		/// </summary>
		/// <param name="parent">The parent window.</param>
		/// <returns>The deepest modal child, or null if no modal children exist.</returns>
		public Window? GetDeepestModalChild(Window parent)
		{
			lock (_lock)
			{
				return FindDeepestModalChildInternal(parent);
			}
		}

		#endregion

		#region Activation Blocking

		/// <summary>
		/// Checks if activation of a window is blocked by a modal.
		/// </summary>
		/// <param name="targetWindow">The window trying to be activated.</param>
		/// <returns>The blocking modal, or null if not blocked.</returns>
		public Window? GetBlockingModal(Window targetWindow)
		{
			lock (_lock)
			{
				// Check for orphan modals (modals with no parent that block everything)
				var orphanModal = _modalStack.FirstOrDefault(m => !_modalParents.ContainsKey(m));
				if (orphanModal != null && orphanModal != targetWindow)
				{
					// Don't block if targetWindow is a child of the orphan modal
					if (IsChildOfInternal(targetWindow, orphanModal))
					{
						// Target is a child of orphan, allow activation
						// (but check if target has its own modal children)
						var childModal = FindDeepestModalChildInternal(targetWindow);
						if (childModal != null)
						{
							return childModal;
						}
						return null;
					}

					return orphanModal;
				}

				// Check if target window has modal children that should be activated instead
				var modalChild = FindDeepestModalChildInternal(targetWindow);
				if (modalChild != null)
				{
					return modalChild;
				}

				return null;
			}
		}

		/// <summary>
		/// Checks if activation of a window is blocked.
		/// </summary>
		/// <param name="targetWindow">The window trying to be activated.</param>
		/// <returns>True if blocked; otherwise, false.</returns>
		public bool IsActivationBlocked(Window targetWindow)
		{
			return GetBlockingModal(targetWindow) != null;
		}

		/// <summary>
		/// Gets the window that should actually be activated when trying to activate a target window.
		/// This handles modal blocking by returning the deepest modal child if one exists.
		/// </summary>
		/// <param name="targetWindow">The window the user is trying to activate.</param>
		/// <returns>The window that should actually be activated.</returns>
		public Window GetEffectiveActivationTarget(Window targetWindow)
		{
			lock (_lock)
			{
				// First check for orphan modals
				var orphanModal = _modalStack.FirstOrDefault(m => !_modalParents.ContainsKey(m));
				if (orphanModal != null && orphanModal != targetWindow)
				{
					// Don't block if targetWindow is a child of the orphan modal
					if (IsChildOfInternal(targetWindow, orphanModal))
					{
						// Target is a child of orphan, allow it to be the effective target
						// (but check if target has its own modal children)
						var childModal = FindDeepestModalChildInternal(targetWindow);
						if (childModal != null)
						{
							FireActivationBlocked(targetWindow, childModal);
							return childModal;
						}
						return targetWindow;
					}

					// Orphan modal blocks everything else
					FireActivationBlocked(targetWindow, orphanModal);
					return orphanModal;
				}

				// Find deepest modal child
				var modalChild = FindDeepestModalChildInternal(targetWindow);
				if (modalChild != null)
				{
					FireActivationBlocked(targetWindow, modalChild);
					return modalChild;
				}

				return targetWindow;
			}
		}

		/// <summary>
		/// Checks if there are any orphan modals (modals with no parent that block everything).
		/// </summary>
		/// <returns>True if orphan modals exist; otherwise, false.</returns>
		public bool HasOrphanModals()
		{
			lock (_lock)
			{
				return _modalStack.Any(m => !_modalParents.ContainsKey(m));
			}
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent modal state history for debugging.
		/// </summary>
		/// <returns>A read-only list of recent modal states.</returns>
		public IReadOnlyList<ModalState> GetHistory()
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
		/// Gets a debug string representation of current modal state.
		/// </summary>
		/// <returns>A formatted string containing the current modal state information.</returns>
		public string GetDebugInfo()
		{
			var state = CurrentState;
			var modalNames = state.ModalStack.Select(m => m.Title).ToArray();
			return $"Modals: Count={state.ModalCount}, " +
			       $"Stack=[{string.Join(", ", modalNames)}], " +
			       $"Topmost={state.TopmostModal?.Title ?? "none"}";
		}

		#endregion

		#region Private Helpers

		/// <summary>
		/// Checks if a window is a child (direct or indirect) of a parent window.
		/// </summary>
		private bool IsChildOfInternal(Window window, Window potentialParent)
		{
			var currentParent = _modalParents.TryGetValue(window, out var parent) ? parent : null;
			while (currentParent != null)
			{
				if (currentParent == potentialParent)
					return true;
				currentParent = _modalParents.TryGetValue(currentParent, out var nextParent) ? nextParent : null;
			}
			return false;
		}

		private Window? FindDeepestModalChildInternal(Window parent)
		{
			// Get direct modal children, ordered by their position in the stack (latest first)
			var modalChildren = _modalParents
				.Where(kv => kv.Value == parent)
				.Select(kv => kv.Key)
				.OrderByDescending(m => _modalStack.IndexOf(m))
				.ToList();

			if (modalChildren.Count == 0)
				return null;

			// Take the highest modal child
			var highestModalChild = modalChildren.First();

			// Recursively check if this modal child has modal children
			var deeperModalChild = FindDeepestModalChildInternal(highestModalChild);

			return deeperModalChild ?? highestModalChild;
		}

		private void UpdateStateInternal()
		{
			var previousState = _currentState;

			_currentState = new ModalState
			{
				ModalStack = _modalStack.ToList().AsReadOnly(),
				ModalParents = new Dictionary<Window, Window>(_modalParents),
				UpdateTime = DateTime.UtcNow
			};

			// Add to history
			_stateHistory.Enqueue(_currentState);
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			// Fire state changed event
			var args = new ModalStateChangedEventArgs(previousState, _currentState);
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

		private void FireModalOpened(ModalStateChangedEventArgs args)
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ModalOpened?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireModalClosed(ModalStateChangedEventArgs args)
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ModalClosed?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		private void FireActivationBlocked(Window targetWindow, Window blockingModal)
		{
			var args = new ActivationBlockedEventArgs(targetWindow, blockingModal);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ActivationBlocked?.Invoke(this, args);
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
			_modalStack.Clear();
			_modalParents.Clear();

			// Clear event handlers
			StateChanged = null;
			ModalOpened = null;
			ModalClosed = null;
			ActivationBlocked = null;
		}

		#endregion
	}
}
