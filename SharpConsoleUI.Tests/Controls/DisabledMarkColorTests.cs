// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// A disabled control must dim its selected mark to the (grey) disabled foreground — not paint it
/// with the vivid CheckmarkColor, and not fall back to the bright cyan Color.DarkSlateGray1. Regression
/// guard: this bug was invisible in the demo (the mark rendered bright cyan on a disabled+selected radio).
/// </summary>
public class DisabledMarkColorTests
{
	private static readonly Color Cyan = new(135, 255, 255); // Color.DarkSlateGray1 / Color.Cyan1

	[Fact]
	public void DisabledSelectedRadio_MarkIsDimmed_NotCyan()
	{
		var group = new RadioGroup<string>();
		var radio = new RadioControl<string>(group, "a", "Locked");
		group.SelectedValue = "a";
		radio.IsEnabled = false;
		Assert.True(radio.Checked);

		var buf = new CharacterBuffer(40, 1);
		radio.PaintDOM(buf, new LayoutRect(0, 0, 40, 1), new LayoutRect(0, 0, 40, 1), Color.White, Color.Black);

		var markFg = FindGlyphFg(buf, '●');
		Assert.NotEqual(Cyan, markFg);
		Assert.NotEqual(Color.Cyan1, markFg);
	}

	[Fact]
	public void DisabledCheckedCheckbox_MarkIsDimmed_NotCyan()
	{
		var cb = new CheckboxControl("Locked", true) { IsEnabled = false };
		var buf = new CharacterBuffer(40, 1);
		cb.PaintDOM(buf, new LayoutRect(0, 0, 40, 1), new LayoutRect(0, 0, 40, 1), Color.White, Color.Black);

		// the checkmark glyph (default ✓/X) — assert whatever mark cell isn't cyan
		for (int x = 0; x < 40; x++)
		{
			var c = buf.GetCell(x, 0);
			var ch = c.Character.ToString();
			if (ch != " " && ch != "[" && ch != "]" && ch != "\0")
			{
				Assert.NotEqual(Cyan, c.Foreground);
				Assert.NotEqual(Color.Cyan1, c.Foreground);
			}
		}
	}

	private static Color FindGlyphFg(CharacterBuffer buf, char glyph)
	{
		for (int x = 0; x < buf.Width; x++)
		{
			var c = buf.GetCell(x, 0);
			if (c.Character.ToString() == glyph.ToString()) return c.Foreground;
		}
		Assert.Fail($"glyph '{glyph}' not painted");
		return default;
	}
}
