// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Which clock each part of the system runs on.
/// </summary>
/// <remarks>
/// <para>
/// One provider for everything is the common case, and stays one line:
/// <c>new TimeOptions { All = clock }</c>. The named clocks exist because the
/// parts measure different things, and a test usually wants to control one of them
/// without stopping the rest - freezing the mouse clock to step a double click
/// apart should not also freeze a progress bar.
/// </para>
/// <para>
/// Each resolves <b>named, then <see cref="All"/>, then
/// <see cref="TimeProvider.System"/></b>. A clock not named here runs on the real
/// one, so configuring nothing changes nothing.
/// </para>
/// <para>
/// Frame PACING is deliberately absent. Frame-rate limiting is a statement about
/// the terminal - do not redraw more than TargetFPS times a second - so it belongs
/// to the machine clock and not to any of these. Pacing on a configurable clock
/// means a frozen one renders nothing at all and an application cannot start.
/// </para>
/// </remarks>
public sealed record TimeOptions
{
	/// <summary>
	/// The clock every part runs on unless it names its own.
	/// </summary>
	/// <remarks>
	/// Not called Default: this type already lives beside options whose
	/// <c>Default</c> is a ready-made instance of themselves, and the two meanings
	/// would read the same and mean different things.
	/// </remarks>
	public TimeProvider? All { get; init; }

	/// <summary>
	/// The multi-click window: how long after one click a second one is a double.
	/// </summary>
	public TimeProvider? Mouse { get; init; }

	/// <summary>
	/// The cadence of controls that repaint themselves - a panel clock, an FPS
	/// readout, an indeterminate progress bar.
	/// </summary>
	public TimeProvider? ScheduleRepaint { get; init; }

	/// <summary>
	/// The per-frame animation delta: every tween, and animated desktop backgrounds.
	/// </summary>
	public TimeProvider? Animation { get; init; }

	/// <summary>Resolves one clock against this configuration.</summary>
	/// <param name="named">The named clock, or null when none was given.</param>
	internal TimeProvider Resolve(TimeProvider? named) =>
		named ?? All ?? TimeProvider.System;
}
