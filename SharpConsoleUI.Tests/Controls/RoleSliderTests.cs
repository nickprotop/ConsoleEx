using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleSliderTests
{
	// The slider's role accent surfaces through the filled-track colour.

	[Fact]
	public void DefaultRole_FilledTrackMatchesLegacy()
	{
		var s = new SliderControl();
		var plain = new SliderControl();
		Assert.Equal(plain.FilledTrackColor, s.FilledTrackColor);
	}

	[Fact]
	public void DangerRole_ChangesFilledTrack()
	{
		var s = new SliderControl { Role = ControlRole.Danger };
		var plain = new SliderControl();
		Assert.NotEqual(plain.FilledTrackColor, s.FilledTrackColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var s = new SliderControl { Role = ControlRole.Danger, FilledTrackColor = Color.Black };
		Assert.Equal(Color.Black, s.FilledTrackColor);
	}
}
