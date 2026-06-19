using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// ScrollablePanelControl honours <see cref="ControlRole"/>: the role tints the scrollbar thumb (the
/// panel's defining, usually-visible chrome) and the border when drawn. Role.Default is unchanged.
/// </summary>
public class RoleScrollablePanelTests
{
	[Fact]
	public void DefaultRole_LeavesScrollbarRoleColorUnset()
	{
		// With no role, the role helper contributes nothing — the panel resolves scrollbar/border
		// colors exactly as before (explicit override or theme).
		var panel = new ScrollablePanelControl();
		Assert.Equal(ControlRole.Default, panel.Role);
		Assert.Null(ColorResolver.RoleBackground(panel.Role, panel.Container, panel.Outline));
		Assert.Null(ColorResolver.RoleBorder(panel.Role, panel.Container, panel.Outline));
	}

	[Fact]
	public void DangerRole_ResolvesConcreteThumbAndBorder()
	{
		var panel = new ScrollablePanelControl { Role = ControlRole.Danger };
		var thumb = ColorResolver.RoleBackground(panel.Role, panel.Container, panel.Outline);
		var border = ColorResolver.RoleBorder(panel.Role, panel.Container, panel.Outline);
		Assert.NotNull(thumb);
		Assert.NotNull(border);
		Assert.Equal(255, thumb!.Value.A);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var panel = new ScrollablePanelBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, panel.Role);
		Assert.True(panel.Outline);
	}
}
