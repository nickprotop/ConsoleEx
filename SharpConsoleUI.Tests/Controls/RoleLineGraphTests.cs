using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleLineGraphTests
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
		var g = new LineGraphControl { Role = ControlRole.Danger };
		var plain = new LineGraphControl();
		Assert.NotEqual(plain.LineColor, g.LineColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var g = new LineGraphControl { Role = ControlRole.Danger, LineColor = Color.Black };
		Assert.Equal(Color.Black, g.LineColor);
	}

	[Fact]
	public void RoleDrivesDefaultSeriesColor()
	{
		var g = new LineGraphControl { Role = ControlRole.Danger };
		g.AddDataPoint(5);
		var defaultSeries = g.Series.First();
		Assert.Equal(g.LineColor, defaultSeries.LineColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var g = new LineGraphBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, g.Role);
		Assert.True(g.Outline);
	}
}
