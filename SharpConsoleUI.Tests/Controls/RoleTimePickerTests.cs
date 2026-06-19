using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleTimePickerTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		var t = new TimePickerControl();
		var plain = new TimePickerControl();
		Assert.Equal(plain.ForegroundColor, t.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var t = new TimePickerControl { Role = ControlRole.Danger };
		var plain = new TimePickerControl();
		Assert.NotEqual(plain.ForegroundColor, t.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var t = new TimePickerControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, t.ForegroundColor);
	}
}
