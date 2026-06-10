using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls
{
	public class MarkdownHostControlTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		// Central guarantee: any markup host that funnels through MarkupParser.Parse renders [markdown].
		[Fact]
		public void HostStringWithMarkdownTag_ResolvesThroughParser()
		{
			string hostLabel = "[markdown]**Done**[/]";
			var cells = MarkupParser.Parse(hostLabel, Fg, Bg);
			var text = string.Concat(cells.Select(c => c.Character.ToString()));
			Assert.Equal("Done", text.Trim());
			Assert.Contains(cells, c => (c.Decorations & TextDecoration.Bold) != 0);
		}
	}
}
