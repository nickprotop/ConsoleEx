using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Toast sizing: the box wraps its text up to a line cap, ellipsizes past it, and honours the
/// min/max width knobs. Measured through <see cref="ToastService.MeasureToast(string,int?,int?,int?,int,int)"/>,
/// the same call the live service sizes a toast with.
/// </summary>
public class ToastSizingTests
{
	private const int DefaultMaxLines = ControlDefaults.ToastMaxLines;
	private const int Chrome = ControlDefaults.ToastChromeWidth;
	private const int ChromeH = ControlDefaults.ToastChromeHeight;

	private static (int Width, int Height) Measure(string text, int? maxLines = null, int? minWidth = null,
		int? maxWidth = null, int desktopWidth = 100)
		=> ToastService.MeasureToast(text, maxLines, minWidth, maxWidth, desktopWidth, DefaultMaxLines);

	[Fact]
	public void AShortMessage_StaysOneLineAndShrinksToFit()
	{
		var (w, h) = Measure("Copied");

		Assert.Equal(1 + ChromeH, h);
		Assert.Equal("Copied".Length + Chrome, w);
	}

	[Fact]
	public void ALongMessage_WrapsAndGrowsTaller()
	{
		string text = string.Join(' ', Enumerable.Repeat("word", 30));
		var (_, h) = Measure(text, maxLines: 4);

		Assert.True(h > 1 + ChromeH, "a long message should occupy more than one content row");
		Assert.Equal(4 + ChromeH, h);
	}

	[Fact]
	public void MaxLines_CapsTheHeight()
	{
		string text = string.Join(' ', Enumerable.Repeat("word", 80));

		Assert.Equal(1 + ChromeH, Measure(text, maxLines: 1).Height);
		Assert.Equal(2 + ChromeH, Measure(text, maxLines: 2).Height);
		Assert.Equal(5 + ChromeH, Measure(text, maxLines: 5).Height);
	}

	[Fact]
	public void MaxLinesBelowOne_StillShowsALine()
	{
		string text = string.Join(' ', Enumerable.Repeat("word", 20));
		Assert.Equal(1 + ChromeH, Measure(text, maxLines: 0).Height);
		Assert.Equal(1 + ChromeH, Measure(text, maxLines: -5).Height);
	}

	[Fact]
	public void TheDefaultLineCap_IsTwo()
	{
		string text = string.Join(' ', Enumerable.Repeat("word", 80));
		Assert.Equal(DefaultMaxLines + ChromeH, Measure(text).Height);
	}

	[Fact]
	public void MinWidth_FloorsAShortToast()
	{
		var bare = Measure("Hi");
		var floored = Measure("Hi", minWidth: 40);

		Assert.True(bare.Width < 40);
		Assert.Equal(40, floored.Width);
	}

	[Fact]
	public void NoMinWidth_MeansShrinkToContent()
	{
		// null min width is "no floor", the historical behaviour — not some built-in minimum.
		Assert.Equal("Hi".Length + Chrome, Measure("Hi").Width);
	}

	[Fact]
	public void MaxWidth_CapsAWideToast()
	{
		string text = new string('x', 200);
		Assert.Equal(30, Measure(text, maxWidth: 30).Width);
	}

	[Fact]
	public void AutoMaxWidth_IsHalfTheDesktopButNeverBelowTheBaseline()
	{
		string text = new string('x', 400);

		// Narrow and standard terminals keep the classic 48.
		Assert.Equal(ControlDefaults.ToastMaxWidth, Measure(text, desktopWidth: 80).Width);
		// Wide ones get half.
		Assert.Equal(100, Measure(text, desktopWidth: 200).Width);
	}

	[Fact]
	public void TheToastNeverExceedsTheDesktop()
	{
		string text = new string('x', 500);
		int desktop = 30;

		var (w, _) = Measure(text, maxWidth: 999, desktopWidth: desktop);
		Assert.True(w <= desktop - 2 * ControlDefaults.ToastEdgeMargin);
	}

	[Fact]
	public void AMinWiderThanTheTerminal_DegradesInsteadOfThrowing()
	{
		// Math.Clamp throws when min > max, so the floor must be capped first.
		var (w, h) = Measure("Hi", minWidth: 400, desktopWidth: 30);

		Assert.True(w <= 30);
		Assert.True(w >= 1);
		Assert.True(h >= 1 + ChromeH);
	}

	[Fact]
	public void MaxWidth_WinsOverMinWidth()
	{
		// Being narrower than the floor is merely ugly; exceeding the cap can run off-screen.
		Assert.Equal(20, Measure("Hi", minWidth: 60, maxWidth: 20).Width);
	}

	[Fact]
	public void AVeryNarrowDesktop_ProducesNoNegativeOrZeroBox()
	{
		foreach (int desktop in new[] { 1, 2, 3, 5 })
		{
			var (w, h) = Measure("Something long enough to wrap", desktopWidth: desktop);
			Assert.True(w >= 1, $"width {w} at desktop {desktop}");
			Assert.True(h >= 1 + ChromeH, $"height {h} at desktop {desktop}");
		}
	}

	[Fact]
	public void WideCharacters_MeasureInColumnsNotCharCount()
	{
		// Each CJK glyph is two columns; measuring by string length would under-size the box.
		string cjk = "日本語テキスト";
		var (w, _) = Measure(cjk, minWidth: null, maxWidth: 200, desktopWidth: 200);

		Assert.Equal(cjk.Length * 2 + Chrome, w);
	}
}
