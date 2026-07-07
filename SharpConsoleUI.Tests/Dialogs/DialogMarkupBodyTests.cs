// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Dialogs;

/// <summary>
/// Verifies the approved behavior change: dialog MESSAGE BODIES render markup by default,
/// with an opt-out <c>literal</c> that escapes brackets so the tag text is shown verbatim.
/// </summary>
public class DialogMarkupBodyTests
{
	// The body a primitive step builds is a ScrollablePanel whose first child is the message MarkupControl.
	private static MarkupControl MessageMarkup(IWindowControl body)
	{
		var spc = Assert.IsType<ScrollablePanelControl>(body);
		return spc.Children.OfType<MarkupControl>().First();
	}

	[Fact]
	public void ConfirmContent_Body_RendersMarkupByDefault()
	{
		var content = new ConfirmContent("[green]hi[/]", "OK", "Cancel");
		var markup = MessageMarkup(content.BuildContent(new FlowChrome("Title")));

		// Markup is consumed: the [green] tag is applied, not shown literally.
		var cells = MarkupParser.Parse(markup.Text, Color.White, Color.Black);
		Assert.DoesNotContain(cells, c => c.Character.ToString() == "[");
		Assert.Contains(cells, c => c.Character.ToString() == "h" && c.Foreground == Color.Green);
	}

	[Fact]
	public void ConfirmContent_Body_Literal_EscapesMarkup()
	{
		var content = new ConfirmContent("[green]hi[/]", "OK", "Cancel", literal: true);
		var markup = MessageMarkup(content.BuildContent(new FlowChrome("Title")));

		// literal: true → the tag is escaped, so the literal '[green]' text is rendered.
		var cells = MarkupParser.Parse(markup.Text, Color.White, Color.Black);
		var text = string.Concat(cells.Select(c => c.Character.ToString()));
		Assert.Contains("[green]", text);
	}

	[Fact]
	public void PromptContent_Body_RendersMarkupByDefault()
	{
		var content = new PromptContent("[green]hi[/]");
		var markup = MessageMarkup(content.BuildContent(new FlowChrome("Title")));

		var cells = MarkupParser.Parse(markup.Text, Color.White, Color.Black);
		Assert.DoesNotContain(cells, c => c.Character.ToString() == "[");
		Assert.Contains(cells, c => c.Character.ToString() == "h" && c.Foreground == Color.Green);
	}

	[Fact]
	public void PromptContent_Body_Literal_EscapesMarkup()
	{
		var content = new PromptContent("[green]hi[/]", literal: true);
		var markup = MessageMarkup(content.BuildContent(new FlowChrome("Title")));

		var cells = MarkupParser.Parse(markup.Text, Color.White, Color.Black);
		var text = string.Concat(cells.Select(c => c.Character.ToString()));
		Assert.Contains("[green]", text);
	}
}
