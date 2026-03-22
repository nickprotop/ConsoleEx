using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using System.Text;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for foreground alpha compositing in CharacterBuffer.
/// Foreground alpha composites against the resolved background (not the existing foreground),
/// because the background is physically what sits beneath the character glyph.
/// </summary>
public class CharacterBufferForegroundAlphaTests
{
	// ── SetNarrowCell foreground alpha ───────────────────────────────────────

	[Fact]
	public void SetNarrowCell_OpaqueForeground_StoredAsIs()
	{
		var buffer = new CharacterBuffer(10, 10);
		var fg = new Color(255, 0, 0, 255);
		buffer.SetNarrowCell(0, 0, 'A', fg, Color.Black);
		Assert.Equal(fg, buffer.GetCell(0, 0).Foreground);
	}

	[Fact]
	public void SetNarrowCell_TransparentForeground_UsesBackground()
	{
		var buffer = new CharacterBuffer(10, 10);
		var bg = new Color(0, 0, 255, 255);
		buffer.SetNarrowCell(0, 0, 'A', Color.White, bg);

		// Write fully transparent foreground — should composite to the background color
		buffer.SetNarrowCell(0, 0, 'B', Color.Transparent, bg);
		Assert.Equal(bg, buffer.GetCell(0, 0).Foreground);
	}

	[Fact]
	public void SetNarrowCell_SemitransparentForeground_BlendsWithBackground()
	{
		var buffer = new CharacterBuffer(10, 10);
		var bg = new Color(0, 0, 255, 255);
		// Seed with blue background
		buffer.SetNarrowCell(0, 0, 'A', Color.White, bg);

		// Write 50% red foreground over blue background — result should be ~halfway between red and blue
		buffer.SetNarrowCell(0, 0, 'B', new Color(255, 0, 0, 128), bg);
		var fg = buffer.GetCell(0, 0).Foreground;

		Assert.InRange(fg.R, 120, 135);
		Assert.InRange(fg.B, 120, 135);
		Assert.Equal(255, fg.A);
	}

	[Fact]
	public void SetNarrowCell_OpaqueForeground_OpaqueResult()
	{
		var buffer = new CharacterBuffer(10, 10);
		buffer.SetNarrowCell(0, 0, 'A', new Color(200, 100, 50, 255), Color.Black);
		Assert.Equal(255, buffer.GetCell(0, 0).Foreground.A);
	}

	// ── SetCell foreground alpha ─────────────────────────────────────────────

	[Fact]
	public void SetCell_OpaqueForeground_StoredAsIs()
	{
		var buffer = new CharacterBuffer(10, 10);
		var fg = new Color(0, 255, 0, 255);
		var cell = new Cell { Character = new System.Text.Rune('X'), Foreground = fg, Background = Color.Black };
		buffer.SetCell(0, 0, cell);
		Assert.Equal(fg, buffer.GetCell(0, 0).Foreground);
	}

	[Fact]
	public void SetCell_TransparentForeground_UsesBackground()
	{
		var buffer = new CharacterBuffer(10, 10);
		var bg = new Color(255, 255, 0, 255);
		buffer.SetNarrowCell(0, 0, 'A', Color.White, bg);

		var cell = new Cell { Character = new System.Text.Rune('B'), Foreground = Color.Transparent, Background = bg };
		buffer.SetCell(0, 0, cell);
		Assert.Equal(bg, buffer.GetCell(0, 0).Foreground);
	}

	[Fact]
	public void SetCell_SemitransparentForeground_BlendsWithBackground()
	{
		var buffer = new CharacterBuffer(10, 10);
		var bg = new Color(0, 0, 255, 255);
		buffer.SetNarrowCell(0, 0, 'A', Color.White, bg);

		var cell = new Cell { Character = new System.Text.Rune('B'), Foreground = new Color(255, 0, 0, 128), Background = bg };
		buffer.SetCell(0, 0, cell);
		var fg = buffer.GetCell(0, 0).Foreground;

		Assert.InRange(fg.R, 120, 135);
		Assert.InRange(fg.B, 120, 135);
		Assert.Equal(255, fg.A);
	}

	// ── Background alpha still works ─────────────────────────────────────────

	[Fact]
	public void SetNarrowCell_TransparentBackground_UsesExistingBackground()
	{
		var buffer = new CharacterBuffer(10, 10);
		var existingBg = new Color(100, 150, 200, 255);
		buffer.SetNarrowCell(0, 0, 'A', Color.White, existingBg);
		buffer.SetNarrowCell(0, 0, 'B', Color.White, Color.Transparent);
		Assert.Equal(existingBg, buffer.GetCell(0, 0).Background);
	}

	[Fact]
	public void SetCell_TransparentBackground_UsesExistingBackground()
	{
		var buffer = new CharacterBuffer(10, 10);
		var existingBg = new Color(50, 100, 150, 255);
		buffer.SetNarrowCell(0, 0, 'A', Color.White, existingBg);

		var cell = new Cell { Character = new System.Text.Rune('B'), Foreground = Color.White, Background = Color.Transparent };
		buffer.SetCell(0, 0, cell);
		Assert.Equal(existingBg, buffer.GetCell(0, 0).Background);
	}

	// ── Foreground composites against gradient (Zone 2 scenario) ─────────────

	[Fact]
	public void SetNarrowCell_TransparentForeground_OverGradient_RevealsGradient()
	{
		var buffer = new CharacterBuffer(10, 10);
		var gradient = new Color(80, 20, 180, 255); // purple gradient
		// Simulate gradient background already in buffer
		buffer.SetNarrowCell(0, 0, ' ', Color.White, gradient);

		// Draw █ with fully transparent foreground — should look like gradient
		buffer.SetNarrowCell(0, 0, '█', Color.Transparent, Color.Transparent);
		var fg = buffer.GetCell(0, 0).Foreground;

		Assert.Equal(gradient, fg);
	}
}
