// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for GitHub issue #42: entering CJK / wide characters into a
/// MultilineEditControl with Wrap or WrapWords froze the UI (the wrap calculation
/// mixed char-index and display-width and threw out-of-range during paint) and
/// produced segments wider than the viewport. The wrap boundary must be computed
/// in display columns, never raw char counts.
/// </summary>
public class MultilineEditUnicodeTests
{
	private static MultilineEditControl CreateEditing(string content, WrapMode wrapMode, int viewportWidth)
	{
		var control = new MultilineEditControl
		{
			WrapMode = wrapMode,
			Content = content,
			IsEditing = true
		};
		return control;
	}

	[Fact]
	public void WrapWords_ChineseLongerThanViewport_DoesNotThrowOrHang()
	{
		var edit = CreateEditing("中文测试内容", WrapMode.WrapWords, viewportWidth: 8);
		// 6 CJK chars = 12 display columns; string.Length = 6. The old code computed
		// the break window with char counts and indexed/substring'd past the end → throw.
		var ex = Record.Exception(() => edit.GetWrappedSegmentsForTest(8));
		Assert.Null(ex); // before fix: IndexOutOfRange / ArgumentOutOfRange (the freeze)
	}

	[Fact]
	public void WrapWords_LoneHighSurrogate_MidEmojiTyping_DoesNotThrow()
	{
		// Simulates the transient state while a user is typing 📦 (U+1F4E6): the line momentarily
		// contains just the high surrogate before the low surrogate is inserted. This flows through
		// TakeColumns, which used to call Rune.GetRuneAt and throw ArgumentException, crashing the app.
		var edit = CreateEditing("abc\uD83D", WrapMode.WrapWords, viewportWidth: 8);
		var ex = Record.Exception(() => edit.GetWrappedSegmentsForTest(8));
		Assert.Null(ex); // before fix: ArgumentException from Rune.GetRuneAt → unhandled TUI crash
	}

	[Theory]
	[InlineData("中文测试内容", 8)]
	[InlineData("abc中文def", 6)]
	[InlineData("📦🚀🎉👍", 4)]
	[InlineData("Привет мир", 6)]                       // Cyrillic: single-width, multi-byte (@YotPhiligan)
	[InlineData("Привет мир этот текст", 6)]            // longer Cyrillic, multiple wraps
	[InlineData("ある日本語のテキスト", 6)]              // Japanese (Hiragana + Kanji, all 2-wide)
	[InlineData("한국어 텍스트입니다", 6)]               // Korean Hangul (2-wide)
	[InlineData("e\u0301fe\u0301g", 3)] // base + combining acute (0-width) must not push wrap
	[InlineData("⚙️⚙️⚙️", 4)]           // VS16-widened glyphs (each 2 cols)
	[InlineData("abc中文Привет\U0001F4E6def", 5)]        // mixed multi-script (Latin+CJK+Cyrillic+emoji)
	[InlineData("ab中cd", 3)]                            // wide char at viewport edge (boundary)
	public void Wrap_EveryWrappedSegment_FitsViewportWidth(string content, int viewportWidth)
	{
		var edit = CreateEditing(content, WrapMode.Wrap, viewportWidth);
		var segments = edit.GetWrappedSegmentsForTest(viewportWidth);

		foreach (var seg in segments)
		{
			int w = UnicodeWidth.GetStringWidth(seg.Text);
			Assert.True(w <= viewportWidth,
				$"segment '{seg.Text}' = {w} cols > {viewportWidth}");
		}
	}

	[Theory]
	[InlineData("中文测试内容", 8)]
	[InlineData("abc中文def", 6)]
	[InlineData("📦🚀🎉👍", 4)]
	[InlineData("Привет мир этот текст", 6)]            // Cyrillic
	[InlineData("e\u0301fe\u0301g", 3)] // base + combining acute (NFD): round-trip must preserve combiners
	[InlineData("⚙️⚙️⚙️", 4)]                          // VS16-widened
	[InlineData("abc中文Привет\U0001F4E6def", 5)]        // mixed multi-script
	[InlineData("ab中cd", 3)]                            // wide char at edge
	public void Wrap_SegmentsReconstructOriginalContent(string content, int viewportWidth)
	{
		var edit = CreateEditing(content, WrapMode.Wrap, viewportWidth);
		var segments = edit.GetWrappedSegmentsForTest(viewportWidth);

		string reconstructed = string.Concat(segments.Select(s => s.Text));
		Assert.Equal(content, reconstructed);
	}

