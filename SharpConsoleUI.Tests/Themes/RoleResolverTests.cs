using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

public class RoleResolverTests
{
	private static ITheme DarkTheme() => new ModernGrayTheme();

	[Fact]
	public void Danger_TextAndBorder_AreReadableOnSurface()
	{
		var rc = RoleResolver.Resolve(ControlRole.Danger, DarkTheme());
		Assert.NotEqual(rc.Background, rc.TextOnBackground);
		var bg = DarkTheme().WindowBackgroundColor;
		Assert.True(System.Math.Abs(rc.Border.Luminance() - bg.Luminance()) >= 60);
		Assert.True(System.Math.Abs(rc.Text.Luminance() - bg.Luminance()) >= 60);
	}

	[Fact]
	public void Default_Role_ReturnsSurfaceColorsAsInertNoOp()
	{
		var t = DarkTheme();
		var rc = RoleResolver.Resolve(ControlRole.Default, t);
		Assert.Equal(t.WindowBackgroundColor, rc.Background);
		Assert.Equal(t.WindowForegroundColor, rc.Text);
		Assert.Equal(t.WindowForegroundColor, rc.TextOnBackground);
		Assert.Equal(t.WindowBackgroundColor, rc.Border);
	}

	[Fact]
	public void Outline_SwapsFillToSurface_AndMovesRoleColorToText()
	{
		var t = DarkTheme();
		var solid = RoleResolver.Resolve(ControlRole.Success, t, outline: false);
		var outline = RoleResolver.Resolve(ControlRole.Success, t, outline: true);
		// Outline fills with the surface and paints the role colour as text/border. That role colour
		// is the same contrast-ensured-on-surface colour the solid variant uses for its text/border,
		// so it stays readable even when the seed sits close to the window background.
		Assert.Equal(t.WindowBackgroundColor, outline.Background);
		Assert.Equal(solid.Text, outline.Text);
		Assert.Equal(solid.Border, outline.Border);
	}

	[Fact]
	public void Disabled_AlphaDampsAllOutputs()
	{
		var normal = RoleResolver.Resolve(ControlRole.Primary, DarkTheme(), state: RoleState.Normal);
		var disabled = RoleResolver.Resolve(ControlRole.Primary, DarkTheme(), state: RoleState.Disabled);
		Assert.True(disabled.Background.A < 255);
		Assert.True(disabled.Text.A < 255);
		Assert.Equal(255, normal.Background.A);
	}

	[Fact]
	public void Focused_BrightensRelativeToNormal()
	{
		// Danger is a mid-luminance red that reliably brightens under Tint.
		var normal = RoleResolver.Resolve(ControlRole.Danger, DarkTheme(), state: RoleState.Normal);
		var focused = RoleResolver.Resolve(ControlRole.Danger, DarkTheme(), state: RoleState.Focused);
		Assert.True(focused.Background.Luminance() > normal.Background.Luminance());
	}

	[Fact]
	public void NullContainer_DoesNotThrow_UsesBuiltInDefaults()
	{
		var rc = RoleResolver.Resolve(ControlRole.Warning, (SharpConsoleUI.Controls.IContainer?)null);
		Assert.Equal(255, rc.Background.A);
	}
}
