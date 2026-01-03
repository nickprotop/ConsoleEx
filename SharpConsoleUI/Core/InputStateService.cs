// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Event arguments for key press events
	/// </summary>
	public class KeyPressEventArgs : EventArgs
	{
		/// <summary>
		/// The key information
		/// </summary>
		public ConsoleKeyInfo KeyInfo { get; }

		/// <summary>
		/// Timestamp when the key was pressed
		/// </summary>
		public DateTime Timestamp { get; }

		/// <summary>
		/// Whether the key press was handled
		/// </summary>
		public bool Handled { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="KeyPressEventArgs"/> class.
		/// </summary>
		/// <param name="keyInfo">The key information for the pressed key.</param>
		public KeyPressEventArgs(ConsoleKeyInfo keyInfo)
		{
			KeyInfo = keyInfo;
			Timestamp = DateTime.UtcNow;
		}
	}

	/// <summary>
	/// Event arguments for idle state changes
	/// </summary>
	public class IdleStateEventArgs : EventArgs
	{
		/// <summary>
		/// Whether the system is now idle
		/// </summary>
		public bool IsIdle { get; }

		/// <summary>
		/// Duration of idle time (if becoming idle) or time since last activity
		/// </summary>
		public TimeSpan IdleDuration { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IdleStateEventArgs"/> class.
		/// </summary>
		/// <param name="isIdle">Whether the system is now idle.</param>
		/// <param name="idleDuration">The duration of idle time.</param>
		public IdleStateEventArgs(bool isIdle, TimeSpan idleDuration)
		{
			IsIdle = isIdle;
			IdleDuration = idleDuration;
		}
	}

	/// <summary>
	/// Centralized service for managing input state.
	/// Provides input queue management, modifier tracking, and input history.
	/// </summary>
	public class InputStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ConcurrentQueue<ConsoleKeyInfo> _inputQueue = new();
		private readonly ConcurrentQueue<(ConsoleKeyInfo Key, DateTime Time)> _inputHistory = new();
		private const int MaxHistorySize = 100;
		private DateTime _lastKeyTime = DateTime.MinValue;
		private ConsoleModifiers _lastModifiers = 0;
		private bool _isIdle = true;
		private readonly TimeSpan _idleThreshold = TimeSpan.FromMilliseconds(500);
		private bool _isDisposed;

		#region Properties

		/// <summary>
		/// Gets whether there are pending keys in the queue
		/// </summary>
		public bool HasPendingInput => !_inputQueue.IsEmpty;

		/// <summary>
		/// Gets the number of pending keys in the queue
		/// </summary>
		public int PendingInputCount => _inputQueue.Count;

		/// <summary>
		/// Gets the time of the last key press
		/// </summary>
		public DateTime LastKeyTime
		{
			get
			{
				lock (_lock)
				{
					return _lastKeyTime;
				}
			}
		}

		/// <summary>
		/// Gets the last known modifier state
		/// </summary>
		public ConsoleModifiers LastModifiers
		{
			get
			{
				lock (_lock)
				{
					return _lastModifiers;
				}
			}
		}

		/// <summary>
		/// Gets whether the system is currently idle (no recent input)
		/// </summary>
		public bool IsIdle
		{
			get
			{
				lock (_lock)
				{
					return _isIdle;
				}
			}
		}

		/// <summary>
		/// Gets the time since the last key press
		/// </summary>
		public TimeSpan TimeSinceLastKey
		{
			get
			{
				lock (_lock)
				{
					return _lastKeyTime == DateTime.MinValue
						? TimeSpan.MaxValue
						: DateTime.UtcNow - _lastKeyTime;
				}
			}
		}

		#endregion

		#region Events

		/// <summary>
		/// Event fired when a key is pressed (after enqueue)
		/// </summary>
		public event EventHandler<KeyPressEventArgs>? KeyPressed;

		/// <summary>
		/// Event fired when idle state changes
		/// </summary>
		public event EventHandler<IdleStateEventArgs>? IdleStateChanged;

		#endregion

		#region Input Queue Management

		/// <summary>
		/// Enqueues a key for processing
		/// </summary>
		public void EnqueueKey(ConsoleKeyInfo key)
		{
			lock (_lock)
			{
				_inputQueue.Enqueue(key);
				_lastKeyTime = DateTime.UtcNow;
				_lastModifiers = key.Modifiers;

				// Add to history
				_inputHistory.Enqueue((key, _lastKeyTime));
				while (_inputHistory.Count > MaxHistorySize)
				{
					_inputHistory.TryDequeue(out _);
				}

				// Update idle state
				if (_isIdle)
				{
					_isIdle = false;
					FireIdleStateChanged(false, TimeSinceLastKey);
				}
			}

			// Fire key pressed event
			var args = new KeyPressEventArgs(key);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					KeyPressed?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		/// <summary>
		/// Dequeues a key from the queue
		/// </summary>
		/// <returns>The key, or null if queue is empty</returns>
		public ConsoleKeyInfo? DequeueKey()
		{
			if (_inputQueue.TryDequeue(out var key))
			{
				return key;
			}

			return null;
		}

		/// <summary>
		/// Peeks at the next key without removing it
		/// </summary>
		/// <returns>The key, or null if queue is empty</returns>
		public ConsoleKeyInfo? PeekKey()
		{
			if (_inputQueue.TryPeek(out var key))
			{
				return key;
			}

			return null;
		}

		/// <summary>
		/// Clears all pending input
		/// </summary>
		public void ClearQueue()
		{
			while (_inputQueue.TryDequeue(out _)) { }
		}

		/// <summary>
		/// Processes all pending input with a handler function
		/// </summary>
		/// <param name="handler">Function to handle each key (return true to continue, false to stop)</param>
		/// <returns>Number of keys processed</returns>
		public int ProcessAll(Func<ConsoleKeyInfo, bool> handler)
		{
			int count = 0;

			while (_inputQueue.TryDequeue(out var key))
			{
				count++;
				if (!handler(key))
					break;
			}

			return count;
		}

		#endregion

		#region Idle Detection

		/// <summary>
		/// Updates idle state based on current time.
		/// Should be called periodically from the main loop.
		/// </summary>
		public void UpdateIdleState()
		{
			lock (_lock)
			{
				var timeSinceLast = TimeSinceLastKey;
				bool shouldBeIdle = timeSinceLast > _idleThreshold && !HasPendingInput;

				if (shouldBeIdle != _isIdle)
				{
					_isIdle = shouldBeIdle;
					FireIdleStateChanged(_isIdle, timeSinceLast);
				}
			}
		}

		/// <summary>
		/// Gets a recommended sleep duration based on current activity
		/// </summary>
		/// <param name="minSleep">Minimum sleep duration (when active)</param>
		/// <param name="maxSleep">Maximum sleep duration (when idle)</param>
		/// <returns>Recommended sleep duration in milliseconds</returns>
		public int GetRecommendedSleepDuration(int minSleep = 10, int maxSleep = 100)
		{
			if (HasPendingInput)
				return minSleep;

			var timeSinceLastMs = TimeSinceLastKey.TotalMilliseconds;

			if (timeSinceLastMs < 100)
				return minSleep;

			if (timeSinceLastMs > 1000)
				return maxSleep;

			// Gradually increase sleep time
			return (int)Math.Min(maxSleep, minSleep + (timeSinceLastMs / 10));
		}

		#endregion

		#region Modifier Helpers

		/// <summary>
		/// Checks if Control modifier was held in last key press
		/// </summary>
		public bool WasControlHeld => (LastModifiers & ConsoleModifiers.Control) != 0;

		/// <summary>
		/// Checks if Alt modifier was held in last key press
		/// </summary>
		public bool WasAltHeld => (LastModifiers & ConsoleModifiers.Alt) != 0;

		/// <summary>
		/// Checks if Shift modifier was held in last key press
		/// </summary>
		public bool WasShiftHeld => (LastModifiers & ConsoleModifiers.Shift) != 0;

		#endregion

		#region Input History

		/// <summary>
		/// Gets recent input history for debugging
		/// </summary>
		public IReadOnlyList<(ConsoleKeyInfo Key, DateTime Time)> GetHistory()
		{
			return _inputHistory.ToArray();
		}

		/// <summary>
		/// Gets the last N keys from history
		/// </summary>
		public IReadOnlyList<ConsoleKeyInfo> GetLastKeys(int count)
		{
			return _inputHistory
				.ToArray()
				.TakeLast(count)
				.Select(x => x.Key)
				.ToList();
		}

		/// <summary>
		/// Clears the input history
		/// </summary>
		public void ClearHistory()
		{
			while (_inputHistory.TryDequeue(out _)) { }
		}

		/// <summary>
		/// Gets a debug string representation of current input state
		/// </summary>
		public string GetDebugInfo()
		{
			return $"Input: Pending={PendingInputCount}, " +
			       $"Idle={IsIdle}, " +
			       $"LastMods={LastModifiers}, " +
			       $"TimeSinceLast={TimeSinceLastKey.TotalMilliseconds:F0}ms";
		}

		#endregion

		#region Private Helpers

		private void FireIdleStateChanged(bool isIdle, TimeSpan idleDuration)
		{
			var args = new IdleStateEventArgs(isIdle, idleDuration);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					IdleStateChanged?.Invoke(this, args);
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
			ClearQueue();
			ClearHistory();

			// Clear event handlers
			KeyPressed = null;
			IdleStateChanged = null;
		}

		#endregion
	}
}
