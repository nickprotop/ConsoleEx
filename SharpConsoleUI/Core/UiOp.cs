// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// SharpConsoleUI/Core/UiOp.cs
namespace SharpConsoleUI.Core;

/// <summary>
/// The kind of UI-thread operation currently executing, used to build the best-effort
/// <see cref="UnresponsiveEventArgs.BlockedIn"/> breadcrumb when the main loop stalls.
/// </summary>
internal enum UiOp
{
	/// <summary>No operation is being tracked.</summary>
	None = 0,
	/// <summary>A keyboard handler (ProcessKey) is running.</summary>
	Key,
	/// <summary>A mouse click handler is running.</summary>
	Click,
	/// <summary>A mouse scroll handler is running.</summary>
	MouseScroll,
	/// <summary>A control paint/render callback is running.</summary>
	Render,
	/// <summary>A queued UI action (EnqueueOnUIThread / async continuation) is running.</summary>
	UIAction
}
