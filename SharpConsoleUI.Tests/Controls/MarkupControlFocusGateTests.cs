using System.Collections.Generic;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class MarkupControlFocusGateTests
	{
		private static MarkupControl Make(string line) => new MarkupControl(new List<string> { line });

		[Fact]
		public void NoLinks_NotFocusable()
		{
			var c = Make("just plain text, no links");
			Assert.False(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void HasLink_Focusable()
		{
			var c = Make("[link=https://x]click[/]");
			Assert.True(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void Disabled_WithLinks_NotFocusable()
		{
			var c = Make("[link=https://x]click[/]");
			((IInteractiveControl)c).IsEnabled = false;
			Assert.False(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void CanReceiveFocus_CorrectBeforeAnyPaint()
		{
			// No PaintDOM called — must still know it has links (content-derived, not paint-derived).
			var c = Make("[link=u]x[/]");
			Assert.True(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void ContentChange_NoLinkToLink_FlipsFocusable()
		{
			var c = Make("plain");
			Assert.False(((IFocusableControl)c).CanReceiveFocus);
			c.Text = "[link=u]now a link[/]";
			Assert.True(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void ContentChange_LinkToNoLink_FlipsNonFocusable()
		{
			var c = Make("[link=u]x[/]");
			Assert.True(((IFocusableControl)c).CanReceiveFocus);
			c.SetContent(new List<string> { "plain now" });
			Assert.False(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void Markdown_WithLink_Focusable()
		{
			var c = Make("[markdown][text](https://x)[/]");
			Assert.True(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void DefaultIsEnabled_True()
		{
			Assert.True(((IInteractiveControl)Make("x")).IsEnabled);
		}

		[Fact]
		public void AppendLine_WithLink_FlipsFocusable()
		{
			var c = Make("plain");
			Assert.False(((IFocusableControl)c).CanReceiveFocus);
			c.AppendLine("[link=u]added link[/]");
			Assert.True(((IFocusableControl)c).CanReceiveFocus);
		}

		[Fact]
		public void NotVisible_WithLinks_NotFocusable()
		{
			var c = Make("[link=u]x[/]");
			c.Visible = false;
			Assert.False(((IFocusableControl)c).CanReceiveFocus);
		}
	}
}