	[Fact]
	public void Wrap_Ascii_Unchanged_Regression()
	{
		var edit = CreateEditing("abcdefghij", WrapMode.Wrap, viewportWidth: 5);
		var segs = edit.GetWrappedSegmentsForTest(5);

		Assert.Equal(2, segs.Count);
		Assert.Equal("abcde", segs[0].Text);
		Assert.Equal("fghij", segs[1].Text);
	}

	[Fact]
	public void WrapWords_Ascii_Unchanged_Regression()
	{
		// "hello world foo" with width 8: "hello " breaks at the space, then "world ", then "foo".
		var edit = CreateEditing("hello world foo", WrapMode.WrapWords, viewportWidth: 8);
		var segs = edit.GetWrappedSegmentsForTest(8);

		string reconstructed = string.Concat(segs.Select(s => s.Text));
		Assert.Equal("hello world foo", reconstructed);
		foreach (var seg in segs)
			Assert.True(UnicodeWidth.GetStringWidth(seg.Text) <= 8,
				$"segment '{seg.Text}' exceeds width 8");
	}

	// --- Issue #42: GetMaxLineLength must return DISPLAY WIDTH (columns), not char count ---

	[Fact]
	public void GetMaxLineLength_ReturnsDisplayWidth_NotCharCount()
	{
		var edit = CreateEditing("中文测试", WrapMode.NoWrap, viewportWidth: 20);
		// 4 CJK chars = string.Length 4, but 8 display columns.
		Assert.Equal(8, edit.GetMaxLineLengthForTest());
	}

	[Fact]
	public void GetMaxLineLength_Ascii_Unchanged()
	{
		var edit = CreateEditing("abcdef", WrapMode.NoWrap, viewportWidth: 20);
		Assert.Equal(6, edit.GetMaxLineLengthForTest());
	}

	[Fact]
	public void GetMaxLineLength_MultipleLines_ReturnsWidestInColumns()
	{
		// Line 1: "ab" = 2 cols, 2 chars. Line 2: "中文" = 4 cols, 2 chars.
		// char-count max would be 2 (tie); display-width max is 4.
		var edit = CreateEditing("ab\n中文", WrapMode.NoWrap, viewportWidth: 20);
		Assert.Equal(4, edit.GetMaxLineLengthForTest());
	}

	// --- Issue #42 (spec 4c): horizontal scrollbar appears by DISPLAY WIDTH, not char count ---

	[Fact]
	public void HorizontalScrollbar_TriggeredByDisplayWidth_NotCharCount()
	{
		// "中文测" = 3 CJK chars (string.Length 3) but 6 display columns.
		// The NoWrap Auto-scrollbar trigger is: GetMaxLineLength() > effectiveWidth.
		// With a viewport of 4 columns:
		//   - char count (3) <= viewport (4)  -> a char-count trigger would NOT fire
		//   - display width (6) > viewport (4) -> the display-width trigger DOES fire
		// This proves the scrollbar is driven by display columns, not raw char count.
		const int viewportWidth = 4;
		var edit = CreateEditing("中文测", WrapMode.NoWrap, viewportWidth);

		int displayWidth = edit.GetMaxLineLengthForTest();
		int charCount = "中文测".Length;

		Assert.Equal(6, displayWidth);
		Assert.Equal(3, charCount);

		// Discriminating condition: a char-count trigger would stay silent, the width trigger fires.
		Assert.True(charCount <= viewportWidth, $"char count {charCount} should fit viewport {viewportWidth}");
		Assert.True(displayWidth > viewportWidth, $"display width {displayWidth} must exceed viewport {viewportWidth} to trigger the scrollbar");
	}

