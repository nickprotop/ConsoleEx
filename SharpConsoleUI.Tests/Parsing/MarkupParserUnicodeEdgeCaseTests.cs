using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using System.Text;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing
{
	/// <summary>
	/// Comprehensive Unicode edge case tests for MarkupParser.
	/// Covers emoji ZWJ sequences, skin tones, keycaps, flags,
	/// combining mark chains, Mc/Mn distinction, invisible chars,
	/// truncation boundaries, and line wrapping.
	/// </summary>
	public class MarkupParserUnicodeEdgeCaseTests
	{
		#region Emoji ZWJ Sequences

		[Fact]
		public void Parse_FamilyEmoji_ZWJAttachesAsCombiner()
		{
			// 👨‍👩‍👦 = 👨(2 cells) + ZWJ(combiner) + 👩(2 cells) + ZWJ(combiner) + 👦(2 cells) = 6 cells
			var cells = MarkupParser.Parse(
				"\U0001F468\u200D\U0001F469\u200D\U0001F466",
				Color.White, Color.Black);

			// 👨 at cell 0-1, ZWJ attached to 👨
			Assert.Equal(new Rune(0x1F468), cells[0].Character);
			Assert.NotNull(cells[0].Combiners);
			Assert.Contains("\u200D", cells[0].Combiners);
			Assert.True(cells[1].IsWideContinuation);

			// 👩 at cell 2-3, ZWJ attached to 👩
			Assert.Equal(new Rune(0x1F469), cells[2].Character);
			Assert.NotNull(cells[2].Combiners);
			Assert.Contains("\u200D", cells[2].Combiners);
			Assert.True(cells[3].IsWideContinuation);

			// 👦 at cell 4-5
			Assert.Equal(new Rune(0x1F466), cells[4].Character);
			Assert.True(cells[5].IsWideContinuation);

			Assert.Equal(6, cells.Count);
		}

		[Fact]
		public void StripLength_FamilyEmoji_Returns6()
		{
			// 👨(2) + ZWJ(0) + 👩(2) + ZWJ(0) + 👦(2) = 6
			Assert.Equal(6, MarkupParser.StripLength(
				"\U0001F468\u200D\U0001F469\u200D\U0001F466"));
		}

		[Fact]
		public void Parse_WomanTechnologist_ZWJAttachesCorrectly()
		{
			// 👩‍💻 = 👩(2) + ZWJ(combiner) + 💻(2) = 4 cells
			var cells = MarkupParser.Parse(
				"\U0001F469\u200D\U0001F4BB",
				Color.White, Color.Black);

			Assert.Equal(4, cells.Count);
			Assert.NotNull(cells[0].Combiners); // ZWJ on 👩
			Assert.Equal(new Rune(0x1F4BB), cells[2].Character);
		}

		[Fact]
		public void Parse_FourPersonFamily_CorrectLayout()
		{
			// 👨‍👩‍👧‍👦 = 8 cells
			var cells = MarkupParser.Parse(
				"\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466",
				Color.White, Color.Black);

			Assert.Equal(8, cells.Count);
			// All even positions are base emoji, odd are continuations
			for (int i = 0; i < 8; i += 2)
			{
				Assert.False(cells[i].IsWideContinuation);
				Assert.True(cells[i + 1].IsWideContinuation);
			}
		}

		#endregion

		#region Skin Tone Modifiers

		[Fact]
		public void Parse_EmojiWithSkinTone_ModifierAttachesAsCombiner()
		{
			// 👋🏻 = 👋(2 cells) + 🏻(zero-width, combiner) = 2 cells
			// Skin tone modifiers are category Sk (ModifierSymbol), Wcwidth returns 0
			var cells = MarkupParser.Parse(
				"\U0001F44B\U0001F3FB",
				Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune(0x1F44B), cells[0].Character);
			Assert.NotNull(cells[0].Combiners); // 🏻 attached as combiner
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void StripLength_EmojiWithSkinTone_Returns2()
		{
			// 👍(2) + 🏽(0, zero-width modifier) = 2
			Assert.Equal(2, MarkupParser.StripLength("\U0001F44D\U0001F3FD"));
		}

		[Fact]
		public void Parse_AllFiveSkinTones_SameCellCount()
		{
			// All skin tone modifiers are zero-width, so always 2 cells
			int[] tones = { 0x1F3FB, 0x1F3FC, 0x1F3FD, 0x1F3FE, 0x1F3FF };
			foreach (int tone in tones)
			{
				var cells = MarkupParser.Parse(
					$"\U0001F44D{char.ConvertFromUtf32(tone)}",
					Color.White, Color.Black);
				Assert.Equal(2, cells.Count);
				Assert.NotNull(cells[0].Combiners); // skin tone attached
			}
		}

		#endregion

		#region Keycap Sequences

		[Fact]
		public void Parse_KeycapSequence_FE0FAndKeycapAreCombiners()
		{
			// 1️⃣ = '1'(1 cell) + FE0F(VS16 widens to 2) + U+20E3(combiner) = 2 cells
			var cells = MarkupParser.Parse("1\uFE0F\u20E3", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('1'), cells[0].Character);
			Assert.Equal("\uFE0F\u20E3", cells[0].Combiners);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void StripLength_KeycapSequence_Returns2()
		{
			// 1️⃣ = '1'(1) + FE0F(+1 VS16 widening) + U+20E3(0) = 2
			Assert.Equal(2, MarkupParser.StripLength("1\uFE0F\u20E3"));
		}

		[Fact]
		public void Parse_HashKeycap_TwoCells()
		{
			// #️⃣ = '#'(1) + FE0F(VS16 widens to 2) + U+20E3(0) = 2 cells
			var cells = MarkupParser.Parse("#\uFE0F\u20E3", Color.White, Color.Black);
			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('#'), cells[0].Character);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void Parse_MultipleKeycaps_CorrectCellCount()
		{
			// "1️⃣2️⃣" = 1(2) + 2(2) = 4 cells (VS16 widens each keycap base)
			var cells = MarkupParser.Parse(
				"1\uFE0F\u20E32\uFE0F\u20E3",
				Color.White, Color.Black);

			Assert.Equal(4, cells.Count);
			Assert.Equal(new Rune('1'), cells[0].Character);
			Assert.True(cells[1].IsWideContinuation);
			Assert.Equal(new Rune('2'), cells[2].Character);
			Assert.True(cells[3].IsWideContinuation);
		}

		#endregion

		#region Flag Emoji (Regional Indicators)

		[Fact]
		public void Parse_USFlag_TwoRegionalIndicators()
		{
			// 🇺🇸 — Regional U(1) + Regional S(1) = 2 cells (each narrow per Wcwidth)
			var cells = MarkupParser.Parse(
				"\U0001F1FA\U0001F1F8",
				Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune(0x1F1FA), cells[0].Character);
			Assert.Equal(new Rune(0x1F1F8), cells[1].Character);
		}

		[Fact]
		public void StripLength_Flag_MatchesParseCellCount()
		{
			string flag = "\U0001F1EF\U0001F1F5"; // 🇯🇵
			int strip = MarkupParser.StripLength(flag);
			var cells = MarkupParser.Parse(flag, Color.White, Color.Black);
			Assert.Equal(cells.Count, strip);
		}

		#endregion

		#region Variation Selectors — Advanced

		[Fact]
		public void Parse_EmojiWithFE0F_AttachesToBaseNotContinuation()
		{
			// ⚡(wide, 2 cells) + FE0F → FE0F on base cell (cell[0]), not continuation
			var cells = MarkupParser.Parse("\u26A1\uFE0F", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.NotNull(cells[0].Combiners);
			Assert.Contains("\uFE0F", cells[0].Combiners);
			Assert.Null(cells[1].Combiners);
		}

		[Fact]
		public void Parse_NarrowCharWithFE0F_WidenedToTwoCells()
		{
			// ✈ (U+2708, narrow 1 cell) + FE0F (VS16 widens to 2) = 2 cells
			var cells = MarkupParser.Parse("\u2708\uFE0F", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('\u2708'), cells[0].Character);
			Assert.Contains("\uFE0F", cells[0].Combiners);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void Parse_DuplicateVariationSelectors_BothAttach()
		{
			// ⚡ + FE0F + FE0F → both attach as combiners
			var cells = MarkupParser.Parse("\u26A1\uFE0F\uFE0F", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal("\uFE0F\uFE0F", cells[0].Combiners);
		}

		[Fact]
		public void Parse_TextPresentationFE0E_AttachesAsCombiner()
		{
			// ✏ (U+270F) + FE0E → 1 cell with combiner
			var cells = MarkupParser.Parse("\u270F\uFE0E", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Contains("\uFE0E", cells[0].Combiners);
		}

		#endregion

		#region Combining Mark Chains

		[Fact]
		public void Parse_FourCombiningMarks_AllAttachToBase()
		{
			// A + U+0300 + U+0301 + U+0302 + U+0303 = 1 cell
			var cells = MarkupParser.Parse("A\u0300\u0301\u0302\u0303", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Equal(new Rune('A'), cells[0].Character);
			Assert.Equal("\u0300\u0301\u0302\u0303", cells[0].Combiners);
		}

		[Fact]
		public void Parse_CombiningMarkAfterWideChar_AttachesToBase()
		{
			// 中(wide) + U+0300 → combiner on base cell, not continuation
			var cells = MarkupParser.Parse("\u4E2D\u0300", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('\u4E2D'), cells[0].Character);
			Assert.NotNull(cells[0].Combiners);
			Assert.Contains("\u0300", cells[0].Combiners);
			Assert.Null(cells[1].Combiners);
		}

		[Fact]
		public void Parse_CombiningMarkAfterEmoji_AttachesToBase()
		{
			// 🔥(wide) + U+0300 → combiner on base cell
			var cells = MarkupParser.Parse("\U0001F525\u0300", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.NotNull(cells[0].Combiners);
			Assert.Null(cells[1].Combiners);
		}

		[Fact]
		public void Parse_EnclosingCircle_AttachesAsCombiner()
		{
			// A + U+20DD (combining enclosing circle) = 1 cell
			var cells = MarkupParser.Parse("A\u20DD", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Contains("\u20DD", cells[0].Combiners);
		}

		[Fact]
		public void Parse_TenCombiningMarks_AllAttach()
		{
			var sb = new StringBuilder("X");
			for (int i = 0; i < 10; i++)
				sb.Append('\u0300');

			var cells = MarkupParser.Parse(sb.ToString(), Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Equal(10, cells[0].Combiners!.Length); // Each U+0300 is 1 UTF-16 char
		}

		#endregion

		#region Spacing Combining Marks (Mc) in Parse

		[Fact]
		public void Parse_McMarkAfterConsonant_CreatesSeparateCell()
		{
			// का = क(1 cell) + ा(Mc, 1 cell) = 2 cells
			var cells = MarkupParser.Parse("\u0915\u093E", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('\u0915'), cells[0].Character);
			Assert.Equal(new Rune('\u093E'), cells[1].Character);
			Assert.Null(cells[0].Combiners); // NOT a combiner
		}

		[Fact]
		public void Parse_MnMarkAfterConsonant_AttachesAsCombiner()
		{
			// क्  = क(1 cell) + ्(Mn, combiner) = 1 cell
			var cells = MarkupParser.Parse("\u0915\u094D", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.NotNull(cells[0].Combiners);
		}

		[Fact]
		public void Parse_MixedMcAndMn_CorrectCellLayout()
		{
			// "हिन्दी" = ह(1) + ि(Mc,1) + न(1) + ्(Mn,0) + द(1) + ी(Mc,1) = 5 cells
			var cells = MarkupParser.Parse("हिन्दी", Color.White, Color.Black);

			Assert.Equal(5, cells.Count);
			Assert.Equal(new Rune('ह'), cells[0].Character);
			Assert.Equal(new Rune('ि'), cells[1].Character); // Mc own cell
			Assert.Equal(new Rune('न'), cells[2].Character);
			Assert.NotNull(cells[2].Combiners); // ्(virama) attached
			Assert.Equal(new Rune('द'), cells[3].Character);
			Assert.Equal(new Rune('ी'), cells[4].Character); // Mc own cell
		}

		[Fact]
		public void Parse_ThaiWithMcMarks_CorrectCells()
		{
			// Thai text with Mc marks produces separate cells for Mc
			// สา = ส(1) + า(Mc,1) = 2 cells (U+0E2A + U+0E32)
			var cells = MarkupParser.Parse("\u0E2A\u0E32", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('\u0E2A'), cells[0].Character);
			Assert.Equal(new Rune('\u0E32'), cells[1].Character);
		}

		[Fact]
		public void StripLength_MatchesParse_ForDevanagari()
		{
			string[] samples = { "नमस्ते", "हिन्दी", "दुनिया", "भारत" };
			foreach (string text in samples)
			{
				int strip = MarkupParser.StripLength(text);
				var cells = MarkupParser.Parse(text, Color.White, Color.Black);
				Assert.Equal(cells.Count, strip);
			}
		}

		#endregion

		#region Invisible Characters in Parse

		[Fact]
		public void Parse_ZeroWidthSpace_AttachesAsCombiner()
		{
			// A + ZWSP(0) + B = A(1 cell, ZWSP combiner) + B(1 cell) = 2 cells
			var cells = MarkupParser.Parse("A\u200BB", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('A'), cells[0].Character);
			Assert.NotNull(cells[0].Combiners);
			Assert.Equal(new Rune('B'), cells[1].Character);
		}

		[Fact]
		public void Parse_ZWNJ_AttachesAsCombiner()
		{
			// A + ZWNJ(0) + B = 2 cells
			var cells = MarkupParser.Parse("A\u200CB", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.NotNull(cells[0].Combiners);
		}

		[Fact]
		public void Parse_BidiMarks_AttachAsCombiners()
		{
			// "Hi" + LRM + "Bye" = H(1) + i(1, LRM combiner) + B(1) + y(1) + e(1) = 5 cells
			var cells = MarkupParser.Parse("Hi\u200EBye", Color.White, Color.Black);

			Assert.Equal(5, cells.Count);
		}

		#endregion

		#region Zero-Width at String Start

		[Fact]
		public void Parse_ZWJAtStart_IsDropped()
		{
			// ZWJ at start with no previous cell to attach to is dropped.
			var cells = MarkupParser.Parse("\u200DA", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Equal(new Rune('A'), cells[0].Character);
		}

		[Fact]
		public void Parse_CombiningMarkAtStart_IsDropped()
		{
			// U+0301 at start with no base cell to attach to is dropped.
			var cells = MarkupParser.Parse("\u0301A", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Equal(new Rune('A'), cells[0].Character);
		}

		[Fact]
		public void Parse_MultipleCombinersAtStart_AllDropped()
		{
			// Leading zero-width runes with no base cell are all dropped;
			// the first real base cell starts fresh without their combiners.
			var cells = MarkupParser.Parse("\u0300\u0301A", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Equal(new Rune('A'), cells[0].Character);
			Assert.Null(cells[0].Combiners);
		}

		#endregion

		#region Mixed Content with Markup Tags

		[Fact]
		public void Parse_EmojiInMarkup_CombinersPreserved()
		{
			// [red]⚡️[/] = ⚡ + FE0F in red
			var cells = MarkupParser.Parse("[red]\u26A1\uFE0F[/]", Color.White, Color.Black);

			Assert.Equal(2, cells.Count);
			Assert.Equal(Color.Red, cells[0].Foreground);
			Assert.Contains("\uFE0F", cells[0].Combiners);
		}

		[Fact]
		public void Parse_KeycapInMarkup_CorrectCells()
		{
			// [bold]1️⃣[/] — the literal text path in MarkupParser handles this
			var cells = MarkupParser.Parse("[bold]1\uFE0F\u20E3[/]", Color.White, Color.Black);

			// '1' is base cell, FE0F widens it (VS16), 20E3 is combiner = 2 cells
			Assert.Equal(2, cells.Count);
			Assert.Equal(new Rune('1'), cells[0].Character);
			Assert.NotNull(cells[0].Combiners);
			Assert.True(cells[1].IsWideContinuation);
		}

		[Fact]
		public void Parse_DevanagariInMarkup_McHandledCorrectly()
		{
			// [green]नमस्ते[/] — Mc marks create own cells
			var cells = MarkupParser.Parse("[green]नमस्ते[/]", Color.White, Color.Black);

			// All cells should be green
			foreach (var cell in cells)
				Assert.Equal(Color.Green, cell.Foreground);

			// StripLength should match
			Assert.Equal(MarkupParser.StripLength("[green]नमस्ते[/]"), cells.Count);
		}

		[Fact]
		public void Parse_MixedScriptsInMarkup_CorrectLayout()
		{
			// [red]A[/][blue]中[/][green]🔥[/] = A(1) + 中(2) + 🔥(2) = 5 cells
			var cells = MarkupParser.Parse(
				"[red]A[/][blue]\u4E2D[/][green]\U0001F525[/]",
				Color.White, Color.Black);

			Assert.Equal(5, cells.Count);
			Assert.Equal(Color.Red, cells[0].Foreground);   // A
			Assert.Equal(Color.Blue, cells[1].Foreground);  // 中
			Assert.Equal(Color.Blue, cells[2].Foreground);  // 中 continuation
			Assert.Equal(Color.Green, cells[3].Foreground); // 🔥
			Assert.Equal(Color.Green, cells[4].Foreground); // 🔥 continuation
		}

		#endregion

		#region Truncate Edge Cases

		[Fact]
		public void Truncate_AtZWJBoundary_ExcludesIfNoRoom()
		{
			// "A👨‍👩" = A(1) + 👨(2) + ZWJ(0) + 👩(2) = 5 display width
			// Truncate to 3: A(1) + 👨(2) = 3, ZWJ attaches to 👨 → "A👨‍"
			var result = MarkupParser.Truncate(
				"A\U0001F468\u200D\U0001F469", 3);

			Assert.StartsWith("A", result);
			// Should contain 👨 and the trailing ZWJ
			Assert.Contains("\U0001F468", result);
		}

		[Fact]
		public void Truncate_EmojiWithFE0F_PreservesTrailingCombiner()
		{
			// "⚡️X" = ⚡(2) + FE0F(0) + X(1) = 3 width
			// Truncate to 2: ⚡(2) + trailing FE0F = "⚡️"
			var result = MarkupParser.Truncate("\u26A1\uFE0FX", 2);

			Assert.Contains("\u26A1", result);
			Assert.Contains("\uFE0F", result);
			Assert.DoesNotContain("X", result);
		}

		[Fact]
		public void Truncate_KeycapSequence_PreservesCombiners()
		{
			// "1️⃣AB" = 1(1)+FE0F(+1 VS16 widening)+20E3(0)+A(1)+B(1) = 4
			// Truncate to 2: "1️⃣" (keycap is 2 cols wide with VS16)
			var result = MarkupParser.Truncate("1\uFE0F\u20E3AB", 2);

			Assert.Equal("1\uFE0F\u20E3", result);
		}

		[Fact]
		public void Truncate_DevanagariMcMark_IncludedIfRoom()
		{
			// "काम" = क(1) + ा(Mc,1) + म(1) = 3
			// Truncate to 2: "का"
			var result = MarkupParser.Truncate("\u0915\u093E\u092E", 2);
			Assert.Equal("\u0915\u093E", result);
		}

		[Fact]
		public void Truncate_DevanagariMnMark_PreservedAsCombiner()
		{
			// "क्ष" = क(1) + ्(Mn,0) + ष(1) = 2
			// Truncate to 1: "क्" (क + trailing virama)
			var result = MarkupParser.Truncate("\u0915\u094D\u0937", 1);
			Assert.Equal("\u0915\u094D", result);
		}

		[Fact]
		public void Truncate_MixedEmojiAndDevanagari_CorrectBoundary()
		{
			// "🔥का" = 🔥(2) + क(1) + ा(Mc,1) = 4
			// Truncate to 3: "🔥क"
			var result = MarkupParser.Truncate("\U0001F525\u0915\u093E", 3);
			Assert.Equal("\U0001F525\u0915", result);
		}

		[Fact]
		public void Truncate_SkinToneEmoji_ModifierPreservedAsCombiner()
		{
			// "👍🏽X" = 👍(2) + 🏽(0, combiner) + X(1) = 3
			// Truncate to 2: "👍🏽" (base + trailing zero-width modifier)
			var result = MarkupParser.Truncate("\U0001F44D\U0001F3FDX", 2);
			Assert.Equal("\U0001F44D\U0001F3FD", result);
		}

		[Fact]
		public void Truncate_SkinToneEmoji_FullString()
		{
			// "👍🏽" = 👍(2) + 🏽(0) = 2, truncate to 2 returns full string
			var result = MarkupParser.Truncate("\U0001F44D\U0001F3FD", 2);
			Assert.Equal("\U0001F44D\U0001F3FD", result);
		}

		[Fact]
		public void Truncate_Width0_ReturnsEmpty()
		{
			Assert.Equal("", MarkupParser.Truncate("\U0001F525\u0915\u093E", 0));
		}

		[Fact]
		public void Truncate_ExactWidth_ReturnsFullString()
		{
			string s = "\U0001F525AB"; // 🔥(2) + A(1) + B(1) = 4
			Assert.Equal(s, MarkupParser.Truncate(s, 4));
		}

		#endregion

		#region ParseLines Edge Cases

		[Fact]
		public void ParseLines_EmojiAtLineEnd_FitsExactly()
		{
			// Width=4, "AB🔥" = A(1)+B(1)+🔥(2) = 4 → fits on one line
			var lines = MarkupParser.ParseLines("AB\U0001F525", 4, Color.White, Color.Black);

			Assert.Single(lines);
			Assert.Equal(4, lines[0].Count);
		}

		[Fact]
		public void ParseLines_EmojiDoesntFit_WrapsToNextLine()
		{
			// Width=3, "AB🔥" = A(1)+B(1)+🔥(2) = 4 > 3
			// Line 1: AB (2), 🔥 needs 2 but only 1 left → wraps
			// Line 2: 🔥 (2 cells)
			var lines = MarkupParser.ParseLines("AB\U0001F525", 3, Color.White, Color.Black);

			Assert.Equal(2, lines.Count);
			Assert.Equal(2, lines[0].Count); // AB
			Assert.Equal(2, lines[1].Count); // 🔥
		}

		[Fact]
		public void ParseLines_DevanagariWrapping_McMarkOnSameLine()
		{
			// Width=2, "काम" = क(1)+ा(Mc,1)+म(1) = 3
			// Line 1: का (2 cells), Line 2: म (1 cell)
			var lines = MarkupParser.ParseLines("\u0915\u093E\u092E", 2, Color.White, Color.Black);

			Assert.Equal(2, lines.Count);
			Assert.Equal(2, lines[0].Count);
			Assert.Equal(new Rune('\u0915'), lines[0][0].Character);
			Assert.Equal(new Rune('\u093E'), lines[0][1].Character);
		}

		[Fact]
		public void ParseLines_ZWJSequence_FitsOnLine()
		{
			// Width=6, "👨‍👩‍👦" = 6 cells → fits exactly
			var lines = MarkupParser.ParseLines(
				"\U0001F468\u200D\U0001F469\u200D\U0001F466",
				6, Color.White, Color.Black);

			Assert.Single(lines);
			Assert.Equal(6, lines[0].Count);
		}

		[Fact]
		public void ParseLines_CombinerDoesntPushOverWidth()
		{
			// Width=2, "e\u0301X" = é(1 cell)+X(1 cell) = 2 cells, fits
			var lines = MarkupParser.ParseLines("e\u0301X", 2, Color.White, Color.Black);

			Assert.Single(lines);
			Assert.Equal(2, lines[0].Count);
			Assert.NotNull(lines[0][0].Combiners);
		}

		#endregion

		#region Precomposed vs Decomposed in Parse

		[Fact]
		public void Parse_Precomposed_SingleCell()
		{
			// é (U+00E9) = 1 cell, no combiners
			var cells = MarkupParser.Parse("\u00E9", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Null(cells[0].Combiners);
		}

		[Fact]
		public void Parse_Decomposed_SingleCellWithCombiner()
		{
			// e + U+0301 = 1 cell with combiner
			var cells = MarkupParser.Parse("e\u0301", Color.White, Color.Black);

			Assert.Single(cells);
			Assert.Equal(new Rune('e'), cells[0].Character);
			Assert.NotNull(cells[0].Combiners);
		}

		[Fact]
		public void StripLength_PrecomposedEqualsDecomposed()
		{
			Assert.Equal(
				MarkupParser.StripLength("\u00E9"),
				MarkupParser.StripLength("e\u0301"));
		}

		#endregion

		#region Consistency: StripLength == Parse Count

		[Theory]
		[InlineData("Hello")]
		[InlineData("\u4E2D\u6587")]
		[InlineData("\U0001F525\U0001F389")]
		[InlineData("A\u4E2dB")]
		[InlineData("\u26A1\uFE0F")]
		[InlineData("e\u0301")]
		[InlineData("1\uFE0F\u20E3")]
		[InlineData("\U0001F468\u200D\U0001F469")]
		[InlineData("\U0001F44D\U0001F3FD")]
		[InlineData("दुनिया")]
		[InlineData("हिन्दी")]
		[InlineData("A\u200BB")]
		public void StripLength_AlwaysMatchesParseCellCount(string text)
		{
			int strip = MarkupParser.StripLength(text);
			var cells = MarkupParser.Parse(text, Color.White, Color.Black);
			Assert.Equal(cells.Count, strip);
		}

		#endregion
	}
}
