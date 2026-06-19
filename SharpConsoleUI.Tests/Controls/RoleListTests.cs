using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleListTests
{
	// The list's role accent surfaces through the selected-item colours
	// (HighlightBackgroundColor / HighlightForegroundColor).

	[Fact]
	public void DefaultRole_HighlightColorsMatchLegacy()
	{
		var list = new ListControl();
		Assert.Equal(Color.DarkBlue, list.HighlightBackgroundColor);
		Assert.Equal(Color.White, list.HighlightForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesHighlightBackground()
	{
		var list = new ListControl { Role = ControlRole.Danger };
		var plain = new ListControl();
		Assert.NotEqual(plain.HighlightBackgroundColor, list.HighlightBackgroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var list = new ListControl
		{
			Role = ControlRole.Danger,
			HighlightBackgroundColor = Color.Black
		};
		Assert.Equal(Color.Black, list.HighlightBackgroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var list = new ListBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, list.Role);
		Assert.True(list.Outline);
	}
}
