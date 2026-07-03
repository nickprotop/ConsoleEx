// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests;

public class ChatRoleStyleTests
{
	[Fact]
	public void ChatMessageId_EqualityByValue()
	{
		var a = new ChatMessageId(1);
		var b = new ChatMessageId(1);
		var c = new ChatMessageId(2);
		Assert.Equal(a, b);
		Assert.NotEqual(a, c);
	}

	[Fact]
	public void ChatRoleStyle_Defaults_AreSensible()
	{
		var s = new ChatRoleStyle();
		Assert.Equal(ColorRole.Default, s.ColorRole);
		Assert.True(s.Markdown);
		Assert.False(s.Collapsible);
		Assert.True(s.ShowHeader);
		Assert.Null(s.HeaderGradient);
	}

	[Fact]
	public void ChatRoleStyle_InitProps_Settable()
	{
		var s = new ChatRoleStyle { Collapsible = true, StartCollapsed = true, ColorRole = ColorRole.Info };
		Assert.True(s.Collapsible);
		Assert.True(s.StartCollapsed);
		Assert.Equal(ColorRole.Info, s.ColorRole);
	}
}
