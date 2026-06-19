using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleBarGraphTests
{
	// The bar graph's role accent surfaces through the default FilledColor (used when no
	// per-threshold or smooth-gradient colours are set). Threshold/gradient colours still win.

	[Fact]
	public void DefaultRole_FilledColorMatchesLegacy()
	{
		var b = new BarGraphControl();
		Assert.Equal(Color.Cyan1, b.FilledColor);
	}

	[Fact]
	public void DangerRole_ChangesFilledColor()
	{
		var b = new BarGraphControl { ColorRole = ColorRole.Danger };
		var plain = new BarGraphControl();
		Assert.NotEqual(plain.FilledColor, b.FilledColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var b = new BarGraphControl { ColorRole = ColorRole.Danger, FilledColor = Color.Black };
		Assert.Equal(Color.Black, b.FilledColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var b = new BarGraphBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, b.ColorRole);
		Assert.True(b.Outline);
	}
}
