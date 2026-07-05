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

namespace SharpConsoleUI.Tests.Infrastructure
{
	public class RecordingContainerTests
	{
		[Fact]
		public void RecordsWorkAndCaller()
		{
			var rc = new RecordingContainer();
			rc.Invalidate(Invalidation.Repaint, null);
			Assert.Equal(1, rc.InvalidateCount);
			Assert.Equal(Invalidation.Repaint, rc.LastWork);
			Assert.Null(rc.LastCaller);

			var control = new ButtonControl();
			rc.Invalidate(Invalidation.Relayout, control);
			Assert.Equal(2, rc.InvalidateCount);
			Assert.Same(control, rc.LastCaller);
			Assert.Equal(Invalidation.Relayout, rc.LastWork);
		}
	}
}
