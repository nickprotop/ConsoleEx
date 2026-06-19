using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleBarGraphTests
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
		var b = new BarGraphControl { Role = ControlRole.Danger };
		var plain = new BarGraphControl();
		Assert.NotEqual(plain.FilledColor, b.FilledColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var b = new BarGraphControl { Role = ControlRole.Danger, FilledColor = Color.Black };
		Assert.Equal(Color.Black, b.FilledColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var b = new BarGraphBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, b.Role);
		Assert.True(b.Outline);
	}
}
