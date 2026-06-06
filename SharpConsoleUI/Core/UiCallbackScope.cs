// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// SharpConsoleUI/Core/UiCallbackScope.cs
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core;

/// <summary>
/// Sets the current UI-thread "frame" breadcrumb for the duration of a handler invocation and
/// restores the previous frame on dispose. Allocation-free (ref struct, captured fields only).
/// Use with a <c>using</c> statement around a handler dispatch so the watchdog can name where a
/// stall occurred. Nested scopes degrade to "innermost wins, outer restored on exit".
/// </summary>
/// <remarks>
/// The frame fields on <see cref="ConsoleWindowSystem"/> are volatile and read from the watchdog
/// timer thread. Publication across the (window, control, op, label) fields is not atomic; a torn
/// read yields a slightly inconsistent best-effort label during the microsecond of a swap, which
/// matches the documented "best-effort" contract of <see cref="UnresponsiveEventArgs.BlockedIn"/>.
/// </remarks>
internal readonly ref struct UiCallbackScope
{
	private readonly ConsoleWindowSystem? _sys;
	private readonly Window? _prevWindow;
	private readonly IWindowControl? _prevControl;
	private readonly UiOp _prevOp;
	private readonly string? _prevLabel;

	/// <summary>Begins a structured frame (input / render) naming the window, control, and operation.</summary>
	public UiCallbackScope(ConsoleWindowSystem? sys, Window? window, IWindowControl? control, UiOp op)
	{
		_sys = sys;
		if (sys is null)
		{
			_prevWindow = null; _prevControl = null; _prevOp = UiOp.None; _prevLabel = null;
			return;
		}
		sys.CaptureFrame(out _prevWindow, out _prevControl, out _prevOp, out _prevLabel);
		sys.SetFrame(window, control, op);
	}

	/// <summary>Begins a free-form frame (drained UI actions / async continuations) with a label.</summary>
	public UiCallbackScope(ConsoleWindowSystem? sys, string label)
	{
		_sys = sys;
		if (sys is null)
		{
			_prevWindow = null; _prevControl = null; _prevOp = UiOp.None; _prevLabel = null;
			return;
		}
		sys.CaptureFrame(out _prevWindow, out _prevControl, out _prevOp, out _prevLabel);
		sys.SetFrameLabel(label);
	}

	/// <summary>Restores the frame captured when this scope was created.</summary>
	public void Dispose()
	{
		_sys?.RestoreFrame(_prevWindow, _prevControl, _prevOp, _prevLabel);
	}
}
