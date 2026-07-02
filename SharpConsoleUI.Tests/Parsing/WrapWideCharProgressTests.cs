// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

public class WrapWideCharProgressTests
{
	private static readonly Color Fg = Color.White;
	private static readonly Color Bg = Color.Black;

	// A 2-column CJK char at width 1 must not infinite-loop (#65: collapse squeezes a column to 1,
	// WrapCellLine stepped the hard-break back onto start -> zero progress -> UI freeze).
	[Fact]
	public void Cjk_AtWidth1_DoesNotHang_AndProgresses()
	{
		var t = Task.Run(() =>
			MarkupParser.ParseLines("中文测试", 1, Fg, Bg, out _));
		Assert.True(t.Wait(3000), "ParseLines with a CJK run at width 1 hung (WrapCellLine infinite loop)");
		var rows = t.Result;
		// Each wide char gets its own row (it can't be split); all four appear, one per row.
		Assert.Equal(4, rows.Count);
		var visible = rows.Select(r => string.Concat(
			r.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString())));
		Assert.Equal(new[] { "中", "文", "测", "试" }, visible.ToArray());
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void MixedCjkLatin_AtTinyWidths_Terminates(int width)
	{
		var t = Task.Run(() =>
			MarkupParser.ParseLines("a中b文c", width, Fg, Bg, out _));
		Assert.True(t.Wait(3000), $"ParseLines hung at width {width}");
		Assert.All(t.Result, r => Assert.True(r.Count > 0)); // no empty rows / progress made
	}

	[Fact]
	public void Emoji_AtWidth1_DoesNotHang()
	{
		var t = Task.Run(() => MarkupParser.ParseLines("📦📦", 1, Fg, Bg, out _));
		Assert.True(t.Wait(3000), "ParseLines with wide emoji at width 1 hung");
	}
}
