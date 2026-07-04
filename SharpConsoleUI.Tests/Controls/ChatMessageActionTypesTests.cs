// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests;

public class ChatMessageActionTypesTests
{
	[Fact]
	public void ChatMessageAction_Defaults_AreSaneAndAdditive()
	{
		var a = new ChatMessageAction { Id = "copy", Label = "Copy" };
		Assert.Equal("copy", a.Id);
		Assert.Equal("Copy", a.Label);
		Assert.Equal(ChatActionVariant.Default, a.Variant);
		Assert.Equal(ChatActionAfterPress.None, a.AfterPress);
		Assert.True(a.Enabled);
		Assert.False(a.IsPressed);
		Assert.Null(a.Icon);
		Assert.Null(a.Group);
	}

	[Fact]
	public void ChatMessageStatus_HoldsTextAndSeverity()
	{
		var s = new ChatMessageStatus("Copied", NotificationSeverity.Success);
		Assert.Equal("Copied", s.Text);
		Assert.Equal(NotificationSeverity.Success, s.Severity);
	}

	[Fact]
	public void ChatActionToggledEventArgs_CarriesState()
	{
		var args = new ChatActionToggledEventArgs(default, new ChatMessageAction { Id = "like", Label = "Like" }, true);
		Assert.True(args.IsPressed);
		Assert.Equal("like", args.Action.Id);
	}
}
