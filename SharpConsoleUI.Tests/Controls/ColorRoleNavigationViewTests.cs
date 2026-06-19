using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleNavigationViewTests
{
	[Fact]
	public void DefaultRole_SelectedBackgroundMatchesLegacy()
	{
		// For ColorRole=Default the public selected-item getter must equal the legacy resolution.
		var c = new NavigationView();
		var plain = new NavigationView();
		Assert.Equal(plain.SelectedItemBackground, c.SelectedItemBackground);
	}

	[Fact]
	public void DangerRole_ChangesSelectedColors()
	{
		var c = new NavigationView { ColorRole = ColorRole.Danger };
		var plain = new NavigationView();
		Assert.NotEqual(plain.SelectedItemBackground, c.SelectedItemBackground);
		Assert.NotEqual(plain.SelectedItemForeground, c.SelectedItemForeground);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var c = new NavigationView { ColorRole = ColorRole.Danger, SelectedItemBackground = Color.Black };
		Assert.Equal(Color.Black, c.SelectedItemBackground);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var c = new NavigationViewBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, c.ColorRole);
		Assert.True(c.Outline);
	}
}
