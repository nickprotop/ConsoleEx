using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleCheckboxTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		// For Role=Default the public getter must equal the legacy resolution (no-role path unchanged).
		var c = new CheckboxControl();
		var plain = new CheckboxControl();
		Assert.Equal(plain.ForegroundColor, c.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var c = new CheckboxControl { Role = ControlRole.Danger };
		var plain = new CheckboxControl();
		Assert.NotEqual(plain.ForegroundColor, c.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var c = new CheckboxControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, c.ForegroundColor);
	}

	[Fact]
	public void Role_DoesNotChangeBackground()
	{
		// A checkbox is a surface-text control; a role colours the LABEL, not a solid background fill.
		var plain = new CheckboxControl();
		var danger = new CheckboxControl { Role = ControlRole.Danger };
		// Resolved background must be identical (role must not introduce a solid fill).
		Assert.Equal(plain.ResolvedBackgroundColor, danger.ResolvedBackgroundColor);
	}
}
