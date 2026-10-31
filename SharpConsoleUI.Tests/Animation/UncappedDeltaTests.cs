// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using Xunit;

namespace SharpConsoleUI.Tests.Animation;

/// <summary>
/// An animation can own its own time policy.
/// </summary>
/// <remarks>
/// The manager caps every delta at <see cref="AnimationDefaults.MaxFrameDeltaMs"/>
/// so a tween does not jump to its end after an idle period. That is right for
/// appearance over time and wrong for simulation over time: a simulation would
/// rather drop steps than stretch its own clock. Asking for the raw delta is how
/// it says so, and it is also what makes a frozen clock steppable by more than
/// one capped frame at a time.
/// </remarks>
public class UncappedDeltaTests
{
	/// <summary>Records what delta it was handed, and asks for whichever policy it was built with.</summary>
	private sealed class DeltaRecorder : IAnimation
	{
		private readonly bool _uncapped;

		internal DeltaRecorder(bool uncapped) => _uncapped = uncapped;

		internal TimeSpan Last { get; private set; }

		public bool IsComplete => false;

		public bool WantsUncappedDelta => _uncapped;

		public void Update(TimeSpan deltaTime) => Last = deltaTime;

		public void Cancel() { }
	}

	[Fact]
	public void AnAnimationThatAsksForTheRawDeltaGetsIt()
	{
		var manager = new AnimationManager();
		var recorder = new DeltaRecorder(uncapped: true);
		manager.Add(recorder);

		var big = TimeSpan.FromSeconds(1);
		manager.Update(big);

		Assert.Equal(big, recorder.Last);
	}

	[Fact]
	public void AnAnimationThatDoesNotAskIsStillCapped()
	{
		var manager = new AnimationManager();
		var recorder = new DeltaRecorder(uncapped: false);
		manager.Add(recorder);

		manager.Update(TimeSpan.FromSeconds(1));

		Assert.Equal(
			TimeSpan.FromMilliseconds(AnimationDefaults.MaxFrameDeltaMs),
			recorder.Last);
	}

	/// <summary>
	/// The default is the existing behaviour, so no implementation outside this
	/// assembly has to change.
	/// </summary>
	[Fact]
	public void TheDefaultForTheInterfaceIsCapped()
	{
		var plain = new FrameCycleAnimation(4, TimeSpan.FromMilliseconds(80), _ => { });

		Assert.False(((IAnimation)plain).WantsUncappedDelta);
	}

	/// <summary>
	/// Both policies in one manager: the cap is per animation, not per Update call.
	/// </summary>
	[Fact]
	public void TwoAnimationsInOneUpdateEachGetTheirOwnPolicy()
	{
		var manager = new AnimationManager();
		var raw = new DeltaRecorder(uncapped: true);
		var capped = new DeltaRecorder(uncapped: false);
		manager.Add(raw);
		manager.Add(capped);

		manager.Update(TimeSpan.FromSeconds(2));

		Assert.Equal(TimeSpan.FromSeconds(2), raw.Last);
		Assert.Equal(
			TimeSpan.FromMilliseconds(AnimationDefaults.MaxFrameDeltaMs),
			capped.Last);
	}
}
