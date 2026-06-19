using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleDatePickerTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		var d = new DatePickerControl();
		var plain = new DatePickerControl();
		Assert.Equal(plain.ForegroundColor, d.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var d = new DatePickerControl { Role = ControlRole.Danger };
		var plain = new DatePickerControl();
		Assert.NotEqual(plain.ForegroundColor, d.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var d = new DatePickerControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, d.ForegroundColor);
	}
}
