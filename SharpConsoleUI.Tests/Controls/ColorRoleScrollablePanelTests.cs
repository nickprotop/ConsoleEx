using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// ScrollablePanelControl honours <see cref="ColorRole"/>: the role tints the scrollbar thumb (the
/// panel's defining, usually-visible chrome) and the border when drawn. ColorRole.Default is unchanged.
/// </summary>
public class ColorRoleScrollablePanelTests
{
	[Fact]
	public void DefaultRole_LeavesScrollbarRoleColorUnset()
	{
		// With no role, the role helper contributes nothing — the panel resolves scrollbar/border
		// colors exactly as before (explicit override or theme).
		var panel = new ScrollablePanelControl();
		Assert.Equal(ColorRole.Default, panel.ColorRole);
		Assert.Null(ColorResolver.ColorRoleBackground(panel.ColorRole, panel.Container, panel.Outline));
		Assert.Null(ColorResolver.ColorRoleBorder(panel.ColorRole, panel.Container, panel.Outline));
	}

	[Fact]
	public void DangerRole_ResolvesConcreteThumbAndBorder()
	{
		var panel = new ScrollablePanelControl { ColorRole = ColorRole.Danger };
		var thumb = ColorResolver.ColorRoleBackground(panel.ColorRole, panel.Container, panel.Outline);
		var border = ColorResolver.ColorRoleBorder(panel.ColorRole, panel.Container, panel.Outline);
		Assert.NotNull(thumb);
		Assert.NotNull(border);
		Assert.Equal(255, thumb!.Value.A);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var panel = new ScrollablePanelBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, panel.ColorRole);
		Assert.True(panel.Outline);
	}
}
