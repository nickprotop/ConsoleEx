// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests;

public class ChatTranscriptBuilderTests
{
	[Fact]
	public void Builder_ConfiguresControl()
	{
		var chat = Builders.Controls.ChatTranscript()
			.AnimateMessages(false)
			.WithRoleStyle(ChatRole.Tool, new ChatRoleStyle { Collapsible = true })
			.Build();
		Assert.False(chat.AnimateMessages);
		Assert.True(chat.GetRoleStyle(ChatRole.Tool).Collapsible);
	}

	[Fact]
	public void Builder_WithAutoScroll_SetsAutoScroll()
	{
		var chat = Builders.Controls.ChatTranscript()
			.WithAutoScroll(false)
			.Build();
		Assert.False(chat.AutoScroll);
	}

	[Fact]
	public void Builder_WithName_SetsName()
	{
		var chat = Builders.Controls.ChatTranscript()
			.WithName("my-chat")
			.Build();
		Assert.Equal("my-chat", chat.Name);
	}

	[Fact]
	public void Builder_WithMarginUniform_SetsMargin()
	{
		var chat = Builders.Controls.ChatTranscript()
			.WithMargin(2)
			.Build();
		Assert.Equal(2, chat.Margin.Left);
		Assert.Equal(2, chat.Margin.Top);
		Assert.Equal(2, chat.Margin.Right);
		Assert.Equal(2, chat.Margin.Bottom);
	}

	[Fact]
	public void Builder_WithMarginSides_SetsMargin()
	{
		var chat = Builders.Controls.ChatTranscript()
			.WithMargin(1, 2, 3, 4)
			.Build();
		Assert.Equal(1, chat.Margin.Left);
		Assert.Equal(2, chat.Margin.Top);
		Assert.Equal(3, chat.Margin.Right);
		Assert.Equal(4, chat.Margin.Bottom);
	}

	[Fact]
	public void ImplicitOperator_ReturnsChatTranscriptControl()
	{
		ChatTranscriptControl chat = Builders.Controls.ChatTranscript()
			.AnimateMessages(true);
		Assert.NotNull(chat);
		Assert.True(chat.AnimateMessages);
	}
}
