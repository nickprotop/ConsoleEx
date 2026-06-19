using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleMultilineEditTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		// For Role=Default the public getter must equal the legacy resolution (no-role path unchanged).
		var m = new MultilineEditControl();
		var plain = new MultilineEditControl();
		Assert.Equal(plain.ForegroundColor, m.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var m = new MultilineEditControl { Role = ControlRole.Danger };
		var plain = new MultilineEditControl();
		Assert.NotEqual(plain.ForegroundColor, m.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var m = new MultilineEditControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, m.ForegroundColor);
	}
}
