using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

public class ColorRoleResolverTests
{
	private static ITheme DarkTheme() => new ModernGrayTheme();

	[Fact]
	public void Danger_TextAndBorder_AreReadableOnSurface()
	{
		var rc = ColorRoleResolver.Resolve(ColorRole.Danger, DarkTheme());
		Assert.NotEqual(rc.Background, rc.TextOnBackground);
		var bg = DarkTheme().WindowBackgroundColor;
		Assert.True(System.Math.Abs(rc.Border.Luminance() - bg.Luminance()) >= 60);
		Assert.True(System.Math.Abs(rc.Text.Luminance() - bg.Luminance()) >= 60);
	}

	[Fact]
	public void Default_Role_ReturnsSurfaceColorsAsInertNoOp()
	{
		var t = DarkTheme();
		var rc = ColorRoleResolver.Resolve(ColorRole.Default, t);
		Assert.Equal(t.WindowBackgroundColor, rc.Background);
		Assert.Equal(t.WindowForegroundColor, rc.Text);
		Assert.Equal(t.WindowForegroundColor, rc.TextOnBackground);
		Assert.Equal(t.WindowBackgroundColor, rc.Border);
	}

	[Fact]
	public void Outline_SwapsFillToSurface_AndMovesRoleColorToText()
	{
		var t = DarkTheme();
		var solid = ColorRoleResolver.Resolve(ColorRole.Success, t, outline: false);
		var outline = ColorRoleResolver.Resolve(ColorRole.Success, t, outline: true);
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
		var normal = ColorRoleResolver.Resolve(ColorRole.Primary, DarkTheme(), state: ColorRoleState.Normal);
		var disabled = ColorRoleResolver.Resolve(ColorRole.Primary, DarkTheme(), state: ColorRoleState.Disabled);
		Assert.True(disabled.Background.A < 255);
		Assert.True(disabled.Text.A < 255);
		Assert.Equal(255, normal.Background.A);
	}

	[Fact]
	public void Focused_BrightensRelativeToNormal()
	{
		// Danger is a mid-luminance red that reliably brightens under Tint.
		var normal = ColorRoleResolver.Resolve(ColorRole.Danger, DarkTheme(), state: ColorRoleState.Normal);
		var focused = ColorRoleResolver.Resolve(ColorRole.Danger, DarkTheme(), state: ColorRoleState.Focused);
		Assert.True(focused.Background.Luminance() > normal.Background.Luminance());
	}

	[Fact]
	public void NullContainer_DoesNotThrow_UsesBuiltInDefaults()
	{
		var rc = ColorRoleResolver.Resolve(ColorRole.Warning, (SharpConsoleUI.Controls.IContainer?)null);
		Assert.Equal(255, rc.Background.A);
	}
}