	// --- Issue #42: NoWrap horizontal slice must treat the offset as a COLUMN, not a char index ---

	[Fact]
	public void HorizontalScroll_SlicesByColumn_NotCharIndex()
	{
		// "中文测试ABCD": 4 CJK chars (8 cols) + 4 ASCII (4 cols) = 12 cols, 8 chars.
		// Scroll 8 columns: should skip exactly the 4 CJK chars and show "ABCD".
		// Old (char-index) behavior would Substring(8) → past end of an 8-char string → blank.
		var edit = CreateEditing("中文测试ABCD", WrapMode.NoWrap, viewportWidth: 10);
		edit.SetHorizontalScrollOffsetForTest(8);

		var lines = ContainerTestHelpers.RenderToLines(edit, width: 14, height: 3);
		string joined = string.Join("\n", lines);

		Assert.Contains("ABCD", joined);
		Assert.DoesNotContain("中", joined);
	}

	[Fact]
	public void HorizontalScroll_StraddlingWideChar_StartsOnColumnBoundary()
	{
		// "中文ABCD": cols 0-1 = 中, cols 2-3 = 文, cols 4-7 = ABCD.
		// Scroll 1 column: the offset lands inside 中 (its 2nd column). We skip the
		// straddled wide char so the slice starts on a column boundary at 文.
		var edit = CreateEditing("中文ABCD", WrapMode.NoWrap, viewportWidth: 10);
		edit.SetHorizontalScrollOffsetForTest(1);

		var lines = ContainerTestHelpers.RenderToLines(edit, width: 14, height: 3);
		string joined = string.Join("\n", lines);

		// 中 is fully scrolled off; 文 and the ASCII tail remain, aligned to columns.
		Assert.DoesNotContain("中", joined);
		Assert.Contains("文", joined);
		Assert.Contains("ABCD", joined);
	}

	// --- Issue #42: cursor-follow horizontal scroll must compare in COLUMNS, not char index ---

	[Fact]
	public void EnsureCursorVisible_CjkCursorPastViewport_ScrollsByColumn()
	{
		// "中文测试内容" = 6 CJK chars (char index range 0..6), 12 display columns.
		// Viewport = 8 columns. Cursor at char index 6 (end) = display column 12.
		// Column 12 is past the viewport, so the offset must scroll right to bring it in.
		const int effectiveWidth = 8;
		var edit = CreateEditing("中文测试内容", WrapMode.NoWrap, viewportWidth: effectiveWidth);
		edit.SetHorizontalScrollOffsetForTest(0);
		edit.SetCursorForTest(charX: 6, lineY: 0);

		edit.EnsureCursorVisibleForTest(effectiveWidth);

		int offset = edit.GetHorizontalScrollOffsetForTest();
		int cursorColumn = UnicodeWidth.CharOffsetToColumn("中文测试内容", 6); // = 12

		// With the fix the cursor's COLUMN lands inside the viewport window.
		// (Old char-vs-column compare: 6 >= 0+8 is false -> offset stays 0 -> column 12 invisible -> fails.)
		Assert.True(cursorColumn >= offset && cursorColumn < offset + effectiveWidth,
			$"cursor column {cursorColumn} not within [{offset}, {offset + effectiveWidth}); offset={offset}");
		Assert.Equal(5, offset); // 12 - 8 + 1
	}

	[Fact]
	public void EnsureCursorVisible_Ascii_Unchanged()
	{
		// Pure ASCII: char index == display column, so behavior is identical to before the fix.
		const int effectiveWidth = 8;
		var edit = CreateEditing("abcdefghijklmnop", WrapMode.NoWrap, viewportWidth: effectiveWidth);
		edit.SetHorizontalScrollOffsetForTest(0);
		edit.SetCursorForTest(charX: 12, lineY: 0);

		edit.EnsureCursorVisibleForTest(effectiveWidth);

		Assert.Equal(5, edit.GetHorizontalScrollOffsetForTest()); // 12 - 8 + 1
	}

	// --- GetMaxLineLength across more scripts (width is the single source of truth) ---

