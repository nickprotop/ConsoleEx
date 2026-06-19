using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleButtonTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		// For Role=Default the public getter must equal the legacy resolution (no-role path unchanged).
		var b = new ButtonControl();
		Assert.Equal(ColorResolver.ResolveButtonForeground(null, null), b.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var b = new ButtonControl { Role = ControlRole.Danger };
		var plain = new ButtonControl();
		Assert.NotEqual(plain.ForegroundColor, b.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var b = new ButtonControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, b.ForegroundColor);
	}

	[Fact]
	public void Outline_SwapsTextAndBackground()
	{
		var theme = new ModernGrayTheme();
		var outline = RoleResolver.Resolve(ControlRole.Success, theme, outline: true);
		var filled = RoleResolver.Resolve(ControlRole.Success, theme, outline: false);

		// Outline keeps the surface background and paints the role colour as text/border — the same
		// contrast-ensured-on-surface colour the filled variant uses for its text/border, so it stays
		// readable even when the role seed sits close to the window background.
		Assert.Equal(theme.WindowBackgroundColor, outline.Background);
		Assert.Equal(filled.Text, outline.Text);
	}
}
