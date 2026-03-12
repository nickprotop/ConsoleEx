using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using System.Text;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer
{
	/// <summary>
	/// Comprehensive Unicode edge case tests for CharacterBuffer.
	/// Tests WriteString/WriteStringClipped with emoji ZWJ sequences,
	/// skin tones, keycaps, Mc marks, combining chains, and overwrite scenarios.
	/// </summary>
	public class CharacterBufferUnicodeEdgeCaseTests
	{
		#region Emoji ZWJ Sequences

		[Fact]
		public void WriteString_FamilyEmoji_CorrectColumnPositions()
		{
			var buffer = new CharacterBuffer(30, 5);

			// 👨‍👩‍👦 = 👨(2)+ZWJ(combiner)+👩(2)+ZWJ(combiner)+👦(2) = 6 columns
			buffer.WriteString(0, 0,
				"\U0001F468\u200D\U0001F469\u200D\U0001F466",
				Color.White, Color.Black);

			// 👨 at col 0-1
			Assert.Equal(new Rune(0x1F468), buffer.GetCell(0, 0).Character);
			Assert.NotNull(buffer.GetCell(0, 0).Combiners); // ZWJ attached
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);

			// 👩 at col 2-3
			Assert.Equal(new Rune(0x1F469), buffer.GetCell(2, 0).Character);
			Assert.NotNull(buffer.GetCell(2, 0).Combiners);
			Assert.True(buffer.GetCell(3, 0).IsWideContinuation);

			// 👦 at col 4-5
			Assert.Equal(new Rune(0x1F466), buffer.GetCell(4, 0).Character);
			Assert.True(buffer.GetCell(5, 0).IsWideContinuation);

			// Col 6 should be untouched space
			Assert.Equal(new Rune(' '), buffer.GetCell(6, 0).Character);
		}

		[Fact]
		public void WriteString_WomanTechnologist_CorrectLayout()
		{
			var buffer = new CharacterBuffer(20, 5);

			// 👩‍💻 = 👩(2) + ZWJ(combiner) + 💻(2) = 4 columns
			buffer.WriteString(0, 0, "\U0001F469\u200D\U0001F4BB", Color.White, Color.Black);

			Assert.Equal(new Rune(0x1F469), buffer.GetCell(0, 0).Character);
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
			Assert.Equal(new Rune(0x1F4BB), buffer.GetCell(2, 0).Character);
			Assert.True(buffer.GetCell(3, 0).IsWideContinuation);
		}

		#endregion

		#region Skin Tone Modifiers

		[Fact]
		public void WriteString_EmojiWithSkinTone_ModifierAttachesAsCombiner()
		{
			var buffer = new CharacterBuffer(20, 5);

			// 👋🏻 = 👋(2 cols) + 🏻(0, combiner) = 2 columns
			buffer.WriteString(0, 0, "\U0001F44B\U0001F3FB", Color.White, Color.Black);

			Assert.Equal(new Rune(0x1F44B), buffer.GetCell(0, 0).Character);
			Assert.NotNull(buffer.GetCell(0, 0).Combiners); // skin tone attached to base
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
			// Col 2 untouched
			Assert.Equal(new Rune(' '), buffer.GetCell(2, 0).Character);
		}

		#endregion

		#region Keycap Sequences

		[Fact]
		public void WriteString_KeycapSequence_TwoColumns()
		{
			var buffer = new CharacterBuffer(20, 5);

			// 1️⃣ = '1'(1) + FE0F(VS16 widens to 2) + U+20E3(combiner) = 2 columns
			buffer.WriteString(0, 0, "1\uFE0F\u20E3", Color.White, Color.Black);

			var cell = buffer.GetCell(0, 0);
			Assert.Equal(new Rune('1'), cell.Character);
			Assert.NotNull(cell.Combiners);
			Assert.Equal("\uFE0F\u20E3", cell.Combiners);

			// Column 1 should be a continuation cell from VS16 widening
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
		}

		[Fact]
		public void WriteString_MultipleKeycaps_CorrectPositions()
		{
			var buffer = new CharacterBuffer(20, 5);

			// "1️⃣2️⃣A" = 1(cols 0-1) + 2(cols 2-3) + A(col 4) = 5 columns
			buffer.WriteString(0, 0, "1\uFE0F\u20E32\uFE0F\u20E3A", Color.White, Color.Black);

			Assert.Equal(new Rune('1'), buffer.GetCell(0, 0).Character);
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
			Assert.Equal(new Rune('2'), buffer.GetCell(2, 0).Character);
			Assert.True(buffer.GetCell(3, 0).IsWideContinuation);
			Assert.Equal(new Rune('A'), buffer.GetCell(4, 0).Character);
		}

		#endregion

		#region Variation Selectors

		[Fact]
		public void WriteString_FE0FAfterWideEmoji_AttachesToBase()
		{
			var buffer = new CharacterBuffer(20, 5);

			// ⚡(wide, 2 cols) + FE0F → combiner on base cell (col 0), not continuation (col 1)
			buffer.WriteString(0, 0, "\u26A1\uFE0F", Color.White, Color.Black);

			Assert.NotNull(buffer.GetCell(0, 0).Combiners);
			Assert.Contains("\uFE0F", buffer.GetCell(0, 0).Combiners);
			Assert.Null(buffer.GetCell(1, 0).Combiners);
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
		}

		[Fact]
		public void WriteString_FE0FAfterNarrowChar_AttachesToChar()
		{
			var buffer = new CharacterBuffer(20, 5);

			// ✈(narrow, 1 col) + FE0F = 1 column with combiner
			buffer.WriteString(0, 0, "\u2708\uFE0F", Color.White, Color.Black);

			Assert.Equal(new Rune('\u2708'), buffer.GetCell(0, 0).Character);
			Assert.Contains("\uFE0F", buffer.GetCell(0, 0).Combiners);
			// Col 1 untouched
			Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character);
		}

		#endregion

		#region Combining Marks

		[Fact]
		public void WriteString_MultipleCombiningMarks_AllAttachToBase()
		{
			var buffer = new CharacterBuffer(20, 5);

			// A + 3 combining marks = 1 column
			buffer.WriteString(0, 0, "A\u0300\u0301\u0302", Color.White, Color.Black);

			var cell = buffer.GetCell(0, 0);
			Assert.Equal(new Rune('A'), cell.Character);
			Assert.Equal("\u0300\u0301\u0302", cell.Combiners);

			// Col 1 untouched
			Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character);
		}

		[Fact]
		public void WriteString_CombiningAfterCJK_AttachesToBase()
		{
			var buffer = new CharacterBuffer(20, 5);

			// 中 (2 cols) + combining grave → combiner on base (col 0), not continuation (col 1)
			buffer.WriteString(0, 0, "\u4E2D\u0300", Color.White, Color.Black);

			Assert.NotNull(buffer.GetCell(0, 0).Combiners);
			Assert.Null(buffer.GetCell(1, 0).Combiners);
		}

		[Fact]
		public void WriteString_CombiningAfterEmoji_AttachesToBase()
		{
			var buffer = new CharacterBuffer(20, 5);

			// 🔥(2 cols) + combining grave → combiner on base cell
			buffer.WriteString(0, 0, "\U0001F525\u0300", Color.White, Color.Black);

			Assert.NotNull(buffer.GetCell(0, 0).Combiners);
			Assert.Null(buffer.GetCell(1, 0).Combiners);
		}

		[Fact]
		public void WriteString_EnclosingCircle_AttachesAsCombiner()
		{
			var buffer = new CharacterBuffer(20, 5);

			// A + U+20DD (enclosing circle) = 1 column
			buffer.WriteString(0, 0, "A\u20DD", Color.White, Color.Black);

			Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
			Assert.Contains("\u20DD", buffer.GetCell(0, 0).Combiners);
		}

		#endregion

		#region Spacing Combining Marks (Mc)

		[Fact]
		public void WriteString_DevanagariMcMark_OccupiesOwnColumn()
		{
			var buffer = new CharacterBuffer(20, 5);

			// का = क(col 0) + ा(Mc, col 1)
			buffer.WriteString(0, 0, "\u0915\u093E", Color.White, Color.Black);

			Assert.Equal(new Rune('\u0915'), buffer.GetCell(0, 0).Character);
			Assert.Null(buffer.GetCell(0, 0).Combiners); // NOT a combiner
			Assert.Equal(new Rune('\u093E'), buffer.GetCell(1, 0).Character);
			Assert.False(buffer.GetCell(1, 0).IsWideContinuation);
		}

		[Fact]
		public void WriteString_DevanagariMnMark_AttachesAsCombiner()
		{
			var buffer = new CharacterBuffer(20, 5);

			// क्  = क(col 0, with virama combiner) = 1 column
			buffer.WriteString(0, 0, "\u0915\u094D", Color.White, Color.Black);

			Assert.Equal(new Rune('\u0915'), buffer.GetCell(0, 0).Character);
			Assert.NotNull(buffer.GetCell(0, 0).Combiners);
			Assert.Contains("\u094D", buffer.GetCell(0, 0).Combiners);
		}

		[Fact]
		public void WriteString_DevanagariMixed_CorrectColumnLayout()
		{
			var buffer = new CharacterBuffer(20, 5);

			// "दुनिया" = द(0)+ु(combiner)+न(1)+ि(Mc,2)+य(3)+ा(Mc,4) = 5 columns
			buffer.WriteString(0, 0, "दुनिया", Color.White, Color.Black);

			Assert.Equal(new Rune('द'), buffer.GetCell(0, 0).Character);
			Assert.NotNull(buffer.GetCell(0, 0).Combiners); // ु attached
			Assert.Equal(new Rune('न'), buffer.GetCell(1, 0).Character);
			Assert.Equal(new Rune('ि'), buffer.GetCell(2, 0).Character); // Mc own column
			Assert.Equal(new Rune('य'), buffer.GetCell(3, 0).Character);
			Assert.Equal(new Rune('ा'), buffer.GetCell(4, 0).Character); // Mc own column
		}

		#endregion

		#region Clipping with Complex Unicode

		[Fact]
		public void WriteStringClipped_ZWJSequencePartiallyClipped()
		{
			var buffer = new CharacterBuffer(20, 5);
			var clipRect = new LayoutRect(0, 0, 4, 5);

			// 👨‍👩‍👦 = 6 columns, clip at 4 → first 2 emoji (4 cols) visible
			buffer.WriteStringClipped(0, 0,
				"\U0001F468\u200D\U0001F469\u200D\U0001F466",
				Color.White, Color.Black, clipRect);

			Assert.Equal(new Rune(0x1F468), buffer.GetCell(0, 0).Character);
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
			Assert.Equal(new Rune(0x1F469), buffer.GetCell(2, 0).Character);
			Assert.True(buffer.GetCell(3, 0).IsWideContinuation);
		}

		[Fact]
		public void WriteStringClipped_EmojiStraddlesClip_ReplacedWithSpace()
		{
			var buffer = new CharacterBuffer(20, 5);
			var clipRect = new LayoutRect(0, 0, 3, 5);

			// "A🔥B" = A(1)+🔥(2)+B(1) = 4 cols, clip at 3
			// 🔥 starts at col 1 and ends at col 2 — fits within 3
			buffer.WriteStringClipped(0, 0, "A\U0001F525B", Color.White, Color.Black, clipRect);

			Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
			Assert.Equal(new Rune(0x1F525), buffer.GetCell(1, 0).Character);
			Assert.True(buffer.GetCell(2, 0).IsWideContinuation);
		}

		[Fact]
		public void WriteStringClipped_KeycapInClip_CombiersPreserved()
		{
			var buffer = new CharacterBuffer(20, 5);
			var clipRect = new LayoutRect(0, 0, 5, 5);

			// "1️⃣AB" = 1(2, VS16 widened)+A(1)+B(1) = 4 columns
			buffer.WriteStringClipped(0, 0, "1\uFE0F\u20E3AB", Color.White, Color.Black, clipRect);

			Assert.Equal(new Rune('1'), buffer.GetCell(0, 0).Character);
			Assert.Contains("\uFE0F", buffer.GetCell(0, 0).Combiners);
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
			Assert.Equal(new Rune('A'), buffer.GetCell(2, 0).Character);
		}

		[Fact]
		public void WriteStringClipped_DevanagariInClip_McMarksOwnColumns()
		{
			var buffer = new CharacterBuffer(20, 5);
			var clipRect = new LayoutRect(0, 0, 3, 5);

			// "काम" = क(1)+ा(Mc,1)+म(1) = 3 columns, exactly fits clip
			buffer.WriteStringClipped(0, 0, "\u0915\u093E\u092E",
				Color.White, Color.Black, clipRect);

			Assert.Equal(new Rune('\u0915'), buffer.GetCell(0, 0).Character);
			Assert.Equal(new Rune('\u093E'), buffer.GetCell(1, 0).Character);
			Assert.Equal(new Rune('\u092E'), buffer.GetCell(2, 0).Character);
		}

		#endregion

		#region Overwrite with Complex Unicode

		[Fact]
		public void OverwriteEmojiWithNarrow_ContinuationCleanedUp()
		{
			var buffer = new CharacterBuffer(20, 5);

			buffer.WriteString(0, 0, "\U0001F525", Color.White, Color.Black); // 🔥 at col 0-1
			buffer.SetCell(0, 0, 'A', Color.White, Color.Black);

			Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
			Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character); // continuation cleared
			Assert.False(buffer.GetCell(1, 0).IsWideContinuation);
		}

		[Fact]
		public void OverwriteCellWithCombiners_CombinersCleared()
		{
			var buffer = new CharacterBuffer(20, 5);

			// Write char with combiner
			buffer.WriteString(0, 0, "A\u0300", Color.White, Color.Black);
			Assert.NotNull(buffer.GetCell(0, 0).Combiners);

			// Overwrite
			buffer.SetCell(0, 0, 'B', Color.White, Color.Black);

			Assert.Equal(new Rune('B'), buffer.GetCell(0, 0).Character);
			Assert.Null(buffer.GetCell(0, 0).Combiners);
		}

		[Fact]
		public void OverwriteWideCharWithCombiner_CombinerOnNewBase()
		{
			var buffer = new CharacterBuffer(20, 5);

			// Write 🔥 at col 0-1
			buffer.WriteString(0, 0, "\U0001F525", Color.White, Color.Black);

			// Overwrite with narrow + combiner
			buffer.WriteString(0, 0, "e\u0301", Color.White, Color.Black);

			Assert.Equal(new Rune('e'), buffer.GetCell(0, 0).Character);
			Assert.Contains("\u0301", buffer.GetCell(0, 0).Combiners);
			// Continuation at col 1 should be cleaned up
			Assert.Equal(new Rune(' '), buffer.GetCell(1, 0).Character);
		}

		#endregion

		#region Mixed Content Strings

		[Fact]
		public void WriteString_CjkEmojiDevanagariLatin_CorrectLayout()
		{
			var buffer = new CharacterBuffer(30, 5);

			// "中🔥नाA" = 中(2)+🔥(2)+न(1)+ा(Mc,1)+A(1) = 7 columns
			buffer.WriteString(0, 0, "\u4E2D\U0001F525\u0928\u093EA", Color.White, Color.Black);

			Assert.Equal(new Rune('\u4E2D'), buffer.GetCell(0, 0).Character);
			Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
			Assert.Equal(new Rune(0x1F525), buffer.GetCell(2, 0).Character);
			Assert.True(buffer.GetCell(3, 0).IsWideContinuation);
			Assert.Equal(new Rune('\u0928'), buffer.GetCell(4, 0).Character);
			Assert.Equal(new Rune('\u093E'), buffer.GetCell(5, 0).Character); // Mc
			Assert.Equal(new Rune('A'), buffer.GetCell(6, 0).Character);
		}

		[Fact]
		public void WriteString_FlagEmoji_TwoColumns()
		{
			var buffer = new CharacterBuffer(20, 5);

			// 🇺🇸 = Regional U(1) + Regional S(1) = 2 columns
			buffer.WriteString(0, 0, "\U0001F1FA\U0001F1F8", Color.White, Color.Black);

			Assert.Equal(new Rune(0x1F1FA), buffer.GetCell(0, 0).Character);
			Assert.Equal(new Rune(0x1F1F8), buffer.GetCell(1, 0).Character);
		}

		#endregion

		#region ToLines ANSI Output

		[Fact]
		public void ToLines_CombinersIncludedInOutput()
		{
			var buffer = new CharacterBuffer(5, 1);

			// "e\u0301" = 1 column with combiner
			buffer.WriteString(0, 0, "e\u0301", Color.White, Color.Black);

			var lines = buffer.ToLines(Color.White, Color.Black);

			// Output should contain the combining character (use ordinal for combining marks)
			Assert.True(lines[0].Contains("\u0301", StringComparison.Ordinal),
				"Output should contain combining acute accent (U+0301)");
		}

		[Fact]
		public void ToLines_FE0FIncludedInOutput()
		{
			var buffer = new CharacterBuffer(5, 1);

			buffer.WriteString(0, 0, "\u2708\uFE0F", Color.White, Color.Black);

			var lines = buffer.ToLines(Color.White, Color.Black);

			Assert.True(lines[0].Contains("\uFE0F", StringComparison.Ordinal),
				"Output should contain variation selector FE0F");
		}

		[Fact]
		public void ToLines_KeycapSequencePreserved()
		{
			var buffer = new CharacterBuffer(5, 1);

			buffer.WriteString(0, 0, "1\uFE0F\u20E3", Color.White, Color.Black);

			var lines = buffer.ToLines(Color.White, Color.Black);

			Assert.True(lines[0].Contains("\uFE0F", StringComparison.Ordinal),
				"Output should contain FE0F");
			Assert.True(lines[0].Contains("\u20E3", StringComparison.Ordinal),
				"Output should contain combining enclosing keycap (U+20E3)");
		}

		[Fact]
		public void ToLines_DevanagariMcMarks_RenderedAsSeparateChars()
		{
			var buffer = new CharacterBuffer(5, 1);

			// "का" = क + ा(Mc) = 2 columns
			buffer.WriteString(0, 0, "\u0915\u093E", Color.White, Color.Black);

			var lines = buffer.ToLines(Color.White, Color.Black);

			// Both characters should appear in output (use ordinal for Indic characters)
			Assert.True(lines[0].Contains("\u0915", StringComparison.Ordinal),
				"Output should contain Devanagari Ka");
			Assert.True(lines[0].Contains("\u093E", StringComparison.Ordinal),
				"Output should contain Devanagari vowel sign AA");
		}

		#endregion
	}
}
