using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleDropdownTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		// For Role=Default the public getter must equal the legacy resolution (no-role path unchanged).
		var d = new DropdownControl();
		var plain = new DropdownControl();
		Assert.Equal(plain.ForegroundColor, d.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var d = new DropdownControl { Role = ControlRole.Danger };
		var plain = new DropdownControl();
		Assert.NotEqual(plain.ForegroundColor, d.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var d = new DropdownControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, d.ForegroundColor);
	}
}
