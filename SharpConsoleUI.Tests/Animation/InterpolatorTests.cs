using SharpConsoleUI.Animation;
using Xunit;

namespace SharpConsoleUI.Tests.Animation;

public class InterpolatorTests
{
	[Fact]
	public void ByteInterpolator_AtHalf_ReturnsMiddle()
	{
		byte result = ByteInterpolator.Instance.Interpolate(0, 255, 0.5);
		// 0 + (255-0)*0.5 = 127.5 -> rounds to 128
		Assert.Equal(128, result);
	}

	[Fact]
	public void ByteInterpolator_AtZero_ReturnsFrom()
	{
		Assert.Equal(50, ByteInterpolator.Instance.Interpolate(50, 200, 0.0));
	}

	[Fact]
	public void ByteInterpolator_AtOne_ReturnsTo()
	{
		Assert.Equal(200, ByteInterpolator.Instance.Interpolate(50, 200, 1.0));
	}

	[Fact]
	public void ByteInterpolator_ClampsToByteRange()
	{
		// t > 1 should still clamp via Math.Clamp
		byte result = ByteInterpolator.Instance.Interpolate(0, 255, 1.1);
		Assert.Equal(255, result);
	}

	[Fact]
	public void IntInterpolator_AtHalf_ReturnsMiddle()
	{
		int result = IntInterpolator.Instance.Interpolate(0, 100, 0.5);
		Assert.Equal(50, result);
	}

	[Fact]
	public void IntInterpolator_Rounds_Correctly()
	{
		// 0 + (100-0)*0.33 = 33
		int result = IntInterpolator.Instance.Interpolate(0, 100, 0.33);
		Assert.Equal(33, result);
	}

	[Fact]
	public void FloatInterpolator_AtQuarter_ReturnsQuarter()
	{
		float result = FloatInterpolator.Instance.Interpolate(0f, 100f, 0.25);
		Assert.Equal(25f, result, precision: 2);
	}

	[Fact]
	public void ColorInterpolator_BlendsChannelsIndependently()
	{
		var from = new Color(0, 0, 0);
		var to = new Color(255, 128, 64);

		Color result = ColorInterpolator.Instance.Interpolate(from, to, 0.5);

		Assert.Equal(128, result.R);
		Assert.Equal(64, result.G);
		Assert.Equal(32, result.B);
	}

	[Fact]
	public void ColorInterpolator_AtZero_ReturnsFrom()
	{
		var from = new Color(10, 20, 30);
		var to = new Color(200, 210, 220);

		Color result = ColorInterpolator.Instance.Interpolate(from, to, 0.0);

		Assert.Equal(from.R, result.R);
		Assert.Equal(from.G, result.G);
		Assert.Equal(from.B, result.B);
	}

	[Fact]
	public void ColorInterpolator_AtOne_ReturnsTo()
	{
		var from = new Color(10, 20, 30);
		var to = new Color(200, 210, 220);

		Color result = ColorInterpolator.Instance.Interpolate(from, to, 1.0);

		Assert.Equal(to.R, result.R);
		Assert.Equal(to.G, result.G);
		Assert.Equal(to.B, result.B);
	}
}
