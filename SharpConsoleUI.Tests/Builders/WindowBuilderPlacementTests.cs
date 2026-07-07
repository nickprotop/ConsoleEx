// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests
{
	// Verifies WindowBuilder.WithPlacement: the built window carries the declarative Placement and,
	// once added to the live system, occupies the bounds the WindowPlacementService resolves for it.
	// Also verifies a placement overrides an explicit WithSize (placement bounds win).
	public class WindowBuilderPlacementTests
	{
		[Fact]
		public void WithPlacement_Snap_SetsPlacementAndResolvesBounds()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);

			var window = new WindowBuilder(system)
				.WithPlacement(Placement.Snap(SnapZone.RightHalf))
				.Build();
			system.AddWindow(window);

			var expected = system.WindowPlacementService.Resolve(Placement.Snap(SnapZone.RightHalf));

			Assert.Equal(Placement.Snap(SnapZone.RightHalf), window.Placement);
			Assert.Equal(expected.X, window.Left);
			Assert.Equal(expected.Y, window.Top);
			Assert.Equal(expected.Width, window.Width);
			Assert.Equal(expected.Height, window.Height);
		}

		[Fact]
		public void WithPlacement_OverridesExplicitWithSize()
		{
			var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);

			var window = new WindowBuilder(system)
				.WithSize(20, 10)
				.WithPlacement(Placement.Snap(SnapZone.RightHalf))
				.Build();
			system.AddWindow(window);

			var expected = system.WindowPlacementService.Resolve(Placement.Snap(SnapZone.RightHalf));

			// The placement bounds win over the explicit 20x10 size.
			Assert.Equal(Placement.Snap(SnapZone.RightHalf), window.Placement);
			Assert.Equal(expected.Width, window.Width);
			Assert.Equal(expected.Height, window.Height);
			Assert.NotEqual(20, window.Width);
		}
	}
}
