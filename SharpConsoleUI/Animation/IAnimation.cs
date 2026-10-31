// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Animation;

/// <summary>
/// Represents an active animation that can be updated and cancelled.
/// </summary>
public interface IAnimation
{
	/// <summary>Whether the animation has finished or been cancelled.</summary>
	bool IsComplete { get; }

	/// <summary>Advances the animation by the given delta time.</summary>
	void Update(TimeSpan deltaTime);

	/// <summary>Cancels the animation, marking it as complete.</summary>
	void Cancel();

	/// <summary>
	/// Whether this animation wants the real elapsed time rather than the
	/// manager's capped delta. Defaults to false, which is the capped delta.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="AnimationManager.Update"/> caps every delta at
	/// <see cref="Configuration.AnimationDefaults.MaxFrameDeltaMs"/> so that a
	/// tween does not jump to its final value after the application sat idle and
	/// drew no frames. That is the right failure for appearance over time: a
	/// transition would rather take an extra frame than skip to the end.
	/// </para>
	/// <para>
	/// Simulation over time wants the opposite. A fixed-step loop would rather
	/// drop steps than have its own clock stretched, because stretching makes it
	/// run in slow motion whenever frames are slow. Such an animation asks for the
	/// raw delta and clamps it itself, bounding catch-up work rather than
	/// smoothness.
	/// </para>
	/// <para>
	/// It also makes a frozen clock steppable by more than one capped frame:
	/// advancing a frozen animation clock by a second through the diagnostics
	/// socket reaches an uncapped animation as a second.
	/// </para>
	/// </remarks>
	bool WantsUncappedDelta => false;
}
