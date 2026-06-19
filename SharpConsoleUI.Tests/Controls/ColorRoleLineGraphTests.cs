using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleLineGraphTests
{
	// The line graph's role accent surfaces through LineColor, the colour applied to the implicit
	// default series. Explicitly-added series keep their own colours.

	[Fact]
	public void DefaultRole_LineColorMatchesLegacy()
	{
		var g = new LineGraphControl();
		Assert.Equal(Color.Cyan1, g.LineColor);
	}

	[Fact]
	public void DangerRole_ChangesLineColor()
	{
		var g = new LineGraphControl { ColorRole = ColorRole.Danger };
		var plain = new LineGraphControl();
		Assert.NotEqual(plain.LineColor, g.LineColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var g = new LineGraphControl { ColorRole = ColorRole.Danger, LineColor = Color.Black };
		Assert.Equal(Color.Black, g.LineColor);
	}

	[Fact]
	public void RoleDrivesDefaultSeriesColor()
	{
		var g = new LineGraphControl { ColorRole = ColorRole.Danger };
		g.AddDataPoint(5);
		var defaultSeries = g.Series.First();
		Assert.Equal(g.LineColor, defaultSeries.LineColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var g = new LineGraphBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, g.ColorRole);
		Assert.True(g.Outline);
	}
}
