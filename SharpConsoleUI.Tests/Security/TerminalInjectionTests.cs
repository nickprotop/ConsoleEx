// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Security
{
	/// <summary>
	/// Tests that control characters, escape sequences, and BiDi overrides
	/// are sanitized before reaching cells, preventing terminal escape injection.
	/// </summary>
	public class TerminalInjectionTests
	{
		[Fact]
		public void WriteString_AnsiColorEscape_ReplacedWithFffd()
		{
			var buffer = new CharacterBuffer(40, 5);
			buffer.WriteString(0, 0, "hello\x1b[31mred\x1b[0m", Color.White, Color.Black);

			// Verify no cell contains ESC (U+001B) in Character or Combiners
			for (int x = 0; x < 40; x++)
			{
				var cell = buffer.GetCell(x, 0);
				Assert.NotEqual(new Rune('\x1b'), cell.Character);
				if (cell.Combiners != null)
				{
					foreach (var rune in cell.Combiners.EnumerateRunes())
						Assert.NotEqual(new Rune('\x1b'), rune);
				}
			}
		}

		[Fact]
		public void WriteString_Osc52ClipboardSet_NoEscapeOrBelInOutput()
		{
			var buffer = new CharacterBuffer(80, 5);
			// OSC 52 clipboard set: ESC ] 52 ; c ; base64 BEL
			string osc52 = "\x1b]52;c;SGVsbG8=\x07";
			buffer.WriteString(0, 0, osc52, Color.White, Color.Black);

			for (int x = 0; x < 80; x++)
			{
				var cell = buffer.GetCell(x, 0);
				Assert.NotEqual(new Rune('\x1b'), cell.Character);
				Assert.NotEqual(new Rune('\x07'), cell.Character);
				if (cell.Combiners != null)
				{
					foreach (var rune in cell.Combiners.EnumerateRunes())
					{
						Assert.NotEqual(new Rune('\x1b'), rune);
						Assert.NotEqual(new Rune('\x07'), rune);
					}
				}
			}
		}

		[Fact]
		public void WriteString_TrojanSourceRlo_ReplacedWithFffd()
		{
			var buffer = new CharacterBuffer(20, 5);
			// U+202E is Right-to-Left Override
			buffer.WriteString(0, 0, "admin\u202eresu", Color.White, Color.Black);

			// "admin" = 5 chars, then U+202E becomes U+FFFD at position 5
			var cell = buffer.GetCell(5, 0);
			Assert.Equal(new Rune('\uFFFD'), cell.Character);
		}

		[Fact]
		public void WriteString_LegitCombiningMark_Preserved()
		{
			var buffer = new CharacterBuffer(20, 5);
			// "cafe" + U+0301 (combining acute accent) = "café"
			buffer.WriteString(0, 0, "cafe\u0301", Color.White, Color.Black);

			// 'e' at position 3 should have U+0301 as combiner
			var cell = buffer.GetCell(3, 0);
			Assert.Equal(new Rune('e'), cell.Character);
			Assert.NotNull(cell.Combiners);
			Assert.Contains("\u0301", cell.Combiners);
		}

		[Fact]
		public void WriteString_Vs16Emoji_WidensCorrectly()
		{
			var buffer = new CharacterBuffer(20, 5);
			// U+26A1 (high voltage) + U+FE0F (VS16) should widen to 2 columns
			buffer.WriteString(0, 0, "\u26A1\uFE0F", Color.White, Color.Black);

			var cell = buffer.GetCell(0, 0);
			Assert.Equal(new Rune('\u26A1'), cell.Character);
			Assert.NotNull(cell.Combiners);
			Assert.Contains("\uFE0F", cell.Combiners);

			// Continuation cell at position 1
			var cont = buffer.GetCell(1, 0);
			Assert.True(cont.IsWideContinuation);
		}

		[Fact]
		public void WriteString_ZwjEmojiSequence_RendersIntact()
		{
			var buffer = new CharacterBuffer(20, 5);
			// Woman technologist: U+1F469 U+200D U+1F4BB
			buffer.WriteString(0, 0, "\U0001F469\u200D\U0001F4BB", Color.White, Color.Black);

			// The ZWJ (U+200D) should be preserved as a combiner
			var cell = buffer.GetCell(0, 0);
			Assert.Equal(new Rune(0x1F469), cell.Character);
			Assert.NotNull(cell.Combiners);
			Assert.Contains("\u200D", cell.Combiners);
		}

		[Fact]
		public void AppendCombiner_DirectEscape_Rejected()
		{
			var cell = new Cell('A', Color.White, Color.Black);
			cell.AppendCombiner(new Rune('\x1b'));

			// ESC should not have been appended
			Assert.Null(cell.Combiners);
		}

		[Fact]
		public void AppendCombiner_DirectBel_Rejected()
		{
			var cell = new Cell('A', Color.White, Color.Black);
			cell.AppendCombiner(new Rune('\x07'));

			Assert.Null(cell.Combiners);
		}

		[Fact]
		public void AppendCombiner_BiDiOverride_Rejected()
		{
			var cell = new Cell('A', Color.White, Color.Black);
			cell.AppendCombiner(new Rune('\u202E'));

			Assert.Null(cell.Combiners);
		}

		[Fact]
		public void AppendCombiner_LegitMark_Accepted()
		{
			var cell = new Cell('e', Color.White, Color.Black);
			cell.AppendCombiner(new Rune('\u0301')); // combining acute

			Assert.NotNull(cell.Combiners);
			Assert.Contains("\u0301", cell.Combiners);
		}

		[Fact]
		public void WriteString_C1Controls_AllReplaced()
		{
			var buffer = new CharacterBuffer(20, 5);
			// U+0090 (DCS) and U+009B (CSI) are C1 controls
			buffer.WriteString(0, 0, "A\u0090B\u009BC", Color.White, Color.Black);

			Assert.Equal(new Rune('A'), buffer.GetCell(0, 0).Character);
			Assert.Equal(new Rune('\uFFFD'), buffer.GetCell(1, 0).Character);
			Assert.Equal(new Rune('B'), buffer.GetCell(2, 0).Character);
			Assert.Equal(new Rune('\uFFFD'), buffer.GetCell(3, 0).Character);
			Assert.Equal(new Rune('C'), buffer.GetCell(4, 0).Character);
		}

		[Fact]
		public void WriteStringClipped_AnsiEscape_Sanitized()
		{
			var buffer = new CharacterBuffer(40, 5);
			var clip = new LayoutRect(0, 0, 40, 5);
			buffer.WriteStringClipped(0, 0, "test\x1b[2Jhidden", Color.White, Color.Black, clip);

			for (int x = 0; x < 40; x++)
			{
				var cell = buffer.GetCell(x, 0);
				Assert.NotEqual(new Rune('\x1b'), cell.Character);
			}
		}

		[Fact]
		public void TextSanitizer_IsUnsafeRune_IdentifiesControls()
		{
			// C0
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\x00')));
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\x07'))); // BEL
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\x0A'))); // LF
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\x0D'))); // CR
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\x1B'))); // ESC

			// DEL
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\x7F')));

			// C1
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\u0080')));
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\u009F')));

			// BiDi
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\u202A')));
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\u202E')));
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\u2066')));
			Assert.True(TextSanitizer.IsUnsafeRune(new Rune('\u2069')));

			// Safe characters
			Assert.False(TextSanitizer.IsUnsafeRune(new Rune('A')));
			Assert.False(TextSanitizer.IsUnsafeRune(new Rune(' ')));
			Assert.False(TextSanitizer.IsUnsafeRune(new Rune('\u4E2D'))); // CJK
		}

		[Fact]
		public void TextSanitizer_IsSafeCombiner_AcceptsMarks()
		{
			// Combining acute accent (Mn)
			Assert.True(TextSanitizer.IsSafeCombiner(new Rune('\u0301')));
			// Enclosing circle (Me)
			Assert.True(TextSanitizer.IsSafeCombiner(new Rune('\u20DD')));
			// VS16
			Assert.True(TextSanitizer.IsSafeCombiner(new Rune('\uFE0F')));
			// ZWJ
			Assert.True(TextSanitizer.IsSafeCombiner(new Rune('\u200D')));
			// ZWNJ
			Assert.True(TextSanitizer.IsSafeCombiner(new Rune('\u200C')));
		}

		[Fact]
		public void TextSanitizer_IsSafeCombiner_RejectsUnsafe()
		{
			// ESC
			Assert.False(TextSanitizer.IsSafeCombiner(new Rune('\x1b')));
			// BEL
			Assert.False(TextSanitizer.IsSafeCombiner(new Rune('\x07')));
			// BiDi override
			Assert.False(TextSanitizer.IsSafeCombiner(new Rune('\u202E')));
			// Regular letter (not a combiner)
			Assert.False(TextSanitizer.IsSafeCombiner(new Rune('A')));
		}
	}
}
