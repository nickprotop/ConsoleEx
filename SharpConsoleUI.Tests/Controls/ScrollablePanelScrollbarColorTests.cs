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
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Covers the ScrollablePanelControl scrollbar-color API added for discussion #44
/// (parity with MultilineEditControl). Track/thumb colors are overridable; null = focus-aware default.
/// </summary>
public class ScrollablePanelScrollbarColorTests
{
	[Fact]
	public void Defaults_AreNull_MeaningFocusAwareBuiltIn()
	{
		var panel = new ScrollablePanelControl();
		Assert.Null(panel.ScrollbarColor);
		Assert.Null(panel.ScrollbarThumbColor);
	}

	[Fact]
	public void Properties_RoundTrip()
	{
		var panel = new ScrollablePanelControl
		{
			ScrollbarColor = Color.Red,
			ScrollbarThumbColor = Color.Green
		};
		Assert.Equal(Color.Red, panel.ScrollbarColor);
		Assert.Equal(Color.Green, panel.ScrollbarThumbColor);
	}

	[Fact]
	public void Properties_CanBeResetToNull()
	{
		var panel = new ScrollablePanelControl { ScrollbarColor = Color.Red };
		panel.ScrollbarColor = null;
		Assert.Null(panel.ScrollbarColor);
	}

	[Fact]
	public void Builder_WithScrollbarColors_SetsBoth()
	{
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithScrollbarColors(trackColor: Color.Blue, thumbColor: Color.Yellow)
			.Build();

		Assert.Equal(Color.Blue, panel.ScrollbarColor);
		Assert.Equal(Color.Yellow, panel.ScrollbarThumbColor);
	}

	[Fact]
	public void Builder_WithoutScrollbarColors_LeavesDefaultsNull()
	{
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel().Build();
		Assert.Null(panel.ScrollbarColor);
		Assert.Null(panel.ScrollbarThumbColor);
	}
}
