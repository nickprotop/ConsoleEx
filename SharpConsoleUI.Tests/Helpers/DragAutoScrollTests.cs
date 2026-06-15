using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class DragAutoScrollTests
{
	private const int VH = 10; // viewport height rows

	[Fact]
	public void InsideViewport_ReturnsZero_AndResetsCarry()
	{
		double carry = 0.9;
		int step = DragAutoScroll.ComputeStep(dragRelativeY: 5, viewportHeightRows: VH, elapsedMs: 100, carry: ref carry);
		Assert.Equal(0, step);
		Assert.Equal(0.0, carry);
	}

	[Fact]
	public void AboveTop_ReturnsNegativeStep()
	{
		double carry = 0;
		int step = DragAutoScroll.ComputeStep(dragRelativeY: -3, viewportHeightRows: VH, elapsedMs: 1000, carry: ref carry);
		Assert.True(step < 0, $"expected negative, got {step}");
	}

	[Fact]
	public void BelowBottom_ReturnsPositiveStep()
	{
		double carry = 0;
		int step = DragAutoScroll.ComputeStep(dragRelativeY: VH + 3, viewportHeightRows: VH, elapsedMs: 1000, carry: ref carry);
		Assert.True(step > 0, $"expected positive, got {step}");
	}

	[Fact]
	public void LargerOvershoot_ProducesLargerStep()
	{
		double c1 = 0, c2 = 0;
		int near = DragAutoScroll.ComputeStep(VH + 1, VH, 1000, ref c1);
		int far = DragAutoScroll.ComputeStep(VH + 20, VH, 1000, ref c2);
		Assert.True(far > near, $"far={far} should exceed near={near}");
	}

	[Fact]
	public void FractionalCarry_AccumulatesToAStepOverFrames()
	{
		double carry = 0;
		int total = 0;
		for (int i = 0; i < 10; i++)
			total += DragAutoScroll.ComputeStep(VH + 1, VH, elapsedMs: 16, carry: ref carry);
		Assert.True(total >= 1, $"expected accumulated movement over 10 frames, got {total}");
	}

	[Fact]
	public void TimeNormalized_OneBigFrameEqualsTwoHalfFrames()
	{
		double cBig = 0;
		int big = DragAutoScroll.ComputeStep(VH + 5, VH, elapsedMs: 32, carry: ref cBig);

		double cSmall = 0;
		int small = DragAutoScroll.ComputeStep(VH + 5, VH, elapsedMs: 16, carry: ref cSmall);
		small += DragAutoScroll.ComputeStep(VH + 5, VH, elapsedMs: 16, carry: ref cSmall);

		Assert.Equal(big, small);
	}

	[Fact]
	public void MaxRowsPerSec_CapsTheStep()
	{
		double carry = 0;
		int step = DragAutoScroll.ComputeStep(VH + 100000, VH, elapsedMs: 1000, carry: ref carry);
		Assert.Equal(60, step);
	}
}
