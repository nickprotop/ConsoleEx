using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class ToastPositioningTests
{
	[Fact]
	public void BottomRight_StacksUpward()
	{
		var s0 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, 0, 20, 100, 40);
		var s1 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, 1, 20, 100, 40);
		Assert.True(s1.Y < s0.Y);
		Assert.Equal(s0.X, s1.X);
		Assert.True(s0.X + s0.Width <= 100);
	}

	[Fact]
	public void TopRight_StacksDownward()
	{
		var s0 = ToastService.ComputeToastBounds(ToastPosition.TopRight, 0, 20, 100, 40);
		var s1 = ToastService.ComputeToastBounds(ToastPosition.TopRight, 1, 20, 100, 40);
		Assert.True(s1.Y > s0.Y);
	}

	[Fact]
	public void BottomCenter_IsCentered()
	{
		var s = ToastService.ComputeToastBounds(ToastPosition.BottomCenter, 0, 20, 100, 40);
		Assert.Equal((100 - 20) / 2, s.X);
	}
}
