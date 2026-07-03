// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests;

public class FormBuilderTests
{
	[Fact]
	public void Builder_BuildsForm_WithFields_AndOnSubmit()
	{
		IReadOnlyDictionary<string, string?>? got = null;
		var form = Builders.Controls.Form()
			.AddText("name", "Name:", initial: "Bob")
			.AddCheckbox("ssl", "SSL", initial: true)
			.OnSubmit(v => got = v)
			.Build();
		form.Submit();
		Assert.NotNull(got);
		Assert.Equal("Bob", got!["name"]);
		Assert.Equal("true", got!["ssl"]);
	}
}
