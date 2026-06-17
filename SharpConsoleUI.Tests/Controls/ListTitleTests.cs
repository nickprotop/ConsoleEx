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
/// A list with no title must not show a title row. Regression: the builder used to default the
/// title to the literal "List", so <c>Controls.List()</c> showed an unwanted "List" header.
/// </summary>
public class ListTitleTests
{
	[Fact]
	public void List_NoTitleSpecified_HasEmptyTitle()
	{
		var list = SharpConsoleUI.Builders.Controls.List().Build();
		Assert.True(string.IsNullOrEmpty(list.Title),
			$"Unspecified list title should be empty, was '{list.Title}'.");
	}

	[Fact]
	public void List_EmptyTitle_HasEmptyTitle()
	{
		var list = SharpConsoleUI.Builders.Controls.List("").Build();
		Assert.True(string.IsNullOrEmpty(list.Title));
	}

	[Fact]
	public void List_ExplicitTitle_IsKept()
	{
		var list = SharpConsoleUI.Builders.Controls.List("Packages").Build();
		Assert.Equal("Packages", list.Title);
	}

	[Fact]
	public void List_NoTitle_DoesNotReserveTitleRowHeight()
	{
		var titled = SharpConsoleUI.Builders.Controls.List("T").AddItem("a").AddItem("b").Build();
		var untitled = SharpConsoleUI.Builders.Controls.List().AddItem("a").AddItem("b").Build();
		// The untitled list is exactly one row shorter (no title row) for the same items.
		Assert.Equal(titled.GetLogicalContentSize().Height - 1, untitled.GetLogicalContentSize().Height);
	}
}
