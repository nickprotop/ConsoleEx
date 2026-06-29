// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls
{
	public class ScrollablePanelMeasureTests
	{
		[Fact]
		public void MeasureContentHeight_CountsEachChildRow()
		{
			// Five single-line markup children at a comfortable width → at least 5 rows.
			var panel = Ctl.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill).Build();
			for (int i = 0; i < 5; i++)
				panel.AddControl(Ctl.Markup().AddLine($"line {i}").Build());

			int h = panel.MeasureContentHeight(40);

			Assert.True(h >= 5, $"expected >= 5 content rows, got {h}");
		}

		[Fact]
		public void MeasureContentHeight_NarrowWidth_WrapsToMoreRows()
		{
			// One long line: wide enough fits in 1 row; very narrow forces wrapping to more rows.
			var panel = Ctl.ScrollablePanel().WithVerticalAlignment(VerticalAlignment.Fill).Build();
			panel.AddControl(Ctl.Markup()
				.AddLine("the quick brown fox jumps over the lazy dog several times over and over")
				.Build());

			int wide = panel.MeasureContentHeight(120);
			int narrow = panel.MeasureContentHeight(12);

			Assert.True(narrow > wide, $"narrow ({narrow}) should wrap to more rows than wide ({wide})");
		}
	}
}
