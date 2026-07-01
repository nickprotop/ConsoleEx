// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

public class InternationalWrappingTests
{
	// Visible text of a wrapped row (skip wide-continuation cells so each glyph appears once).
	private static List<string> Wrap(string s, int width)
	{
		var rows = MarkupParser.ParseLines(s, width, Color.White, Color.Black, out _);
		return rows.Select(r => string.Concat(
			r.Where(c => !c.IsWideContinuation).Select(c => c.Character.ToString()))).ToList();
	}

	// Checks that no ASCII/Latin word token (letters, digits, underscore — NOT CJK ideographs, which
	// break freely by design) is split across a row boundary. Detects the specific class of bugs where
	// "length" becomes "len" | "gth" or "4204" becomes "42" | "04".
	private static void AssertNoMidTokenSplit(string original, List<string> rows)
	{
		for (int i = 1; i < rows.Count; i++)
		{
			string prev = rows[i - 1].TrimEnd();
			string cur = rows[i].TrimStart();
			if (prev.Length == 0 || cur.Length == 0) continue;

			// Only check ASCII word chars (a-z, A-Z, 0-9, _). CJK ideographs break freely by design.
			static bool IsAsciiWordChar(char c) =>
				(c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';

			string tailWord = new string(prev.Reverse().TakeWhile(IsAsciiWordChar).Reverse().ToArray());
			string headWord = new string(cur.TakeWhile(IsAsciiWordChar).ToArray());

			if (tailWord.Length == 0 || headWord.Length == 0) continue;

			// If tail + head form a substring of the original, then the break was mid-token.
			string candidate = tailWord + headWord;
			Assert.False(original.Contains(candidate),
				$"ASCII token '{candidate}' was split across rows: '{prev}' | '{cur}' (row {i - 1}->{i})");
		}
	}

	[Fact]
	public void EnglishWordsAndNumbers_NotSplitMidToken_inMixedCjkJson()
	{
		// changlv #63 follow-up: JSON with embedded CJK; English words and numbers must stay whole.
		string s = "{\"name\":\"基本 Git 指令\",\"length\":4204,\"last_write_time\":\"2026-05-27\"}";
		var rows = Wrap(s, 21);
		AssertNoMidTokenSplit(s, rows);
		// Spot-check the specific tokens from the bug report are intact on some row.
		Assert.Contains(rows, r => r.Contains("length"));
		Assert.Contains(rows, r => r.Contains("4204"));
		Assert.Contains(rows, r => r.Contains("write_time") || rows.Any(x => x.Contains("last_write_time")));
	}

	[Fact]
	public void PlainEnglishSentence_BreaksAtSpaces()
	{
		string s = "the quick brown fox jumps";
		var rows = Wrap(s, 10);
		AssertNoMidTokenSplit(s, rows);
		Assert.All(rows, r => Assert.DoesNotContain("  ", r)); // no doubled spaces from bad trim
	}

	[Fact]
	public void Float_NotSplit()
	{
		// Width 14 is enough for "value=3.14159" (13 chars) — the float must stay whole on one row.
		string s = "value=3.14159 ok";
		var rows = Wrap(s, 14);
		AssertNoMidTokenSplit(s, rows);
		Assert.Contains(rows, r => r.Contains("3.14159"));
	}

	[Fact]
	public void PureCjk_BreaksPerIdeographAndFills()
	{
		var rows = Wrap("中文测试内容示例文本", 4);
		// width 4 = 2 ideographs per row; each row should be full (2 wide chars) until the last.
		Assert.True(rows.Count >= 2);
		Assert.All(rows.Take(rows.Count - 1), r => Assert.Equal(2, r.Length));
	}

	[Fact]
	public void NoWrap_SingleLine()
	{
		string s = "a very long line that exceeds width";
		// Width 8 fits the longest word ("exceeds" = 7 chars) so every break is at a space boundary.
		var rows = Wrap(s, 8);
		// multiple rows produced; assert tokens still whole
		Assert.True(rows.Count >= 4);
		AssertNoMidTokenSplit(s, rows);
	}

	[Fact]
	public void LongUnbreakableRun_StillProgresses_HardBreak()
	{
		// A single very long token with no break opportunity must still wrap (hard break), not loop.
		var rows = Wrap(new string('x', 30), 8);
		Assert.True(rows.Count >= 4);              // forced progress
		Assert.All(rows, r => Assert.True(r.Length <= 8));
	}

	[Fact]
	public void IsoDate_NotSplitAtInternalHyphen()
	{
		// changlv #63 follow-up: an ISO date's internal hyphens are digit-separators, not prose hyphens,
		// so "2026-05-27" must NOT break at "2026-05-" | "27". A hyphen between digits binds like the ':'
		// in a time already does (03:41:30 stays whole). Widths must be wide enough for the date to fit
		// ("date=2026-05-27" is 15 cols) — narrower than the token forces a legitimate hard break.
		foreach (int w in new[] { 16, 20, 26 })
		{
			var rows = Wrap("date=2026-05-27 end", w);
			AssertNoMidTokenSplit("date=2026-05-27 end", rows);
			// The full date appears intact on exactly one row.
			Assert.Contains(rows, r => r.Contains("2026-05-27"));
		}
	}

	[Fact]
	public void ProseHyphenatedWord_StillBreaksAtHyphen()
	{
		// Regression: a hyphen between LETTERS (prose) must STILL be a break opportunity — the date fix
		// only binds hyphen-before-DIGIT. "state-of-the-art" may break after a hyphen.
		var rows = Wrap("state-of-the-art design", 8);
		Assert.True(rows.Count >= 2);
		Assert.All(rows, r => Assert.True(r.Length <= 8, "prose hyphenation must wrap within width"));
	}

	[Fact]
	public void ChangvlJsonResult_DateAndNumbersIntact()
	{
		// changlv's exact reported string (#63). The date must stay whole and no number/word splits.
		string s = " * result: {\"entries\":[{\"name\":\"Git base usage.md\",\"type\":\"File\",\"length\":4204,"
			+ "\"last_write_time\":\"2026-05-27T03:41:30.3094599+08:00\",\"is_text\":true}]}";
		foreach (int w in new[] { 40, 60, 80 })
		{
			var rows = Wrap(s, w);
			AssertNoMidTokenSplit(s, rows);
			Assert.Contains(rows, r => r.Contains("4204"));
			Assert.Contains(rows, r => r.Contains("2026-05-27T03:41:30.3094599+08:00"));
		}
	}
}
