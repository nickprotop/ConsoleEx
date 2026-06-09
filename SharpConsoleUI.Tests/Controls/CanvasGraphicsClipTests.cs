// -----------------------------------------------------------------------
// Regression tests for canvas drawing primitives respecting the clip rect.
//
// Bug: a CanvasControl scrolled inside a ScrollablePanel painted its boxes,
// lines, fills and Clear() OVER sibling controls (e.g. a NavigationView nav
// pane) because several CanvasGraphics primitives delegated to raw
// CharacterBuffer methods that ignored the clip rect. Only the cell-level and
// text primitives clipped. These tests pin that EVERY primitive clips.
// -----------------------------------------------------------------------

using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

public class CanvasGraphicsClipTests
{
	private readonly ITestOutputHelper _out;
	public CanvasGraphicsClipTests(ITestOutputHelper o) { _out = o; }

	// Builds a CanvasGraphics whose content origin is LEFT of and ABOVE the clip rect — simulating
	// a canvas scrolled right/down inside a panel. Canvas-local (0,0) maps to (offsetX, offsetY),
	// which is outside the clip, so any primitive that ignores the clip will write there.
	private static (CharacterBuffer buffer, CanvasGraphics g, LayoutRect clip) MakeScrolledCanvas(
		int bufW = 40, int bufH = 20, int offsetX = 5, int offsetY = 3,
		int canvasW = 30, int canvasH = 14, int clipX = 12, int clipY = 6, int clipW = 20, int clipH = 10)
	{
		var buffer = new CharacterBuffer(bufW, bufH, Color.Black);
		var clip = new LayoutRect(clipX, clipY, clipW, clipH);
		var g = new CanvasGraphics(buffer, offsetX, offsetY, canvasW, canvasH, clip);
		return (buffer, g, clip);
	}

	// Returns every absolute cell written with a non-space, OR with a non-default background,
	// that falls OUTSIDE the clip rect. (A leak is any visible mark beyond the clip.)
	private static List<(int x, int y, char ch)> CellsOutsideClip(CharacterBuffer buffer, LayoutRect clip)
	{
		var leaks = new List<(int, int, char)>();
		for (int y = 0; y < buffer.Height; y++)
			for (int x = 0; x < buffer.Width; x++)
			{
				if (clip.Contains(x, y)) continue;
				var cell = buffer.GetCell(x, y);
				char ch = (char)cell.Character.Value;
				bool painted = ch != ' ' && ch != '\0';
				bool bgPainted = !cell.Background.Equals(Color.Black);
				if (painted || bgPainted)
					leaks.Add((x, y, painted ? ch : '·'));
			}
		return leaks;
	}

	private void AssertNoLeak(CharacterBuffer buffer, LayoutRect clip, string primitive)
	{
		var leaks = CellsOutsideClip(buffer, clip);
		if (leaks.Count > 0)
			_out.WriteLine($"{primitive} leaked: {string.Join(", ", leaks.Take(20).Select(l => $"({l.x},{l.y})'{l.ch}'"))}");
		Assert.True(leaks.Count == 0, $"{primitive} painted {leaks.Count} cell(s) outside the clip rect");
	}

	// Each primitive is drawn spanning the full canvas (well beyond the clip on all sides), then
	// we assert nothing landed outside the clip.

	[Fact]
	public void SetNarrowCell_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		for (int y = 0; y < 14; y++) for (int x = 0; x < 30; x++) g.SetNarrowCell(x, y, 'A', Color.White, Color.Blue);
		AssertNoLeak(b, clip, "SetNarrowCell");
	}

	[Fact]
	public void Clear_Color_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.Clear(Color.Red); // the background-fill leak the user hit
		AssertNoLeak(b, clip, "Clear(color)");
	}

	[Fact]
	public void Clear_CharColor_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.Clear('#', Color.White, Color.Red);
		AssertNoLeak(b, clip, "Clear(char)");
	}

	[Fact]
	public void FillRect_Char_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.FillRect(0, 0, 30, 14, '*', Color.White, Color.Green);
		AssertNoLeak(b, clip, "FillRect(char)");
	}

	[Fact]
	public void FillRect_Bg_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.FillRect(0, 0, 30, 14, Color.Green);
		AssertNoLeak(b, clip, "FillRect(bg)");
	}

	[Fact]
	public void DrawBox_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.DrawBox(0, 0, 30, 14, BoxChars.FromBorderStyle(BorderStyle.Rounded), Color.White, Color.Black);
		AssertNoLeak(b, clip, "DrawBox");
	}

	[Fact]
	public void DrawHorizontalLine_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		for (int y = 0; y < 14; y++) g.DrawHorizontalLine(0, y, 30, '─', Color.White, Color.Black);
		AssertNoLeak(b, clip, "DrawHorizontalLine");
	}

	[Fact]
	public void DrawVerticalLine_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		for (int x = 0; x < 30; x++) g.DrawVerticalLine(x, 0, 14, '│', Color.White, Color.Black);
		AssertNoLeak(b, clip, "DrawVerticalLine");
	}

	[Fact]
	public void DrawLine_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.DrawLine(0, 0, 29, 13, '\\', Color.White, Color.Black);
		g.DrawLine(0, 13, 29, 0, '/', Color.White, Color.Black);
		AssertNoLeak(b, clip, "DrawLine");
	}

	[Fact]
	public void WriteString_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		for (int y = 0; y < 14; y++) g.WriteString(0, y, new string('W', 30), Color.White, Color.Black);
		AssertNoLeak(b, clip, "WriteString");
	}

	[Fact]
	public void DrawCircle_And_FillCircle_DoNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.DrawCircle(15, 7, 13, 'o', Color.White, Color.Black);
		g.FillCircle(15, 7, 10, '.', Color.White, Color.Black);
		AssertNoLeak(b, clip, "DrawCircle/FillCircle");
	}

	[Fact]
	public void GradientFill_DoesNotLeak()
	{
		var (b, g, clip) = MakeScrolledCanvas();
		g.GradientFillRect(0, 0, 30, 14, Color.Red, Color.Blue, horizontal: true);
		AssertNoLeak(b, clip, "GradientFillRect");
	}

	[Fact]
	public void AllPrimitivesTogether_DoNotLeak()
	{
		// Composite draw resembling TopologyRenderer: clear, box, connector line, label.
		var (b, g, clip) = MakeScrolledCanvas();
		g.Clear(Color.Black);
		g.DrawBox(0, 0, 22, 3, BoxChars.FromBorderStyle(BorderStyle.Rounded), Color.Cyan1, Color.Black);
		g.FillRect(1, 1, 20, 1, ' ', Color.White, Color.Black);
		g.WriteString(1, 1, "example.test /api", Color.White, Color.Black);
		g.DrawHorizontalLine(22, 1, 8, '─', Color.Grey, Color.Black);
		AssertNoLeak(b, clip, "composite");
	}
}
