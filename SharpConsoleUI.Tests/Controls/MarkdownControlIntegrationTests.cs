using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;
using MarkdownStyle = SharpConsoleUI.Configuration.MarkdownStyle;

namespace SharpConsoleUI.Tests.Controls
{
	public class MarkdownControlIntegrationTests
	{
		[Fact]
		public void SetMarkdown_WrapsContentInMarkdownTag()
		{
			var c = new MarkupControl(new List<string>());
			c.SetMarkdown("# Hi");
			Assert.Contains("[markdown]", c.Text);
			Assert.Contains("# Hi", c.Text);
		}

		[Fact]
		public void MarkdownStyle_PerControl_OverridesLinkColor_ViaConverter()
		{
			// Core guarantee: the converter honors a per-control style override.
			var custom = MarkdownStyle.Default with { LinkColor = new Color(10, 20, 30) };
			var native = MarkdownToMarkup.Convert("[x](http://y)", custom);
			var cells = MarkupParser.Parse(native, Color.White, Color.Black);
			Assert.Contains(cells, cc => cc.Character.ToString() == "x"
				&& cc.Foreground == new Color(10, 20, 30));
		}

		[Fact]
		public void MarkdownStyle_Property_DefaultsToNull()
		{
			var c = new MarkupControl(new List<string>());
			Assert.Null(c.MarkdownStyle);
		}

		[Fact]
		public void MarkdownStyle_Property_CanBeSet()
		{
			var c = new MarkupControl(new List<string>());
			var custom = MarkdownStyle.Default with { LinkColor = new Color(5, 6, 7) };
			c.MarkdownStyle = custom;
			Assert.NotNull(c.MarkdownStyle);
			Assert.Equal(new Color(5, 6, 7), c.MarkdownStyle!.LinkColor);
		}
	}
}
