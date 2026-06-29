// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

public class MultilineEditBuilderTests
{
	[Fact]
	public void Builder_SetsContent_ReadOnly_AndLayoutHeight()
	{
		MultilineEditControl editor = Ctl.MultilineEdit()
			.WithContent("line 1\nline 2")
			.AsReadOnly()
			.WithHeight(4)
			.Build();

		Assert.Equal("line 1\nline 2", editor.Content);
		Assert.True(editor.ReadOnly);
		Assert.Equal(4, editor.Height);            // WithHeight sets the layout height
	}

	[Fact]
	public void WithHeight_IsDistinctFromViewportHeight()
	{
		// WithHeight sets the control's layout Height; WithViewportHeight sets the editor viewport.
		MultilineEditControl editor = Ctl.MultilineEdit()
			.WithHeight(7)
			.Build();

		Assert.Equal(7, editor.Height);
	}
}
