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
/// Registering an animation says whether it took.
/// </summary>
/// <remarks>
/// <see cref="AnimationManager.Add"/> returns void and drops the animation when
/// the manager is full or disabled, which for a decorative tween is a frame
/// nobody misses. For anything that must actually run, a silent no-op is an
/// afternoon of debugging, so TryAdd reports what happened. Add keeps its
/// behaviour exactly.
/// </remarks>
public class TryAddTests
{
	/// <summary>An animation that never finishes, so it holds its slot.</summary>
	private sealed class Forever : IAnimation
	{
		public bool IsComplete => false;

		public void Update(TimeSpan deltaTime) { }

		public void Cancel() { }
	}

	[Fact]
	public void TryAddReportsTrueWhenTheAnimationIsRegistered()
	{
		var manager = new AnimationManager();

		Assert.True(manager.TryAdd(new Forever()));
		Assert.Equal(1, manager.ActiveCount);
	}

	[Fact]
	public void TryAddReportsFalseWhenTheManagerIsFull()
	{
		var manager = new AnimationManager();
		for (int i = 0; i < AnimationDefaults.MaxConcurrentAnimations; i++)
		{
			Assert.True(manager.TryAdd(new Forever()));
		}

		Assert.False(manager.TryAdd(new Forever()));
		Assert.Equal(AnimationDefaults.MaxConcurrentAnimations, manager.ActiveCount);
	}

	[Fact]
	public void TryAddReportsFalseWhenAnimationsAreDisabled()
	{
		var manager = new AnimationManager { IsEnabled = false };

		Assert.False(manager.TryAdd(new Forever()));
		Assert.Equal(0, manager.ActiveCount);
	}

	/// <summary>
	/// The existing void Add is unchanged: it still drops silently.
	/// </summary>
	[Fact]
	public void AddStillAcceptsAndStillDropsSilently()
	{
		var manager = new AnimationManager();
		manager.Add(new Forever());
		Assert.Equal(1, manager.ActiveCount);

		var disabled = new AnimationManager { IsEnabled = false };
		disabled.Add(new Forever());
		Assert.Equal(0, disabled.ActiveCount);
	}
}
