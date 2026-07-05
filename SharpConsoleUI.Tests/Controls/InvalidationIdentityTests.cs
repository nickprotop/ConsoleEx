// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class InvalidationIdentityTests
	{
		[Fact]
		public void SetProperty_ForwardsControlIdentityToContainer()
		{
			var rc = new RecordingContainer();
			var button = new ButtonControl { Container = rc };

			button.Text = "changed"; // a SetProperty-backed property (ButtonControl.cs:128)

			Assert.Same(button, rc.LastCaller);
		}
	}
}
