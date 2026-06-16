// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for <see cref="Builders.MarkupBuilder.AddText"/>, which appends inline (Console.Write-style)
/// — the first segment joins the current last line, and a new line begins only at each embedded \n.
/// </summary>
public class MarkupBuilderAddTextTests
{
	[Fact]
	public void Append_JoinsOntoPrevious_ConsoleWriteStyle()
	{
		// .NET-convention name on the builder: Append (StringBuilder.Append / Console.Write).
		var control = MarkupControl.Create()
			.Append("Hello, ")
			.Append("world")
			.Build();

		Assert.Equal("Hello, world", control.Text);
	}

	[Fact]
	public void Append_NewlineStartsNewLine()
	{
		var control = MarkupControl.Create()
			.Append("a\nb")
			.Build();

		Assert.Equal("a\nb", control.Text);
	}

	[Fact]
	public void AddText_IsAliasOfAppend()
	{
		var viaAppend = MarkupControl.Create().AddLine("x").Append(" tail").Build();
		var viaAddText = MarkupControl.Create().AddLine("x").AddText(" tail").Build();
		Assert.Equal(viaAppend.Text, viaAddText.Text);
		Assert.Equal("x tail", viaAppend.Text);
	}

	[Fact]
	public void AddText_JoinsOntoPreviousAddText()
	{
		var control = MarkupControl.Create()
			.AddText("Hello, ")
			.AddText("world")
			.Build();

		Assert.Equal("Hello, world", control.Text);
	}

	[Fact]
	public void AddText_NewlineStartsNewLine()
	{
		var control = MarkupControl.Create()
			.AddText("a\nb")
			.Build();

		Assert.Equal("a\nb", control.Text);
	}

	[Fact]
	public void AddText_LeadingNewlines_StartNewLines()
	{
		// "\n\ntail" after "head": empty first segment joins "head" (no-op), then a blank line, then "tail".
		var control = MarkupControl.Create()
			.AddLine("head")
			.AddText("\n\ntail")
			.Build();

		Assert.Equal("head\n\ntail", control.Text);
	}

	[Fact]
	public void AddText_AfterAddLine_JoinsOntoThatLine()
	{
		var control = MarkupControl.Create()
			.AddLine("line")
			.AddText(" tail")
			.Build();

		Assert.Equal("line tail", control.Text);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void AddText_NullOrEmpty_IsNoOp(string? text)
	{
		var control = MarkupControl.Create()
			.AddLine("x")
			.AddText(text!)
			.Build();

		Assert.Equal("x", control.Text);
	}
}
