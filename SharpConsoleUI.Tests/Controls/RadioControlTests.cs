// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests;

public class RadioControlTests
{
	private enum Size { Small, Large }

	[Fact]
	public void Checked_IsComputedFromGroup()
	{
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Small, "Small");
		Assert.False(r.Checked);
		g.SelectedValue = Size.Small;
		Assert.True(r.Checked);          // computed, no stored state set on r
		g.SelectedValue = Size.Large;
		Assert.False(r.Checked);
	}

	[Fact]
	public void Select_SetsGroupValue()
	{
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Large, "Large");
		r.Select();
		Assert.Equal(Size.Large, g.SelectedValue);
		Assert.True(r.Checked);
	}

	[Fact]
	public void CheckedSurvivesReRender()
	{
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Small, "Small");
		r.Select();
		var buffer = new CharacterBuffer(20, 3);
		var bounds = new LayoutRect(0, 0, 20, 1);
		r.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);   // render once
		r.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);   // re-render
		Assert.True(r.Checked);          // computed value intact after repaint
	}

	[Fact]
	public void SingleLine_MeasuresHeightOne_WhenWrapFalse()
	{
		var g = new RadioGroup<Size>();
		var r = new RadioControl<Size>(g, Size.Small, "Small") { Wrap = false };
		var size = r.MeasureDOM(new LayoutConstraints(0, 40, 0, 10));
		Assert.Equal(1, size.Height);
	}
}
