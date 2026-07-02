// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests;

public class RadioGroupTests
{
	private enum Size { Small, Medium, Large }

	private static (RadioGroup<Size> g, RadioControl<Size> a, RadioControl<Size> b, RadioControl<Size> c) Build()
	{
		var g = new RadioGroup<Size>();
		var a = new RadioControl<Size>(g, Size.Small, "Small");
		var b = new RadioControl<Size>(g, Size.Medium, "Medium");
		var c = new RadioControl<Size>(g, Size.Large, "Large");
		return (g, a, b, c);
	}

	[Fact]
	public void EmptyByDefault()
	{
		var (g, a, b, c) = Build();
		Assert.False(g.HasSelection);
		Assert.False(a.Checked); Assert.False(b.Checked); Assert.False(c.Checked);
	}

	[Fact]
	public void SelectingOne_UnchecksPeers_SingleSelectionInvariant()
	{
		var (g, a, b, c) = Build();
		a.Select();
		Assert.True(a.Checked); Assert.False(b.Checked);
		b.Select();
		Assert.False(a.Checked); Assert.True(b.Checked);
		Assert.Equal(Size.Medium, g.SelectedValue);
	}

	[Fact]
	public void SelectionChanged_FiresOncePerChange()
	{
		var (g, a, b, _) = Build();
		int count = 0; Size? last = null;
		g.SelectionChanged += (_, v) => { count++; last = v; };
		a.Select();
		Assert.Equal(1, count); Assert.Equal(Size.Small, last);
		a.Select(); // already selected, no change
		Assert.Equal(1, count);
	}

	[Fact]
	public void AllowDeselectFalse_ClickSelected_IsNoOp()
	{
		var (g, a, _, _) = Build();
		a.Select();
		a.Select(); // classic radio: stays selected
		Assert.True(a.Checked); Assert.True(g.HasSelection);
	}

	[Fact]
	public void AllowDeselectTrue_ClickSelected_Clears()
	{
		var (g, a, _, _) = Build();
		g.AllowDeselect = true;
		a.Select();
		a.Select();
		Assert.False(a.Checked); Assert.False(g.HasSelection);
	}

	[Fact]
	public void RequiredWins_OverAllowDeselect_AndBlocksClear()
	{
		var (g, a, _, _) = Build();
		g.AllowDeselect = true; g.Required = true;
		a.Select();
		a.Select();                 // Required blocks deselect
		Assert.True(a.Checked);
		g.Clear();                  // Required blocks clear while HasSelection
		Assert.True(a.Checked);
	}

	[Fact]
	public void RequiredTrue_StillStartsEmpty()
	{
		var g = new RadioGroup<Size> { Required = true };
		_ = new RadioControl<Size>(g, Size.Small, "S");
		Assert.False(g.HasSelection);
	}

	[Fact]
	public void EqualityComparer_WorksForString()
	{
		var g = new RadioGroup<string>();
		var x = new RadioControl<string>(g, "light", "Light");
		var y = new RadioControl<string>(g, "dark", "Dark");
		x.Select();
		Assert.True(x.Checked); Assert.False(y.Checked);
		Assert.Equal("light", g.SelectedValue);
	}
}
