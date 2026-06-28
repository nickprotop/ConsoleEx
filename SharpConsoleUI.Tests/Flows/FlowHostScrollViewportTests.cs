// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Layout;
using Xunit;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Flows
{
	public class FlowHostScrollViewportTests
	{
		[Fact]
		public void WrapBody_PlainControl_WrapsInFillScrollablePanel()
		{
			var inner = Ctl.Markup().AddLine("hello").Build();

			var wrapped = FlowContentHelpers.WrapBody(inner);

			var spc = Assert.IsType<ScrollablePanelControl>(wrapped);
			Assert.NotSame(inner, wrapped);
			Assert.Equal(VerticalAlignment.Fill, spc.VerticalAlignment);
		}

		[Fact]
		public void WrapBody_AlreadyScrollablePanel_ReturnedUnchanged()
		{
			var alreadySpc = Ctl.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(Ctl.Markup().AddLine("hi").Build())
				.Build();

			var wrapped = FlowContentHelpers.WrapBody(alreadySpc);

			Assert.Same(alreadySpc, wrapped);
		}
	}
}
