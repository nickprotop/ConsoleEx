using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleSparklineTests
{
	// The sparkline's role accent surfaces through the primary BarColor (used when no gradient is
	// set). The secondary series keeps its own colour.

	[Fact]
	public void DefaultRole_BarColorMatchesLegacy()
	{
		var s = new SparklineControl();
		Assert.Equal(Color.Cyan1, s.BarColor);
	}

	[Fact]
	public void DangerRole_ChangesBarColor()
	{
		var s = new SparklineControl { ColorRole = ColorRole.Danger };
		var plain = new SparklineControl();
		Assert.NotEqual(plain.BarColor, s.BarColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var s = new SparklineControl { ColorRole = ColorRole.Danger, BarColor = Color.Black };
		Assert.Equal(Color.Black, s.BarColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var s = new SparklineBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, s.ColorRole);
		Assert.True(s.Outline);
	}
}
