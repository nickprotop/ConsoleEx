// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Rectangle = System.Drawing.Rectangle;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Specifies where a toast notification is anchored on the screen.
	/// </summary>
	public enum ToastPosition
	{
		/// <summary>Anchored to the bottom-right corner.</summary>
		BottomRight,
		/// <summary>Anchored to the top-right corner.</summary>
		TopRight,
		/// <summary>Anchored to the bottom-left corner.</summary>
		BottomLeft,
		/// <summary>Anchored to the top-left corner.</summary>
		TopLeft,
		/// <summary>Anchored to the bottom-center of the screen.</summary>
		BottomCenter
	}

	/// <summary>
	/// Optional per-toast configuration overriding service defaults.
	/// </summary>
	/// <param name="Timeout">Auto-dismiss timeout in milliseconds, or <c>null</c> to use the service default.</param>
	/// <param name="Sticky">When <c>true</c>, the toast does not auto-dismiss and must be dismissed explicitly.</param>
	/// <param name="Position">The screen position, or <c>null</c> to use the service default.</param>
	public sealed record ToastOptions(
		int? Timeout = null,
		bool Sticky = false,
		ToastPosition? Position = null);

	/// <summary>
	/// Immutable description of a single active toast.
	/// </summary>
	/// <param name="Id">Unique identifier for the toast.</param>
	/// <param name="Message">The message text to display.</param>
	/// <param name="Severity">The severity level determining the visual style.</param>
	/// <param name="Position">The screen position where the toast is anchored.</param>
	/// <param name="Sticky">Whether the toast is sticky (does not auto-dismiss).</param>
	public record ToastInfo(
		string Id,
		string Message,
		NotificationSeverity Severity,
		ToastPosition Position,
		bool Sticky);

	/// <summary>
	/// Immutable snapshot of the toast subsystem state.
	/// </summary>
	/// <param name="ActiveToasts">The currently visible toasts.</param>
	/// <param name="TotalShown">The cumulative count of toasts shown since creation.</param>
	/// <param name="TotalDismissed">The cumulative count of toasts dismissed since creation.</param>
	public record ToastState(
		IReadOnlyList<ToastInfo> ActiveToasts,
		int TotalShown,
		int TotalDismissed)
	{
		/// <summary>Gets an empty state with no active toasts and zero counters.</summary>
		public static ToastState Empty => new(Array.Empty<ToastInfo>(), 0, 0);

		/// <summary>Gets a value indicating whether any toasts are currently active.</summary>
		public bool HasToasts => ActiveToasts.Count > 0;

		/// <summary>Gets the number of currently active toasts.</summary>
		public int ActiveCount => ActiveToasts.Count;
	}

	/// <summary>
	/// Event data describing a toast state transition.
	/// </summary>
	public class ToastEventArgs : EventArgs
	{
		/// <summary>Gets the toast that triggered the event.</summary>
		public ToastInfo Toast { get; }

		/// <summary>Gets the state prior to the transition.</summary>
		public ToastState PreviousState { get; }

		/// <summary>Gets the state after the transition.</summary>
		public ToastState CurrentState { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ToastEventArgs"/> class.
		/// </summary>
		/// <param name="toast">The toast that triggered the event.</param>
		/// <param name="previous">The state prior to the transition.</param>
		/// <param name="current">The state after the transition.</param>
		public ToastEventArgs(ToastInfo toast, ToastState previous, ToastState current)
		{
			Toast = toast;
			PreviousState = previous;
			CurrentState = current;
		}
	}

	/// <summary>
	/// Abstraction for scheduling a delayed callback used to auto-dismiss toasts.
	/// </summary>
	public interface IToastScheduler
	{
		/// <summary>
		/// Schedules <paramref name="callback"/> to run after <paramref name="delayMs"/> milliseconds.
		/// </summary>
		/// <param name="delayMs">The delay in milliseconds before invoking the callback.</param>
		/// <param name="callback">The callback to invoke after the delay.</param>
		void Schedule(int delayMs, Action callback);
	}

	/// <summary>
	/// Default <see cref="IToastScheduler"/> that delays via <see cref="System.Threading.Tasks.Task.Delay(int)"/>
	/// and marshals the callback back to the UI thread.
	/// </summary>
	public sealed class TaskToastScheduler : IToastScheduler
	{
		private readonly Action<Action> _post;

		/// <summary>
		/// Initializes a new instance of the <see cref="TaskToastScheduler"/> class.
		/// </summary>
		/// <param name="postToUiThread">A delegate that marshals the supplied action onto the UI thread.</param>
		public TaskToastScheduler(Action<Action> postToUiThread) => _post = postToUiThread;

		/// <inheritdoc/>
		public void Schedule(int delayMs, Action callback)
			=> _ = System.Threading.Tasks.Task.Delay(delayMs).ContinueWith(_ => _post(callback));
	}

	/// <summary>
	/// Manages transient toast notifications: showing, auto-dismissing, and stacking.
	/// </summary>
	/// <remarks>
	/// The service is observable: it raises <see cref="System.ComponentModel.INotifyPropertyChanged"/> for derived
	/// properties (<see cref="HasToasts"/>, <see cref="ActiveCount"/>, <see cref="DefaultPosition"/>),
	/// exposes an <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/> of active toasts,
	/// and fires <see cref="ToastShown"/>/<see cref="ToastDismissed"/>/<see cref="AllToastsDismissed"/>/<see cref="StateChanged"/>.
	/// Portal creation/removal and slot reflow are <c>protected virtual</c> seams so a derived
	/// type can wire the toasts into the live window system without coupling the core to rendering.
	/// </remarks>
	public class ToastService : System.ComponentModel.INotifyPropertyChanged, IDisposable
	{
		private readonly ConsoleWindowSystem _windowSystem;
		private readonly Logging.ILogService? _log;
		private readonly IToastScheduler _scheduler;
		private readonly object _lock = new();

		private readonly Dictionary<string, ToastEntry> _entries = new();
		private readonly System.Collections.ObjectModel.ObservableCollection<ToastInfo> _activeToasts = new();
		private ToastState _currentState = ToastState.Empty;
		private int _idSeq;
		private ToastPosition _defaultPosition = ToastPosition.BottomRight;

		/// <inheritdoc/>
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

		/// <summary>Raised when a new toast is shown.</summary>
		public event EventHandler<ToastEventArgs>? ToastShown;

		/// <summary>Raised when an individual toast is dismissed.</summary>
		public event EventHandler<ToastEventArgs>? ToastDismissed;

		/// <summary>Raised when all toasts are dismissed at once via <see cref="DismissAll"/>.</summary>
		public event EventHandler? AllToastsDismissed;

		/// <summary>Raised whenever the toast subsystem state changes.</summary>
		public event EventHandler<ToastState>? StateChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="ToastService"/> class.
		/// </summary>
		/// <param name="windowSystem">The owning window system, used to marshal callbacks to the UI thread.</param>
		/// <param name="logService">Optional log service for diagnostics.</param>
		/// <param name="scheduler">Optional auto-dismiss scheduler; defaults to a <see cref="TaskToastScheduler"/>.</param>
		public ToastService(ConsoleWindowSystem windowSystem, Logging.ILogService? logService = null, IToastScheduler? scheduler = null)
		{
			_windowSystem = windowSystem;
			_log = logService;
			_scheduler = scheduler ?? new TaskToastScheduler(a => _windowSystem.EnqueueOnUIThread(a));
		}

		/// <summary>Gets the observable collection of currently active toasts.</summary>
		public System.Collections.ObjectModel.ObservableCollection<ToastInfo> ActiveToasts => _activeToasts;

		/// <summary>Gets an immutable snapshot of the current toast state.</summary>
		public ToastState CurrentState { get { lock (_lock) return _currentState; } }

		/// <summary>Gets a value indicating whether any toasts are currently active.</summary>
		public bool HasToasts => CurrentState.HasToasts;

		/// <summary>Gets the number of currently active toasts.</summary>
		public int ActiveCount => CurrentState.ActiveCount;

		/// <summary>Gets or sets the default position used for toasts that do not specify one.</summary>
		public ToastPosition DefaultPosition
		{
			get => _defaultPosition;
			set { if (_defaultPosition != value) { _defaultPosition = value; Raise(nameof(DefaultPosition)); } }
		}

		/// <summary>
		/// Shows a toast with the given message and severity using default options.
		/// </summary>
		/// <param name="message">The message text to display.</param>
		/// <param name="severity">The severity level determining the visual style.</param>
		/// <returns>The unique identifier of the new toast.</returns>
		public string Show(string message, NotificationSeverity severity)
			=> Show(message, severity, new ToastOptions());

		/// <summary>
		/// Shows a toast with the given message, severity, and explicit options.
		/// </summary>
		/// <param name="message">The message text to display.</param>
		/// <param name="severity">The severity level determining the visual style.</param>
		/// <param name="options">Per-toast options overriding service defaults.</param>
		/// <returns>The unique identifier of the new toast.</returns>
		public string Show(string message, NotificationSeverity severity, ToastOptions options)
		{
			var position = options.Position ?? _defaultPosition;
			var info = new ToastInfo($"toast-{System.Threading.Interlocked.Increment(ref _idSeq)}", message, severity, position, options.Sticky);
			var content = new Controls.ToastContent(message, severity, MapSeverityToRole(severity.Severity)) { Sticky = options.Sticky };
			content.DismissRequested += (_, _) => Dismiss(info.Id);

			object handle;
			ToastState prev, cur;
			lock (_lock)
			{
				handle = CreatePortalFor(content, info);
				_entries[info.Id] = new ToastEntry(info, content, handle);
				prev = _currentState;
				cur = _currentState = new ToastState(BuildActiveList(), _currentState.TotalShown + 1, _currentState.TotalDismissed);
				ReflowSlots();
			}
			_activeToasts.Add(info);
			ToastShown?.Invoke(this, new ToastEventArgs(info, prev, cur));
			StateChanged?.Invoke(this, cur);
			Raise(nameof(HasToasts)); Raise(nameof(ActiveCount));

			if (!options.Sticky)
			{
				int timeout = options.Timeout ?? DefaultTimeoutFor(severity.Severity);
				if (timeout > 0) _scheduler.Schedule(timeout, () => Dismiss(info.Id));
			}
			return info.Id;
		}

		/// <summary>
		/// Dismisses the toast with the given identifier, if present.
		/// </summary>
		/// <param name="id">The identifier of the toast to dismiss.</param>
		/// <returns><c>true</c> if a toast was dismissed; <c>false</c> if no matching toast existed.</returns>
		public bool Dismiss(string id)
		{
			ToastEntry entry; ToastState prev, cur;
			lock (_lock)
			{
				if (!_entries.TryGetValue(id, out entry)) return false;
				RemovePortalFor(entry.Handle);
				_entries.Remove(id);
				prev = _currentState;
				cur = _currentState = new ToastState(BuildActiveList(), _currentState.TotalShown, _currentState.TotalDismissed + 1);
				ReflowSlots();
			}
			_activeToasts.Remove(entry.Info);
			ToastDismissed?.Invoke(this, new ToastEventArgs(entry.Info, prev, cur));
			StateChanged?.Invoke(this, cur);
			Raise(nameof(HasToasts)); Raise(nameof(ActiveCount));
			return true;
		}

		/// <summary>
		/// Dismisses all active toasts at once.
		/// </summary>
		public void DismissAll()
		{
			lock (_lock)
			{
				foreach (var e in _entries.Values) RemovePortalFor(e.Handle);
				_entries.Clear();
				_currentState = new ToastState(Array.Empty<ToastInfo>(), _currentState.TotalShown, _currentState.TotalDismissed);
			}
			_activeToasts.Clear();
			AllToastsDismissed?.Invoke(this, EventArgs.Empty);
			StateChanged?.Invoke(this, CurrentState);
			Raise(nameof(HasToasts)); Raise(nameof(ActiveCount));
		}

		/// <inheritdoc/>
		public void Dispose() => DismissAll();

		/// <summary>Creates the portal/window handle for a toast. Override to wire into the live system.</summary>
		/// <param name="content">The toast content control.</param>
		/// <param name="info">The toast descriptor.</param>
		/// <returns>An opaque handle later passed to <see cref="RemovePortalFor"/>.</returns>
		protected virtual object CreatePortalFor(Controls.ToastContent content, ToastInfo info)
		{
			content.Container = _windowSystem.ActiveWindow;
			var bounds = BoundsFor(info, _entries.Count); // appended last (called under lock before the add)
			content.SetBounds(bounds);
			var dims = _windowSystem.DesktopDimensions;
			return _windowSystem.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
				Content: content,
				Bounds: bounds,
				DismissOnClickOutside: false,
				ConsumeClickOnDismiss: false,
				BufferSize: new System.Drawing.Size(dims.Width, dims.Height),
				BufferOrigin: new System.Drawing.Point(0, 0)));
		}

		/// <summary>Removes a previously-created portal/window handle.</summary>
		/// <param name="handle">The handle returned by <see cref="CreatePortalFor"/>.</param>
		protected virtual void RemovePortalFor(object handle)
		{
			if (handle is DesktopPortal p) _windowSystem.DesktopPortalService.RemovePortal(p);
		}

		/// <summary>Recomputes stacking slots after the active set changes. Called under the lock.</summary>
		protected virtual void ReflowSlots()
		{
			int i = 0;
			foreach (var e in _entries.Values)
			{
				var b = BoundsFor(e.Info, i++);
				e.Content.SetBounds(b);
				if (e.Handle is DesktopPortal dp) dp.Bounds = b;
			}
		}

		/// <summary>
		/// Computes the screen-absolute bounds for a toast occupying the given stacking slot.
		/// </summary>
		/// <param name="pos">The anchor position on the screen.</param>
		/// <param name="slotIndex">The zero-based stacking slot, growing away from the anchored edge.</param>
		/// <param name="w">The toast width in columns.</param>
		/// <param name="dw">The desktop width in columns.</param>
		/// <param name="dh">The desktop height in rows.</param>
		/// <returns>The screen-absolute bounds rectangle for the toast.</returns>
		internal static Rectangle ComputeToastBounds(ToastPosition pos, int slotIndex, int w, int dw, int dh)
		{
			int margin = Configuration.ControlDefaults.ToastEdgeMargin;
			int gap = Configuration.ControlDefaults.ToastGap;
			int h = 3; // border + 1 content line (single-line v1)
			int slotOffset = slotIndex * (h + gap);

			int x = pos switch
			{
				ToastPosition.BottomRight or ToastPosition.TopRight => dw - w - margin,
				ToastPosition.BottomLeft or ToastPosition.TopLeft => margin,
				ToastPosition.BottomCenter => (dw - w) / 2,
				_ => dw - w - margin,
			};
			bool top = pos is ToastPosition.TopRight or ToastPosition.TopLeft;
			int y = top ? margin + slotOffset : dh - h - margin - slotOffset;
			return new Rectangle(x, y, w, h);
		}

		private int ContentWidth(ToastInfo info)
		{
			string s = (string.IsNullOrEmpty(info.Severity.Icon) ? "" : info.Severity.Icon + "  ") + info.Message;
			// +5 chrome: left border + accent bar + leading space + right border + trailing pad
			return Math.Min(Configuration.ControlDefaults.ToastMaxWidth, Parsing.MarkupParser.StripLength(s) + 5);
		}

		private Rectangle BoundsFor(ToastInfo info, int slotIndex)
		{
			var dims = _windowSystem.DesktopDimensions;
			return ComputeToastBounds(info.Position, slotIndex, ContentWidth(info), dims.Width, dims.Height);
		}

		private IReadOnlyList<ToastInfo> BuildActiveList()
		{
			var list = new List<ToastInfo>(_entries.Count);
			foreach (var e in _entries.Values) list.Add(e.Info);
			return list;
		}

		private void Raise(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

		private static int DefaultTimeoutFor(NotificationSeverityEnum sev) => sev switch
		{
			NotificationSeverityEnum.Danger => Configuration.ControlDefaults.ToastErrorTimeoutMs,
			_ => Configuration.ControlDefaults.ToastDefaultTimeoutMs,
		};

		private static Themes.ColorRole MapSeverityToRole(NotificationSeverityEnum sev) => sev switch
		{
			NotificationSeverityEnum.Danger => Themes.ColorRole.Danger,
			NotificationSeverityEnum.Warning => Themes.ColorRole.Warning,
			NotificationSeverityEnum.Success => Themes.ColorRole.Success,
			NotificationSeverityEnum.Info => Themes.ColorRole.Info,
			_ => Themes.ColorRole.Default,
		};

		private readonly struct ToastEntry
		{
			public readonly ToastInfo Info; public readonly Controls.ToastContent Content; public readonly object Handle;
			public ToastEntry(ToastInfo info, Controls.ToastContent content, object handle) { Info = info; Content = content; Handle = handle; }
		}
	}
}
