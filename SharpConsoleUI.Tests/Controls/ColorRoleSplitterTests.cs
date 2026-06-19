using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleSplitterTests
{
	// The splitter's role accent colours the resting-state line via ForegroundColor.

	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		var s = new SplitterControl();
		Assert.Equal(Color.White, s.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var s = new SplitterControl { ColorRole = ColorRole.Danger };
		var plain = new SplitterControl();
		Assert.NotEqual(plain.ForegroundColor, s.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var s = new SplitterControl { ColorRole = ColorRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, s.ForegroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var s = new SplitterControlBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, s.ColorRole);
		Assert.True(s.Outline);
	}
}
