// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Represents the state of a single notification.
	/// </summary>
	/// <param name="Id">The unique identifier for the notification.</param>
	/// <param name="Window">The window displaying the notification.</param>
	/// <param name="Title">The notification title.</param>
	/// <param name="Message">The notification message.</param>
	/// <param name="Severity">The severity level of the notification.</param>
	/// <param name="CreatedAt">The timestamp when the notification was created.</param>
	/// <param name="TimeoutMs">The auto-dismiss timeout in milliseconds, or null for no timeout.</param>
	/// <param name="IsModal">Whether the notification blocks UI interaction.</param>
	public record NotificationInfo(
		string Id,
		Window Window,
		string Title,
		string Message,
		NotificationSeverity Severity,
		DateTime CreatedAt,
		int? TimeoutMs,
		bool IsModal);

	/// <summary>
	/// Represents the current notification system state.
	/// </summary>
	/// <param name="ActiveNotifications">The list of currently active notifications.</param>
	/// <param name="TotalShown">The total number of notifications shown since service creation.</param>
	/// <param name="TotalDismissed">The total number of notifications dismissed since service creation.</param>
	public record NotificationState(
		IReadOnlyList<NotificationInfo> ActiveNotifications,
		int TotalShown,
		int TotalDismissed)
	{
		/// <summary>
		/// Gets an empty notification state.
		/// </summary>
		public static NotificationState Empty => new(
			Array.Empty<NotificationInfo>(),
			0,
			0);

		/// <summary>
		/// Gets a value indicating whether there are any active notifications.
		/// </summary>
		public bool HasNotifications => ActiveNotifications.Count > 0;

		/// <summary>
		/// Gets the number of active notifications.
		/// </summary>
		public int ActiveCount => ActiveNotifications.Count;
	}

	/// <summary>
	/// Event arguments for notification state changes.
	/// </summary>
	public class NotificationEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the notification that triggered the event.
		/// </summary>
		public NotificationInfo Notification { get; }

		/// <summary>
		/// Gets the previous notification state.
		/// </summary>
		public NotificationState PreviousState { get; }

		/// <summary>
		/// Gets the current notification state.
		/// </summary>
		public NotificationState CurrentState { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="NotificationEventArgs"/> class.
		/// </summary>
		/// <param name="notification">The notification that triggered the event.</param>
		/// <param name="previousState">The previous notification state.</param>
		/// <param name="currentState">The current notification state.</param>
		public NotificationEventArgs(NotificationInfo notification, NotificationState previousState, NotificationState currentState)
		{
			Notification = notification;
			PreviousState = previousState;
			CurrentState = currentState;
		}
	}

	/// <summary>
	/// Centralized service for managing notification state.
	/// Tracks active notifications, provides dismissal methods, and fires events.
	/// </summary>
	public class NotificationStateService : IDisposable
	{
		private readonly object _lock = new();
		private readonly ILogService? _logService;
		private readonly ConsoleWindowSystem _windowSystem;
		private NotificationState _currentState = NotificationState.Empty;
		private readonly List<NotificationInfo> _activeNotifications = new();
		private readonly ConcurrentQueue<NotificationState> _stateHistory = new();
		private readonly Dictionary<string, CancellationTokenSource> _timeoutCancellations = new();
		private const int MaxHistorySize = 100;
		private bool _isDisposed;
		private int _notificationCounter;

		/// <summary>
		/// Initializes a new instance of the <see cref="NotificationStateService"/> class.
		/// </summary>
		/// <param name="windowSystem">The console window system to display notifications in.</param>
		/// <param name="logService">Optional log service for diagnostic logging.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="windowSystem"/> is null.</exception>
		public NotificationStateService(ConsoleWindowSystem windowSystem, ILogService? logService = null)
		{
			_windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
			_logService = logService;
		}

		#region Properties

		/// <summary>
		/// Gets the current notification state.
		/// </summary>
		public NotificationState CurrentState
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
		/// Gets a value indicating whether any notifications are currently displayed.
		/// </summary>
		public bool HasNotifications => CurrentState.HasNotifications;

		/// <summary>
		/// Gets the number of active notifications.
		/// </summary>
		public int ActiveCount => CurrentState.ActiveCount;

		/// <summary>
		/// Gets all active notifications.
		/// </summary>
		public IReadOnlyList<NotificationInfo> ActiveNotifications => CurrentState.ActiveNotifications;

		#endregion

		#region Events

		/// <summary>
		/// Occurs when a notification is shown.
		/// </summary>
		public event EventHandler<NotificationEventArgs>? NotificationShown;

		/// <summary>
		/// Occurs when a notification is dismissed.
		/// </summary>
		public event EventHandler<NotificationEventArgs>? NotificationDismissed;

		/// <summary>
		/// Occurs when all notifications are dismissed.
		/// </summary>
		public event EventHandler? AllNotificationsDismissed;

		/// <summary>
		/// Occurs when notification state changes.
		/// </summary>
		public event EventHandler<NotificationState>? StateChanged;

		#endregion

		#region Public Methods

		/// <summary>
		/// Shows a notification with the specified parameters.
		/// </summary>
		/// <param name="title">The notification title.</param>
		/// <param name="message">The notification message.</param>
		/// <param name="severity">The severity level.</param>
		/// <param name="blockUi">Whether to block UI (modal).</param>
		/// <param name="timeout">Auto-dismiss timeout in milliseconds (0 or null = no timeout).</param>
		/// <param name="parentWindow">Optional parent window for modal notifications.</param>
		/// <returns>The notification ID for later reference.</returns>
		public string ShowNotification(
			string title,
			string message,
			NotificationSeverity severity,
			bool blockUi = false,
			int? timeout = 5000,
			Window? parentWindow = null)
		{
			lock (_lock)
			{
				var id = GenerateNotificationId();
				var previousState = _currentState;

				_logService?.LogDebug($"Showing notification: {title} (ID: {id}, Severity: {severity.Name})", "Notification");

				// Create the notification window
				var notificationWindow = CreateNotificationWindow(title, message, severity, blockUi, parentWindow);

				// Create notification info
				var notificationInfo = new NotificationInfo(
					id,
					notificationWindow,
					string.IsNullOrWhiteSpace(title) ? severity.Name ?? "Notification" : title,
					message,
					severity,
					DateTime.Now,
					timeout,
					blockUi);

				// Add to active list
				_activeNotifications.Add(notificationInfo);

				// Add the window to the system
				_windowSystem.AddWindow(notificationWindow);
				_windowSystem.SetActiveWindow(notificationWindow);

				// Set up close button handler
				SetupCloseHandler(notificationWindow, id);

				// Set up timeout if specified
				if (timeout.HasValue && timeout.Value > 0)
				{
					SetupTimeout(id, timeout.Value);
				}

				// Update state
				UpdateStateInternal();

				// Fire events
				var args = new NotificationEventArgs(notificationInfo, previousState, _currentState);
				NotificationShown?.Invoke(this, args);

				return id;
			}
		}

		/// <summary>
		/// Dismisses a notification by ID.
		/// </summary>
		/// <param name="notificationId">The notification ID to dismiss.</param>
		/// <returns>True if the notification was found and dismissed; otherwise, false.</returns>
		public bool DismissNotification(string notificationId)
		{
			lock (_lock)
			{
				var notification = _activeNotifications.FirstOrDefault(n => n.Id == notificationId);
				if (notification == null)
					return false;

				return DismissNotificationInternal(notification);
			}
		}

		/// <summary>
		/// Dismisses a notification by its window.
		/// </summary>
		/// <param name="window">The notification window to dismiss.</param>
		/// <returns>True if the notification was found and dismissed; otherwise, false.</returns>
		public bool DismissNotification(Window window)
		{
			lock (_lock)
			{
				var notification = _activeNotifications.FirstOrDefault(n => n.Window == window);
				if (notification == null)
					return false;

				return DismissNotificationInternal(notification);
			}
		}

		/// <summary>
		/// Dismisses all active notifications.
		/// </summary>
		public void DismissAll()
		{
			lock (_lock)
			{
				if (_activeNotifications.Count == 0)
					return;

				_logService?.LogDebug($"Dismissing all notifications ({_activeNotifications.Count} active)", "Notification");

				// Copy list to avoid modification during iteration
				var notificationsToDismiss = _activeNotifications.ToList();

				foreach (var notification in notificationsToDismiss)
				{
					DismissNotificationInternal(notification, suppressEvents: true);
				}

				AllNotificationsDismissed?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Gets a notification by ID.
		/// </summary>
		/// <param name="notificationId">The notification ID to find.</param>
		/// <returns>The notification info if found; otherwise, null.</returns>
		public NotificationInfo? GetNotification(string notificationId)
		{
			lock (_lock)
			{
				return _activeNotifications.FirstOrDefault(n => n.Id == notificationId);
			}
		}

		/// <summary>
		/// Checks if a notification with the given ID exists.
		/// </summary>
		/// <param name="notificationId">The notification ID to check.</param>
		/// <returns>True if the notification exists; otherwise, false.</returns>
		public bool NotificationExists(string notificationId)
		{
			return GetNotification(notificationId) != null;
		}

		#endregion

		#region Private Methods

		private string GenerateNotificationId()
		{
			return $"notification_{++_notificationCounter}_{DateTime.Now.Ticks}";
		}

		private Window CreateNotificationWindow(
			string title,
			string message,
			NotificationSeverity severity,
			bool blockUi,
			Window? parentWindow)
		{
			var notificationWindow = new Window(_windowSystem, parentWindow)
			{
				Title = string.IsNullOrWhiteSpace(title) ? severity.Name ?? "Notification" : title,
				Left = _windowSystem.DesktopDimensions.Width / 2 - (AnsiConsoleHelper.StripSpectreLength(message) + 8) / 2,
				Top = _windowSystem.DesktopDimensions.Height / 2 - 2,
				Width = AnsiConsoleHelper.StripSpectreLength(message) + 8,
				Height = message.Split('\n').Length + 5,
				BackgroundColor = severity.WindowBackgroundColor(_windowSystem),
				ForegroundColor = _windowSystem.Theme.WindowForegroundColor,
				ActiveBorderForegroundColor = severity.ActiveBorderForegroundColor(_windowSystem),
				InactiveBorderForegroundColor = severity.InactiveBorderForegroundColor(_windowSystem),
				ActiveTitleForegroundColor = severity.ActiveTitleForegroundColor(_windowSystem),
				InactiveTitleForegroundColor = severity.InactiveTitleForegroundColor(_windowSystem),
				IsResizable = false
			};

			if (blockUi)
			{
				notificationWindow.Mode = WindowMode.Modal;
			}

			// Add content
			var notificationContent = new MarkupControl(new List<string>()
			{
				$"{severity.Icon}{(string.IsNullOrEmpty(severity.Icon) ? string.Empty : " ")}{message}"
			})
			{
				Alignment = Alignment.Left
			};
			notificationWindow.AddControl(notificationContent);

			// Add close button
			var closeButton = new ButtonControl()
			{
				Text = "Close",
				StickyPosition = StickyPosition.Bottom,
				Margin = new Margin() { Left = 1 }
			};
			notificationWindow.AddControl(closeButton);

			return notificationWindow;
		}

		private void SetupCloseHandler(Window notificationWindow, string notificationId)
		{
			// Find the close button and attach handler
			var closeButton = notificationWindow.GetControlsByType<ButtonControl>()
				.FirstOrDefault(b => b.Text == "Close");

			if (closeButton != null)
			{
				closeButton.Click += (sender, e) =>
				{
					DismissNotification(notificationId);
				};
			}
		}

		private void SetupTimeout(string notificationId, int timeoutMs)
		{
			var cts = new CancellationTokenSource();
			_timeoutCancellations[notificationId] = cts;

			Task.Run(async () =>
			{
				try
				{
					await Task.Delay(timeoutMs, cts.Token);
					if (!cts.Token.IsCancellationRequested)
					{
						DismissNotification(notificationId);
					}
				}
				catch (TaskCanceledException)
				{
					// Timeout was cancelled (notification dismissed manually)
				}
			}, cts.Token);
		}

		private bool DismissNotificationInternal(NotificationInfo notification, bool suppressEvents = false)
		{
			var previousState = _currentState;

			_logService?.LogDebug($"Dismissing notification: {notification.Title} (ID: {notification.Id})", "Notification");

			// Cancel timeout if active
			if (_timeoutCancellations.TryGetValue(notification.Id, out var cts))
			{
				cts.Cancel();
				cts.Dispose();
				_timeoutCancellations.Remove(notification.Id);
			}

			// Remove from active list
			_activeNotifications.Remove(notification);

			// Close the window
			_windowSystem.CloseWindow(notification.Window);

			// Update state
			UpdateStateInternal();

			// Fire events
			if (!suppressEvents)
			{
				var args = new NotificationEventArgs(notification, previousState, _currentState);
				NotificationDismissed?.Invoke(this, args);
			}

			return true;
		}

		private void UpdateStateInternal()
		{
			var previousState = _currentState;

			_currentState = new NotificationState(
				_activeNotifications.AsReadOnly(),
				previousState.TotalShown + (_activeNotifications.Count > previousState.ActiveCount ? 1 : 0),
				previousState.TotalDismissed + (previousState.ActiveCount > _activeNotifications.Count ? 1 : 0));

			// Add to history
			_stateHistory.Enqueue(previousState);
			while (_stateHistory.Count > MaxHistorySize)
			{
				_stateHistory.TryDequeue(out _);
			}

			StateChanged?.Invoke(this, _currentState);
		}

		#endregion

		#region IDisposable

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_isDisposed)
				return;

			_isDisposed = true;

			lock (_lock)
			{
				// Cancel all pending timeouts
				foreach (var cts in _timeoutCancellations.Values)
				{
					cts.Cancel();
					cts.Dispose();
				}
				_timeoutCancellations.Clear();

				// Clear active notifications (windows will be disposed by window system)
				_activeNotifications.Clear();
			}
		}

		#endregion
	}
}
