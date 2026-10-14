// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// Direct cover for <see cref="VisibleRegions"/>, the rectangle-subtraction that decides which parts
/// of a window are drawn. It is pure geometry and was reachable only through full renders, so a
/// wrong rectangle showed up as a visual artefact rather than a failing assert.
/// <para>
/// The area identity is what the renderer actually depends on: whatever rectangles come back must
/// cover exactly the uncovered part of the window, no more (nothing drawn over a window above) and
/// no less (no gap left unpainted). Asserting total area plus disjointness pins that without
/// freezing the particular way the remainder is cut into pieces.
/// </para>
/// </summary>
public class VisibleRegionsSubtractionTests
{
	#region Harness

	private static (ConsoleWindowSystem system, VisibleRegions regions) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		return (system, new VisibleRegions(system));
	}

	private static Window Win(ConsoleWindowSystem system, int left, int top, int width, int height)
		=> new(system) { Left = left, Top = top, Width = width, Height = height };

	private static int Area(IEnumerable<Rectangle> rects)
	{
		int total = 0;
		foreach (var r in rects) total += r.Width * r.Height;
		return total;
	}

	/// <summary>True if no two rectangles overlap, so summing their areas is meaningful.</summary>
	private static bool Disjoint(IReadOnlyList<Rectangle> rects)
	{
		for (int i = 0; i < rects.Count; i++)
		{
			for (int j = i + 1; j < rects.Count; j++)
			{
				var a = rects[i];
				var b = rects[j];
				bool overlap = a.Left < b.Left + b.Width && a.Left + a.Width > b.Left
					&& a.Top < b.Top + b.Height && a.Top + a.Height > b.Top;
				if (overlap) return false;
			}
		}
		return true;
	}

	/// <summary>Whether any returned rectangle contains a point, i.e. that cell gets painted.</summary>
	private static bool Covers(IEnumerable<Rectangle> rects, int x, int y)
	{
		foreach (var r in rects)
		{
			if (x >= r.Left && x < r.Left + r.Width && y >= r.Top && y < r.Top + r.Height)
				return true;
		}
		return false;
	}

	#endregion

	#region The shapes of a subtraction

	[Fact]
	public void NothingOverlapping_KeepsTheWholeArea()
	{
		var (system, regions) = Build();
		var area = new Rectangle(0, 0, 20, 10);

		var result = regions.CalculateVisibleRegions(area, new List<Window>());

		Assert.Equal(200, Area(result));
		Assert.True(Covers(result, 0, 0));
		Assert.True(Covers(result, 19, 9));
	}

	[Fact]
	public void AWindowThatMissesTheArea_KeepsTheWholeArea()
	{
		var (system, regions) = Build();
		var area = new Rectangle(0, 0, 20, 10);
		var other = Win(system, 50, 50, 10, 5);

		var result = regions.CalculateVisibleRegions(area, new List<Window> { other });

		Assert.Equal(200, Area(result));
	}

	[Fact]
	public void AWindowCoveringEverything_LeavesNothing()
	{
		var (system, regions) = Build();
		var area = new Rectangle(10, 5, 20, 10);
		var other = Win(system, 0, 0, 100, 50);

		var result = regions.CalculateVisibleRegions(area, new List<Window> { other });

		Assert.Empty(result);
	}

	/// <summary>A window over one edge leaves a single strip.</summary>
	[Fact]
	public void AWindowOverTheLeftEdge_LeavesTheRightStrip()
	{
		var (system, regions) = Build();
		var area = new Rectangle(0, 0, 20, 10);
		var other = Win(system, 0, 0, 8, 10);

		var result = regions.CalculateVisibleRegions(area, new List<Window> { other });

		Assert.Equal(120, Area(result));           // 12 wide x 10 tall
		Assert.True(Disjoint(result));
		Assert.False(Covers(result, 7, 5), "the covered column must not be painted");
		Assert.True(Covers(result, 8, 5), "the first uncovered column must be painted");
	}

	/// <summary>
	/// THE HARD CASE: a window entirely inside the area leaves a ring, which cannot be expressed as
	/// one rectangle. The pieces must tile the ring exactly — this is where an off-by-one shows up as
	/// a seam of unpainted cells or a double-painted row.
	/// </summary>
	[Fact]
	public void AWindowInTheMiddle_LeavesAnExactRing()
	{
		var (system, regions) = Build();
		var area = new Rectangle(0, 0, 20, 10);
		var other = Win(system, 5, 3, 6, 4);

		var result = regions.CalculateVisibleRegions(area, new List<Window> { other });

		Assert.True(result.Count > 1, "a hole in the middle needs several rectangles");
		Assert.True(Disjoint(result), "the pieces must not overlap, or cells are painted twice");
		Assert.Equal(200 - 24, Area(result));

		// Every cell of the hole is excluded, and the cells just outside it are kept.
		for (int y = 3; y < 7; y++)
		{
			for (int x = 5; x < 11; x++)
				Assert.False(Covers(result, x, y), $"covered cell ({x},{y}) must not be painted");
		}
		Assert.True(Covers(result, 4, 5));
		Assert.True(Covers(result, 11, 5));
		Assert.True(Covers(result, 7, 2));
		Assert.True(Covers(result, 7, 7));
	}

	/// <summary>Two windows subtract cumulatively, not just the last one.</summary>
	[Fact]
	public void TwoWindows_BothSubtract()
	{
		var (system, regions) = Build();
		var area = new Rectangle(0, 0, 20, 10);
		var left = Win(system, 0, 0, 5, 10);
		var right = Win(system, 15, 0, 5, 10);

		var result = regions.CalculateVisibleRegions(area, new List<Window> { left, right });

		Assert.Equal(100, Area(result));           // the middle 10 columns
		Assert.True(Disjoint(result));
		Assert.False(Covers(result, 2, 5));
		Assert.False(Covers(result, 17, 5));
		Assert.True(Covers(result, 10, 5));
	}

	/// <summary>Overlapping subtractions must not double-remove and leave a gap.</summary>
	[Fact]
	public void TwoOverlappingWindows_LeaveTheCorrectRemainder()
	{
		var (system, regions) = Build();
		var area = new Rectangle(0, 0, 20, 10);
		var a = Win(system, 0, 0, 10, 10);
		var b = Win(system, 5, 0, 10, 10);   // overlaps a across columns 5..9

		var result = regions.CalculateVisibleRegions(area, new List<Window> { a, b });

		Assert.Equal(50, Area(result));            // columns 15..19 survive
		Assert.True(Disjoint(result));
		Assert.False(Covers(result, 14, 5));
		Assert.True(Covers(result, 15, 5));
	}

	/// <summary>A window touching edge-to-edge does not overlap, so nothing is removed.</summary>
	[Fact]
	public void AWindowExactlyAbutting_RemovesNothing()
	{
		var (system, regions) = Build();
		var area = new Rectangle(0, 0, 20, 10);
		var other = Win(system, 20, 0, 5, 10);   // starts where the area ends

		var result = regions.CalculateVisibleRegions(area, new List<Window> { other });

		Assert.Equal(200, Area(result));
	}

	/// <summary>The window overload agrees with the rectangle overload for the same geometry.</summary>
	[Fact]
	public void TheWindowOverload_MatchesTheRectangleOverload()
	{
		var (system, regions) = Build();
		var target = Win(system, 2, 1, 20, 10);
		var other = Win(system, 5, 3, 6, 4);

		var byWindow = regions.CalculateVisibleRegions(target, new List<Window> { other });
		var byRect = regions.CalculateVisibleRegions(
			new Rectangle(2, 1, 20, 10), new List<Window> { other });

		Assert.Equal(Area(byRect), Area(byWindow));
		Assert.Equal(byRect.Count, byWindow.Count);
	}

	#endregion

	#region The pooled buffers

	/// <summary>
	/// THE OWNERSHIP CONTRACT. The public overloads must hand back a list the caller owns: the hot
	/// path reuses two pooled buffers, so returning one of those directly would let a second call
	/// rewrite a result the caller is still holding. This is the allocation the render path avoids
	/// through the internal no-copy overloads, and the reason the public ones keep copying.
	/// </summary>
	[Fact]
	public void SuccessiveCalls_DoNotRewriteAnEarlierResult()
	{
		var (system, regions) = Build();
		var first = regions.CalculateVisibleRegions(new Rectangle(0, 0, 20, 10), new List<Window>());
		int firstArea = Area(first);

		// A different query, which rewrites the pooled buffers underneath.
		var second = regions.CalculateVisibleRegions(
			new Rectangle(0, 0, 4, 4), new List<Window> { Win(system, 0, 0, 2, 4) });

		Assert.Equal(firstArea, Area(first));
		Assert.Equal(200, Area(first));
		Assert.Equal(8, Area(second));
		Assert.NotSame(first, second);
	}

	#endregion
}
