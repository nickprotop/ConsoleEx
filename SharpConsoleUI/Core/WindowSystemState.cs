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
	/// Immutable snapshot of the entire window system state.
	/// Provides a single source of truth for window management.
	/// </summary>
	public record WindowSystemState
	{
		/// <summary>
		/// The currently active window (receives keyboard input)
		/// </summary>
		public Window? ActiveWindow { get; init; }

		/// <summary>
		/// All registered windows indexed by their GUID
		/// </summary>
		public IReadOnlyDictionary<string, Window> Windows { get; init; } = new Dictionary<string, Window>();

		/// <summary>
		/// Current mouse interaction state (drag/resize)
		/// </summary>
		public InteractionState Interaction { get; init; } = InteractionState.None;

		/// <summary>
		/// Timestamp when this state was created
		/// </summary>
		public DateTime UpdateTime { get; init; }

		/// <summary>
		/// Empty initial state
		/// </summary>
		public static readonly WindowSystemState Empty = new()
		{
			ActiveWindow = null,
			Windows = new Dictionary<string, Window>(),
			Interaction = InteractionState.None,
			UpdateTime = DateTime.UtcNow
		};

		/// <summary>
		/// Gets the number of registered windows
		/// </summary>
		public int WindowCount => Windows.Count;

		/// <summary>
		/// Gets windows ordered by Z-index (back to front)
		/// </summary>
		public IReadOnlyList<Window> GetWindowsByZOrder()
		{
			return Windows.Values.OrderBy(w => w.ZIndex).ToList();
		}

		/// <summary>
		/// Gets only visible windows (excludes minimized)
		/// </summary>
		public IReadOnlyList<Window> GetVisibleWindows()
		{
			return Windows.Values
				.Where(w => w.State != WindowState.Minimized)
				.OrderBy(w => w.ZIndex)
				.ToList();
		}

		/// <summary>
		/// Gets the maximum Z-index among all windows
		/// </summary>
		public int GetMaxZIndex()
		{
			return Windows.Count > 0 ? Windows.Values.Max(w => w.ZIndex) : 0;
		}

		/// <summary>
		/// Checks if active window has changed compared to another state
		/// </summary>
		public bool HasActiveWindowChanged(WindowSystemState other)
		{
			return ActiveWindow != other.ActiveWindow;
		}

		/// <summary>
		/// Checks if interaction state has changed compared to another state
		/// </summary>
		public bool HasInteractionChanged(WindowSystemState other)
		{
			return Interaction != other.Interaction;
		}

		/// <summary>
		/// Checks if window collection has changed compared to another state
		/// </summary>
		public bool HasWindowsChanged(WindowSystemState other)
		{
			return Windows.Count != other.Windows.Count ||
			       !Windows.Keys.SequenceEqual(other.Windows.Keys);
		}
	}

	/// <summary>
	/// Current mouse interaction state (drag or resize operation)
	/// </summary>
	public record InteractionState
	{
		/// <summary>
		/// Whether a drag operation is in progress
		/// </summary>
		public bool IsDragging { get; init; }

		/// <summary>
		/// Whether a resize operation is in progress
		/// </summary>
		public bool IsResizing { get; init; }

		/// <summary>
		/// Current drag operation state (if dragging)
		/// </summary>
		public DragState? Drag { get; init; }

		/// <summary>
		/// Current resize operation state (if resizing)
		/// </summary>
		public ResizeState? Resize { get; init; }

		/// <summary>
		/// No active interaction
		/// </summary>
		public static readonly InteractionState None = new()
		{
			IsDragging = false,
			IsResizing = false,
			Drag = null,
			Resize = null
		};

		/// <summary>
		/// Creates a new dragging state
		/// </summary>
		public static InteractionState StartDrag(Window window, Point mousePos, Point windowPos)
		{
			return new InteractionState
			{
				IsDragging = true,
				IsResizing = false,
				Drag = new DragState(window, mousePos, windowPos),
				Resize = null
			};
		}

		/// <summary>
		/// Creates a new resizing state
		/// </summary>
		public static InteractionState StartResize(Window window, ResizeDirection direction, Point mousePos, Size windowSize, Point windowPos)
		{
			return new InteractionState
			{
				IsDragging = false,
				IsResizing = true,
				Drag = null,
				Resize = new ResizeState(window, direction, mousePos, windowSize, windowPos)
			};
		}
	}

	/// <summary>
	/// State of an active drag operation
	/// </summary>
	public record DragState(
		/// <summary>Window being dragged</summary>
		Window Window,
		/// <summary>Mouse position when drag started</summary>
		Point StartMousePos,
		/// <summary>Window position when drag started</summary>
		Point StartWindowPos
	);

	/// <summary>
	/// State of an active resize operation
	/// </summary>
	public record ResizeState(
		/// <summary>Window being resized</summary>
		Window Window,
		/// <summary>Direction of the resize</summary>
		ResizeDirection Direction,
		/// <summary>Mouse position when resize started</summary>
		Point StartMousePos,
		/// <summary>Window size when resize started</summary>
		Size StartWindowSize,
		/// <summary>Window position when resize started</summary>
		Point StartWindowPos
	);
}
