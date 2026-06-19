using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleHorizontalSplitterTests
{
	// The horizontal splitter's role accent colours the resting-state line via ForegroundColor.

	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		var s = new HorizontalSplitterControl();
		Assert.Equal(Color.White, s.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var s = new HorizontalSplitterControl { ColorRole = ColorRole.Danger };
		var plain = new HorizontalSplitterControl();
		Assert.NotEqual(plain.ForegroundColor, s.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var s = new HorizontalSplitterControl { ColorRole = ColorRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, s.ForegroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var s = new HorizontalSplitterBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, s.ColorRole);
		Assert.True(s.Outline);
	}
}
