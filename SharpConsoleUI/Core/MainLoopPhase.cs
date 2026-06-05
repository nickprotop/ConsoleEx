// SharpConsoleUI/Core/MainLoopPhase.cs
namespace SharpConsoleUI.Core;

/// <summary>
/// Identifies which step of the main loop was executing — surfaced in UnresponsiveEventArgs
/// to attribute a stall to input handling, UI-action draining, rendering, or idle wait.
/// </summary>
public enum MainLoopPhase
{
	/// <summary>The executing phase is not known.</summary>
	Unknown,
	/// <summary>Processing queued input (key/mouse dispatch to controls).</summary>
	Input,
	/// <summary>Draining the UI action queue (work marshalled from background threads).</summary>
	Drain,
	/// <summary>Rendering the display (layout, paint, console output).</summary>
	Render,
	/// <summary>Idle — waiting for the next wake signal or timeout.</summary>
	Idle
}
