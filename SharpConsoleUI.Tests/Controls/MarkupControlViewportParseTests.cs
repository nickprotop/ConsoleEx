// SPDX: ConsoleEx — MarkupControl viewport-parse tests
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

// MarkupControl.GetParseCountForTest() reads a PROCESS-GLOBAL static parse counter. Tests that assert
// an exact parse delta (e.g. "second MeasureDOM did zero parses") are corrupted if another test class
// parses concurrently and bumps that counter between two reads. Serialize this class against the rest
// of the suite so the global counter is stable across our before/after reads.
[CollectionDefinition("MarkupParseCounter", DisableParallelization = true)]
public class MarkupParseCounterCollection { }

[Collection("MarkupParseCounter")]
public class MarkupControlViewportParseTests
{
	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new System.Drawing.Point(x, y);
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	private static List<string> Lines(int n, string text = "Hello world line")
	{
		var list = new List<string>(n);
		for (int i = 0; i < n; i++) list.Add($"{text} {i}");
		return list;
	}

	private static void Paint(MarkupControl c, int x, int y, int w, int h)
	{
		var buffer = new CharacterBuffer(x + w + 5, y + h + 5);
		var bounds = new LayoutRect(x, y, w, h);
		c.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void ParseCounter_StartsAtZero_AndIncrementsOnParse()
	{
		long before = MarkupControl.GetParseCountForTest();
		var control = new MarkupControl(Lines(3)) { Wrap = false };
		Paint(control, 0, 0, 40, 10);
		long after = MarkupControl.GetParseCountForTest();
		Assert.True(after > before, "painting once must perform at least one parse");
	}

	[Fact]
	public void ContentVersion_BumpsOnMutation()
	{
		var control = new MarkupControl(Lines(2));
		int v0 = control.GetContentVersionForTest();

		control.SetContent(Lines(3));
		int v1 = control.GetContentVersionForTest();
		Assert.True(v1 > v0, "SetContent must bump version");

		control.Text = "a\nb";
		int v2 = control.GetContentVersionForTest();
		Assert.True(v2 > v1, "Text setter must bump version");

		control.AppendLine("c");
		int v3 = control.GetContentVersionForTest();
		Assert.True(v3 > v2, "AppendLine must bump version");
	}

	[Fact]
	public void EnsureParsed_HitOnIdenticalInputs_DoesNotReparse()
	{
		var control = new MarkupControl(Lines(5)) { Wrap = false };
		long before = MarkupControl.GetParseCountForTest();
		control.EnsureParsedForTest(width: 40);
		long afterFirst = MarkupControl.GetParseCountForTest();
		Assert.True(afterFirst > before, "first EnsureParsed parses");
		control.EnsureParsedForTest(width: 40);
		long afterSecond = MarkupControl.GetParseCountForTest();
		Assert.Equal(afterFirst, afterSecond);
	}

	[Fact]
	public void EnsureParsed_MissOnWidthChange_Reparses()
	{
		var control = new MarkupControl(Lines(5)) { Wrap = true };
		control.EnsureParsedForTest(width: 40);
		long afterFirst = MarkupControl.GetParseCountForTest();
		control.EnsureParsedForTest(width: 20);
		long afterSecond = MarkupControl.GetParseCountForTest();
		Assert.True(afterSecond > afterFirst, "width change must re-parse");
	}

	[Fact]
	public void EnsureParsed_MissOnContentChange_Reparses()
	{
		var control = new MarkupControl(Lines(5)) { Wrap = false };
		control.EnsureParsedForTest(width: 40);
		long afterFirst = MarkupControl.GetParseCountForTest();
		control.SetContent(Lines(6));
		control.EnsureParsedForTest(width: 40);
		long afterSecond = MarkupControl.GetParseCountForTest();
		Assert.True(afterSecond > afterFirst, "content change must re-parse");
	}

	[Fact]
	public void EnsureParsed_DynamicLine_AlwaysReparses()
	{
		var control = new MarkupControl(new List<string> { "static line", "[spinner]" }) { Wrap = false };
		control.EnsureParsedForTest(width: 40);
		long afterFirst = MarkupControl.GetParseCountForTest();
		control.EnsureParsedForTest(width: 40);
		long afterSecond = MarkupControl.GetParseCountForTest();
		Assert.True(afterSecond > afterFirst, "a [spinner] line must re-parse every call");
	}

	[Fact]
	public void EnsureParsed_RowCounts_MatchWrappedRows()
	{
		var control = new MarkupControl(new List<string> { new string('x', 100), "short" }) { Wrap = true };
		var result = control.EnsureParsedForTest(width: 10);
		Assert.True(result.LineRowCounts[0] > 1, "long line should wrap to >1 rows");
		Assert.Equal(1, result.LineRowCounts[1]);
		int total = 0;
		foreach (var c in result.LineRowCounts) total += c;
		Assert.Equal(total, result.TotalRows);
	}

	[Fact]
	public void MeasureDOM_SecondCallWithSameInputs_DoesNotReparse()
	{
		var control = new MarkupControl(Lines(20)) { Wrap = true };
		var constraints = new LayoutConstraints(0, 40, 0, 100);

		var size1 = control.MeasureDOM(constraints);
		long afterFirst = MarkupControl.GetParseCountForTest();

		var size2 = control.MeasureDOM(constraints);
		long afterSecond = MarkupControl.GetParseCountForTest();

		Assert.Equal(size1.Width, size2.Width);
		Assert.Equal(size1.Height, size2.Height);
		Assert.Equal(afterFirst, afterSecond);
	}

	[Fact]
	public void MeasureDOM_HeightMatchesWrappedRowCount()
	{
		var control = new MarkupControl(new List<string> { new string('y', 80) }) { Wrap = true };
		var constraints = new LayoutConstraints(0, 10, 0, 100);
		var size = control.MeasureDOM(constraints);
		Assert.True(size.Height >= 8, $"expected wrapped height >=8, got {size.Height}");
	}

	[Fact]
	public void MeasureThenPaint_ShareCache_NoSecondParse()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 60, Height = 20 };
		var control = new MarkupControl(Lines(10)) { Wrap = true };
		window.AddControl(control);