	[Fact]
	public void GetMaxLineLength_Cyrillic_EqualsCharCount()
	{
		// Cyrillic is single-width: display columns == char count (10 chars, 10 cols).
		const string s = "Привет мир";
		var edit = CreateEditing(s, WrapMode.NoWrap, viewportWidth: 30);
		Assert.Equal(UnicodeWidth.GetStringWidth(s), edit.GetMaxLineLengthForTest());
		Assert.Equal(s.Length, edit.GetMaxLineLengthForTest()); // 1 col per Cyrillic char
	}

	[Fact]
	public void GetMaxLineLength_MixedMultiScript_EqualsDisplayWidth()
	{
		// Latin + CJK + Cyrillic + emoji on one line: max length must equal display width.
		const string s = "abc中文Привет\U0001F4E6def";
		var edit = CreateEditing(s, WrapMode.NoWrap, viewportWidth: 40);
		Assert.Equal(UnicodeWidth.GetStringWidth(s), edit.GetMaxLineLengthForTest()); // = 18
	}

	// --- Boundary: a wide char straddling the viewport edge must wrap whole, never split ---

	[Fact]
	public void Wrap_WideCharAtViewportEdge_MovesToNextSegment_NeverSplit()
	{
		// "ab中cd" width 3: a(1)+b(1)=2 cols; 中 is 2 cols → 2+2=4 > 3, so 中 must wrap
		// to the next segment. No segment may exceed 3 cols and 中 must stay intact.
		const int width = 3;
		var edit = CreateEditing("ab中cd", WrapMode.Wrap, width);
		var segs = edit.GetWrappedSegmentsForTest(width);

		// Reconstructs original, no segment over the viewport, wide char never split.
		Assert.Equal("ab中cd", string.Concat(segs.Select(s => s.Text)));
		foreach (var seg in segs)
			Assert.True(UnicodeWidth.GetStringWidth(seg.Text) <= width,
				$"segment '{seg.Text}' exceeds width {width}");

		// 中 must appear whole inside exactly one segment (not at the tail of "ab中").
		Assert.Contains(segs, s => s.Text.Contains('中'));
		Assert.DoesNotContain(segs, s => s.Text == "ab中");
		// The first segment is just "ab" (中 didn't fit).
		Assert.Equal("ab", segs[0].Text);
	}

	// --- Mixed multi-script line: every segment width-correct, full reconstruction ---

	[Fact]
	public void Wrap_MixedMultiScript_AllSegmentsFit_AndReconstruct()
	{
		const string s = "abc中文Привет\U0001F4E6def";
		const int width = 5;
		var edit = CreateEditing(s, WrapMode.Wrap, width);
		var segs = edit.GetWrappedSegmentsForTest(width);

		Assert.Equal(s, string.Concat(segs.Select(seg => seg.Text)));
		foreach (var seg in segs)
			Assert.True(UnicodeWidth.GetStringWidth(seg.Text) <= width,
				$"segment '{seg.Text}' = {UnicodeWidth.GetStringWidth(seg.Text)} > {width}");
	}

	// --- Issue #42 literal string under WrapWords + horizontal scroll: no hang, widths correct ---

	[Fact]
	public void WrapWords_Issue42Cjk_WithHorizontalScroll_NoHang_WidthsCorrect()
	{
		// The exact issue-#42 string in a narrow WrapWords editor that also has a non-zero
		// horizontal scroll offset. Before the fix this combination threw/hung during paint.
		const int width = 4;
		var edit = CreateEditing("中文测试内容", WrapMode.WrapWords, width);
		edit.SetHorizontalScrollOffsetForTest(2);

		var ex = Record.Exception(() =>
		{
			var segs = edit.GetWrappedSegmentsForTest(width);
			foreach (var seg in segs)
				Assert.True(UnicodeWidth.GetStringWidth(seg.Text) <= width,
					$"segment '{seg.Text}' exceeds width {width}");

			// Also render with the scroll offset applied (the original freeze was in paint).
			var lines = ContainerTestHelpers.RenderToLines(edit, width: width + 2, height: 6);
			Assert.NotNull(lines);
		});

		Assert.Null(ex);
	}
}
