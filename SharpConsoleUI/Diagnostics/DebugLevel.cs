// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Diagnostics
{
	/// <summary>
	/// Debug logging levels for flicker and performance debugging.
	/// </summary>
	[Flags]
	public enum DebugLevel
	{
		None = 0,
		FRAME = 1 << 0,          // Frame boundaries, render triggers, timing
		DOM = 1 << 1,            // Tree rebuilds, measure/arrange cycles
		BUFFER = 1 << 2,         // Clear operations, dirty regions, cell writes
		PAINT = 1 << 3,          // Paint calls, clip rectangles, control rendering
		INVALIDATE = 1 << 4,     // Invalidation requests, batching, propagation
		ANSI = 1 << 5,           // ANSI sequence generation and output
		INPUT = 1 << 6,          // Keyboard/mouse events, focus changes
		CONTROL = 1 << 7,        // Individual control render operations, size changes
		WINDOW = 1 << 8,         // Window operations (create, close, move, resize, z-order)
		MODAL = 1 << 9,          // Modal push/pop, blocking state
		NOTIFICATION = 1 << 10,  // Notification show/hide/timeout
		STATUS = 1 << 11,        // TopStatus/BottomStatus rendering
		THEME = 1 << 12,         // Theme resolution, color lookups
		FOCUS = 1 << 13,         // Focus changes, tab navigation
		EVENT = 1 << 14,         // Event firing, handler invocation
		All = ~0                 // All levels enabled
	}
}
