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
	public class InvalidationForwardingTests
	{
		[Fact]
		public void ScrollablePanel_ForwardsOriginator_NotSelf()
		{
			var root = new RecordingContainer();
			var spc = new ScrollablePanelControl { Container = root };
			var origin = new ButtonControl();

			((IContainer)spc).Invalidate(Invalidation.Relayout, origin);

			Assert.Same(origin, root.LastCaller); // the originator, not the SPC
		}

		[Fact]
		public void ScrollablePanel_NoOriginator_NamesSelf()
		{
			var root = new RecordingContainer();
			var spc = new ScrollablePanelControl { Container = root };

			((IContainer)spc).Invalidate(Invalidation.Relayout, null);

			Assert.Same(spc, root.LastCaller); // ?? this
		}

		[Fact]
		public void PortalContentContainer_ForwardsOriginator_NotDropped()
		{
			var root = new RecordingContainer();
			var pcc = new PortalContentContainer { Container = root };
			var origin = new ButtonControl();

			((IContainer)pcc).Invalidate(Invalidation.Relayout, origin);

			Assert.Same(origin, root.LastCaller); // was dropped before the fix
		}
	}
}
