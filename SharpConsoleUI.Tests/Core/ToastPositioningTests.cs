using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class ToastPositioningTests
{
	// Toasts vary in height now, so the stacking parameter is the rows already consumed by the toasts
	// below this one — not a slot index. A uniform 3-row toast gives offsets of 0, 3, 6...
	private const int H = 3;

	[Fact]
	public void BottomRight_StacksUpward()
	{
		var s0 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, 0, 20, H, 100, 40);
		var s1 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, H, 20, H, 100, 40);
		Assert.True(s1.Y < s0.Y);
		Assert.Equal(s0.X, s1.X);
		Assert.True(s0.X + s0.Width <= 100);
	}

	[Fact]
	public void ConsecutiveToasts_AreTheirOwnHeightApart()
	{
		var s0 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, 0, 20, H, 100, 40);
		var s1 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, H + ControlDefaults.ToastGap, 20, H, 100, 40);
		Assert.Equal(H + ControlDefaults.ToastGap, s0.Y - s1.Y);
	}

	[Fact]
	public void ATallToast_PushesTheNextOneItsOwnHeight()
	{
		// A 5-row toast must move the next one 5 rows, not a fixed slot pitch — otherwise they overlap.
		const int tall = 5;
		var s0 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, 0, 20, tall, 100, 40);
		var s1 = ToastService.ComputeToastBounds(ToastPosition.BottomRight, tall + ControlDefaults.ToastGap, 20, H, 100, 40);

		Assert.Equal(tall, s0.Height);
		// No overlap: the upper toast ends at or above where the lower one starts.
		Assert.True(s1.Y + s1.Height <= s0.Y);
	}

	[Fact]
	public void TopRight_StacksDownward()
	{
		var s0 = ToastService.ComputeToastBounds(ToastPosition.TopRight, 0, 20, H, 100, 40);
		var s1 = ToastService.ComputeToastBounds(ToastPosition.TopRight, H, 20, H, 100, 40);
		Assert.True(s1.Y > s0.Y);
	}

	[Fact]
	public void BottomCenter_IsCentered()
	{
		var s = ToastService.ComputeToastBounds(ToastPosition.BottomCenter, 0, 20, H, 100, 40);
		Assert.Equal((100 - 20) / 2, s.X);
	}

	[Fact]
	public void TheGivenHeight_IsTheBoxHeight()
	{
		var s = ToastService.ComputeToastBounds(ToastPosition.BottomRight, 0, 20, 7, 100, 40);
		Assert.Equal(7, s.Height);
	}
}