		window.RenderAndGetVisibleContent();
		long afterFirst = MarkupControl.GetParseCountForTest();

		window.Invalidate(true);
		window.RenderAndGetVisibleContent();
		long afterSecond = MarkupControl.GetParseCountForTest();

		Assert.Equal(afterFirst, afterSecond);
	}

	[Fact]
	public void PaintDOM_RepeatedPaint_NoReparse()
	{
		var control = new MarkupControl(Lines(2000)) { Wrap = false };
		Paint(control, 0, 0, 40, 20);
		long afterFirst = MarkupControl.GetParseCountForTest();

		Paint(control, 0, 0, 40, 20);
		long afterSecond = MarkupControl.GetParseCountForTest();

		Assert.Equal(afterFirst, afterSecond);
	}

	[Fact]
	public void Selection_AcrossScroll_CopiesFullText()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 60, Height = 10 };
		var control = new MarkupControl(Lines(200)) { Wrap = false, EnableSelection = true };
		window.AddControl(control);
		window.RenderAndGetVisibleContent();

		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(10, 5, MouseFlags.Button1Dragged));

		string text = control.GetSelectedText();
		Assert.False(string.IsNullOrEmpty(text));
		Assert.Contains("Hello world line 0", text);
	}

	[Fact]
	public void Selection_OffscreenCopy_DoesNotParseWholeBuffer()
	{
		var control = new MarkupControl(Lines(5000)) { Wrap = false, EnableSelection = true };
		Paint(control, 0, 0, 40, 10);

		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		control.ProcessMouseEvent(Mouse(5, 3, MouseFlags.Button1Dragged));

		long before = MarkupControl.GetParseCountForTest();
		string text = control.GetSelectedText();
		long parses = MarkupControl.GetParseCountForTest() - before;

		Assert.False(string.IsNullOrEmpty(text));
		Assert.True(parses < 100, $"copy of a small selection must not re-parse the buffer; parses={parses}");
	}

	// The #42 regression guard. A fast drag-select over a large CJK buffer must NOT re-parse the content
	// per move (which froze the UI). With the parse cache, every drag frame is a cache hit, so the parse
	// count across the whole burst stays ~constant instead of O(moves * content).
	[Fact]
	public void FastDragSelect_DoesNotReparsePerMove()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };

		// Large CJK-ish content (wide runes) — the #42 shape.
		var content = new List<string>();
		var sb = new StringBuilder();
		while (sb.Length < 40) sb.Append("你好世界测试内容");
		string wide = sb.ToString(0, 40);
		for (int i = 0; i < 2000; i++) content.Add(wide);

		var control = new MarkupControl(content) { Wrap = true, EnableSelection = true };
		window.AddControl(control);
		window.RenderAndGetVisibleContent(); // warm: one parse of the buffer

		control.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		long before = MarkupControl.GetParseCountForTest();

		// 40 rapid drag moves, each followed by a real render (the dirty-frame path).
		for (int i = 1; i <= 40; i++)
		{
			control.ProcessMouseEvent(Mouse(i % 70, i % 25, MouseFlags.Button1Dragged));
			window.RenderAndGetVisibleContent();
		}

		long perMoveParses = MarkupControl.GetParseCountForTest() - before;

		// The whole burst must parse essentially nothing (cache hits). O(content) per move would be
		// ~40 * 2000 = 80000. Allow a tiny slack for any first-frame re-key.
		Assert.True(perMoveParses < 200,
			$"fast drag re-parsed {perMoveParses} lines across 40 moves — cache not holding (expected <200, O(content)=~80000)");

		Assert.True(control.HasSelection, "the drag should have produced a selection");
	}
}
