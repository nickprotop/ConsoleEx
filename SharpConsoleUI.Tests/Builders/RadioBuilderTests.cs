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

public class RadioBuilderTests
{
	private enum Size { Small, Large }

	[Fact]
	public void GroupBuilder_SetsFlags_AndSelectionCallback()
	{
		Size? got = null;
		var g = Builders.Controls.RadioGroup<Size>().Required().OnSelectionChanged(v => got = v).Build();
		Assert.True(g.Required);
		var r = Builders.Controls.Radio(g, Size.Large, "Large").Build();
		r.Select();
		Assert.Equal(Size.Large, got);
	}

	[Fact]
	public void RadioBuilder_Selected_SetsGroupValue()
	{
		var g = Builders.Controls.RadioGroup<Size>().Build();
		var r = Builders.Controls.Radio(g, Size.Small, "Small").Selected().Build();
		Assert.True(r.Checked);
		Assert.Equal(Size.Small, g.SelectedValue);
	}

	[Fact]
	public void StringOverload_UsesLabelAsValue()
	{
		var g = Builders.Controls.RadioGroup<string>().Build();
		var r = Builders.Controls.Radio(g, "Dark").Build();  // label IS value
		r.Select();
		Assert.Equal("Dark", g.SelectedValue);
		Assert.Equal("Dark", r.Value);
	}
}
