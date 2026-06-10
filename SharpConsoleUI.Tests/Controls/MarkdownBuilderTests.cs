using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Xunit;
using CB = SharpConsoleUI.Builders.Controls;
using Color = SharpConsoleUI.Color;
using MarkdownStyle = SharpConsoleUI.Configuration.MarkdownStyle;

namespace SharpConsoleUI.Tests.Controls
{
	public class MarkdownBuilderTests
	{
		[Fact]
		public void ControlsMarkdown_SeedsMarkdownLine()
		{
			MarkupControl c = CB.Markdown("# Hi").Build();
			Assert.Contains("[markdown]", c.Text);
			Assert.Contains("# Hi", c.Text);
		}

		[Fact]
		public void ControlsMarkdown_NullArg_NoLine()
		{
			MarkupControl c = CB.Markdown().Build();
			Assert.DoesNotContain("[markdown]", c.Text);
		}

		[Fact]
		public void Builder_AddMarkdown_AddsTaggedLine()
		{
			MarkupControl c = CB.Markup().AddMarkdown("**b**").Build();
			Assert.Contains("[markdown]**b**[/]", c.Text);
		}

		[Fact]
		public void Builder_WithMarkdown_AddsTaggedLine()
		{
			MarkupControl c = CB.Markup().WithMarkdown("*i*").Build();
			Assert.Contains("[markdown]*i*[/]", c.Text);
		}

		[Fact]
		public void Builder_WithMarkdownStyle_SetsControlStyle()
		{
			MarkupControl c = CB.Markdown("x")
				.WithMarkdownStyle(s => s with { LinkColor = new Color(9, 9, 9) })
				.Build();
			Assert.NotNull(c.MarkdownStyle);
			Assert.Equal(new Color(9, 9, 9), c.MarkdownStyle!.LinkColor);
		}
	}
}
