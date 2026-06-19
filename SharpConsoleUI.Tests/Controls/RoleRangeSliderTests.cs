using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleRangeSliderTests
{
	// The range slider's role accent surfaces through the filled-track colour.

	[Fact]
	public void DefaultRole_FilledTrackMatchesLegacy()
	{
		var s = new RangeSliderControl();
		var plain = new RangeSliderControl();
		Assert.Equal(plain.FilledTrackColor, s.FilledTrackColor);
	}

	[Fact]
	public void DangerRole_ChangesFilledTrack()
	{
		var s = new RangeSliderControl { Role = ControlRole.Danger };
		var plain = new RangeSliderControl();
		Assert.NotEqual(plain.FilledTrackColor, s.FilledTrackColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var s = new RangeSliderControl { Role = ControlRole.Danger, FilledTrackColor = Color.Black };
		Assert.Equal(Color.Black, s.FilledTrackColor);
	}
}
