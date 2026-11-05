// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// How the cost of ONE scroll must scale with the number of children a panel holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <c>ScrollLayout.GetPaintClipRect</c> is a PER-CHILD hook: the paint walk
/// calls it once for every child, including the ones scrolled out of view. It used to call
/// <c>Bands()</c>, which materializes a list of every child in the panel to partition them, so one
/// paint of N children allocated N lists of N entries. That is O(N^2) in allocation: at 2,000
/// children a single scroll allocated 32 MB and triggered ~3,875 Gen1 collections, which a user
/// experiences as the panel getting progressively less smooth the longer their session runs.
/// </para>
/// <para>
/// <b>Why it asserts a RATIO and not a byte count.</b> An absolute budget would be a machine and
/// runtime fingerprint, green on one box and red on the next for no reason anyone can act on. The
/// defect here was a complexity class, so the assertion is on the complexity class: quadruple the
/// children and the per-scroll cost must not quadruple with them. The quadratic form fails this by
/// a wide margin (~16x the per-child cost over a 4x range), while anything linear-or-better passes
/// comfortably, so the threshold does not need to be tight to be load-bearing.
/// </para>
/// <para>
/// <b>Why it drives the real window.</b> Calling <c>GetPaintClipRect</c> directly would measure the
/// method and miss the thing that made it expensive, which is how many times the paint walk invokes
/// it. So this builds a real <see cref="Window"/>, adds a real panel with real children, and scrolls
/// it through the public API with a real render between each step, exactly as a wheel notch does.
/// </para>
/// </remarks>
public class ScrollPaintClipScalingTests
{
	private const int WindowWidth = 120;
	private const int WindowHeight = 40;

	/// <summary>Scrolls a panel of <paramref name="children"/> labels and returns bytes allocated per scroll.</summary>
	private static double BytesPerScroll(int children, int scrolls)
	{
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		var window = new Window(system) { Width = WindowWidth, Height = WindowHeight };
		var panel = new ScrollablePanelControl { Height = WindowHeight - 4 };

		for (int i = 0; i < children; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"[bold]{i:D5}[/] line {i} of a transcript" }));

		window.AddControl(panel);

		// Warm everything that is first-call-only: the DOM tree, the parse cache for the rows that
		// become visible, and the panel's metrics. Measuring without this would time the first paint.
		window.RenderAndGetVisibleContent();
		for (int i = 0; i < 12; i++)
		{
			panel.ScrollVerticalBy(1);
			window.RenderAndGetVisibleContent();
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long before = GC.GetAllocatedBytesForCurrentThread();

		// Oscillate so the viewport stays mid-document: walking to an end would clamp and the
		// later scrolls would measure a no-op.
		int direction = 1;
		for (int i = 0; i < scrolls; i++)
		{
			direction = -direction;
			panel.ScrollVerticalBy(direction);
			window.RenderAndGetVisibleContent();
		}

		return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)scrolls;
	}

	/// <summary>
	/// The per-child cost of a scroll must not grow as children are added. Quadrupling the child
	/// count quadruples the total work under a linear law and sixteen-times it under a quadratic
	/// one, so the per-child figure separates the two cleanly.
	/// </summary>
	[Fact]
	public void ScrollCost_PerChild_DoesNotGrowWithChildCount()
	{
		const int Small = 500;
		const int Large = 2000;
		const int Scrolls = 12;

		double smallPerChild = BytesPerScroll(Small, Scrolls) / Small;
		double largePerChild = BytesPerScroll(Large, Scrolls) / Large;

		// Linear-or-better keeps this ratio at or below 1. The quadratic form drove it to ~4 over
		// this 4x range (and higher over a wider one). 2.0 leaves room for the fixed per-frame cost
		// being amortized differently at the two sizes without admitting the defect.
		double ratio = largePerChild / smallPerChild;

		Assert.True(ratio <= 2.0,
			$"per-child scroll cost grew with child count: {smallPerChild:N0} B/child at {Small} " +
			$"children vs {largePerChild:N0} B/child at {Large} (ratio {ratio:F2}). A per-child paint " +
			$"hook is allocating per-panel work. See ScrollLayout.GetPaintClipRect.");
	}

	/// <summary>
	/// The absolute guard. Per-child-per-scroll cost has a real ceiling: the scrolled-out children
	/// are clipped before they paint, so each one should cost a clip-rect computation and nothing
	/// that scales with the panel. Catches a regression that keeps the slope flat but raises the
	/// whole line.
	/// </summary>
	[Fact]
	public void ScrollCost_PerChild_StaysWithinAPlausibleCeiling()
	{
		const int Children = 2000;
		double perChild = BytesPerScroll(Children, 12) / Children;

		// Measured at ~450 B/child after the fix and ~16,400 B/child before it, so this sits an
		// order of magnitude below the defect and well above the healthy value.
		Assert.True(perChild <= 4000,
			$"a scroll allocated {perChild:N0} B per child, which is too much for children that are " +
			$"clipped out before painting. Expected a few hundred bytes.");
	}
}
