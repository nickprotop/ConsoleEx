using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// The two halves of multi-line toasts: the shared wrap/ellipsize used by both measuring and
/// painting, and the per-position stacking that keeps variable-height toasts from colliding.
/// </summary>
public class ToastWrapAndStackTests
{
	private static string Text(List<SharpConsoleUI.Layout.Cell> row)
		=> string.Concat(row.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString()));

	#region Wrap and ellipsize

	[Fact]
	public void TextWithinTheCap_IsNotEllipsized()
	{
		var rows = ToastTextLayout.Wrap("short", 20, 2);

		Assert.Single(rows);
		Assert.Equal("short", Text(rows[0]));
		Assert.DoesNotContain(ToastTextLayout.Ellipsis.ToString(), Text(rows[0]));
	}

	[Fact]
	public void TextBeyondTheCap_EllipsizesOnTheLastRow()
	{
		string text = string.Join(' ', Enumerable.Repeat("word", 40));
		var rows = ToastTextLayout.Wrap(text, 20, 2);

		Assert.Equal(2, rows.Count);
		Assert.EndsWith(ToastTextLayout.Ellipsis.ToString(), Text(rows[rows.Count - 1]));
	}

	[Fact]
	public void AnEllipsizedRow_StaysWithinTheWidth()
	{
		string text = string.Join(' ', Enumerable.Repeat("word", 40));

		foreach (int width in new[] { 1, 2, 5, 12, 31 })
		{
			var rows = ToastTextLayout.Wrap(text, width, 2);
			foreach (var row in rows)
				Assert.True(row.Count <= width, $"row of {row.Count} columns exceeds width {width}");
		}
	}

	[Fact]
	public void TheRowCount_NeverExceedsTheCap()
	{
		string text = string.Join(' ', Enumerable.Repeat("word", 200));

		foreach (int cap in new[] { 1, 2, 3, 8 })
			Assert.True(ToastTextLayout.Wrap(text, 20, cap).Count <= cap);
	}

	[Fact]
	public void ARowAlwaysComesBack_EvenForEmptyText()
	{
		Assert.NotEmpty(ToastTextLayout.Wrap(string.Empty, 20, 2));
	}

	[Fact]
	public void EllipsizingWideText_DoesNotLeaveHalfAGlyph()
	{
		// Cutting between a wide rune and its continuation cell would paint half a character.
		string cjk = string.Concat(Enumerable.Repeat("日本語", 40));
		var rows = ToastTextLayout.Wrap(cjk, 11, 2);

		var last = rows[rows.Count - 1];
		Assert.True(last.Count <= 11);
		Assert.EndsWith(ToastTextLayout.Ellipsis.ToString(), Text(last));
		// The row must not end on a wide rune whose continuation was dropped.
		Assert.False(last[last.Count - 1].IsWideContinuation);
	}

	#endregion

	#region Stacking

	[Fact]
	public void VariableHeightToasts_DoNotOverlap()
	{
		// Three toasts of different heights stacked at one corner, offsets accumulated as the
		// service does it.
		int gap = ControlDefaults.ToastGap;
		int[] heights = { 4, 3, 6 };

		var boxes = new List<System.Drawing.Rectangle>();
		int consumed = 0;
		foreach (int h in heights)
		{
			var b = ToastService.ComputeToastBounds(ToastPosition.BottomRight, consumed, 20, h, 100, 40);
			consumed += h + gap;
			boxes.Add(b);
		}

		for (int i = 1; i < boxes.Count; i++)
			Assert.True(boxes[i].Y + boxes[i].Height <= boxes[i - 1].Y,
				$"toast {i} overlaps toast {i - 1}");
	}

	[Fact]
	public void ToastsAtDifferentCorners_DoNotOffsetEachOther()
	{
		// Each position stacks independently: the first toast at a corner sits AT that corner,
		// whatever is open elsewhere. (A single shared slot counter used to push it away.)
		var right = ToastService.ComputeToastBounds(ToastPosition.BottomRight, 0, 20, 3, 100, 40);
		var left = ToastService.ComputeToastBounds(ToastPosition.BottomLeft, 0, 20, 3, 100, 40);

		Assert.Equal(right.Y, left.Y);
		Assert.Equal(ControlDefaults.ToastEdgeMargin, left.X);
	}

	#endregion
}
