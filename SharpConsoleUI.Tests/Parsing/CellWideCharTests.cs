using Xunit;
using SharpConsoleUI.Layout;
using System.Text;

namespace SharpConsoleUI.Tests.Parsing
{
	public class CellWideCharTests
	{
		[Fact]
		public void Cell_IsWideContinuation_DefaultFalse()
		{
			var cell = default(Cell);

			Assert.False(cell.IsWideContinuation);
		}

		[Fact]
		public void Cell_IsWideContinuation_SetTrue()
		{
			var cell = new Cell('A', Color.White, Color.Black);

			cell.IsWideContinuation = true;

			Assert.True(cell.IsWideContinuation);
		}

		[Fact]
		public void Cell_VisuallyEquals_ContinuationFlagMatters()
		{
			var a = new Cell('A', Color.White, Color.Black);
			var b = new Cell('A', Color.White, Color.Black);
			b.IsWideContinuation = true;

			Assert.False(a.VisuallyEquals(b));
		}

		[Fact]
		public void Cell_Equals_ContinuationFlagMatters()
		{
			var a = new Cell('A', Color.White, Color.Black);
			var b = new Cell('A', Color.White, Color.Black);
			b.IsWideContinuation = true;

			Assert.False(a.Equals(b));
		}

		[Fact]
		public void Cell_GetHashCode_ContinuationFlagIncluded()
		{
			var a = new Cell('A', Color.White, Color.Black);
			var b = new Cell('A', Color.White, Color.Black);
			b.IsWideContinuation = true;

			// Cells that are not equal should ideally have different hash codes
			// (not guaranteed, but highly likely with HashCode.Combine)
			Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
		}

		[Fact]
		public void Cell_Blank_IsNotContinuation()
		{
			var blank = Cell.Blank;

			Assert.False(blank.IsWideContinuation);
		}

		[Fact]
		public void Cell_BlankWithBackground_IsNotContinuation()
		{
			var cell = Cell.BlankWithBackground(Color.Blue);

			Assert.False(cell.IsWideContinuation);
		}

		[Fact]
		public void Cell_ToString_ShowsContinuation()
		{
			var cell = new Cell('X', Color.White, Color.Black);
			cell.IsWideContinuation = true;

			string result = cell.ToString();

			Assert.Contains("continuation", result);
		}

		#region Rune / Emoji Construction

		[Fact]
		public void Cell_RuneConstructor_StoresEmoji()
		{
			var poop = new Rune(0x1F4A9); // 💩
			var cell = new Cell(poop, Color.White, Color.Black);

			Assert.Equal(poop, cell.Character);
		}

		[Fact]
		public void Cell_RuneConstructor_StoresCjkExtensionB()
		{
			var rune = new Rune(0x20000); // CJK Extension B
			var cell = new Cell(rune, Color.Yellow, Color.Black);

			Assert.Equal(rune, cell.Character);
		}

		[Fact]
		public void Cell_CharConstructor_ConvertsToRune()
		{
			var cell = new Cell('A', Color.White, Color.Black);

			Assert.Equal(new Rune('A'), cell.Character);
		}

		[Fact]
		public void Cell_Equals_EmojiRunesMatch()
		{
			var rune = new Rune(0x1F525); // 🔥
			var a = new Cell(rune, Color.Red, Color.Black);
			var b = new Cell(rune, Color.Red, Color.Black);

			Assert.True(a.Equals(b));
		}

		[Fact]
		public void Cell_Equals_DifferentEmoji_NotEqual()
		{
			var a = new Cell(new Rune(0x1F525), Color.Red, Color.Black); // 🔥
			var b = new Cell(new Rune(0x1F4A9), Color.Red, Color.Black); // 💩

			Assert.False(a.Equals(b));
		}

		[Fact]
		public void Cell_VisuallyEquals_EmojiWithSameStyle()
		{
			var rune = new Rune(0x1F600); // 😀
			var a = new Cell(rune, Color.White, Color.Black);
			var b = new Cell(rune, Color.White, Color.Black);

			Assert.True(a.VisuallyEquals(b));
		}

		[Fact]
		public void Cell_GetHashCode_EmojiConsistent()
		{
			var rune = new Rune(0x1F680); // 🚀
			var a = new Cell(rune, Color.White, Color.Black);
			var b = new Cell(rune, Color.White, Color.Black);

			Assert.Equal(a.GetHashCode(), b.GetHashCode());
		}

		[Fact]
		public void Cell_Blank_HasRuneSpace()
		{
			var blank = Cell.Blank;

			Assert.Equal(new Rune(' '), blank.Character);
		}

		#endregion

		#region Combiners Tests

		[Fact]
		public void Cell_Combiners_DefaultNull()
		{
			var cell = new Cell('A', Color.White, Color.Black);
			Assert.Null(cell.Combiners);
		}

		[Fact]
		public void Cell_AppendCombiner_SingleRune()
		{
			var cell = new Cell('A', Color.White, Color.Black);
			cell.AppendCombiner(new Rune(0xFE0F));
			Assert.NotNull(cell.Combiners);
			Assert.Contains("\uFE0F", cell.Combiners);
		}

		[Fact]
		public void Cell_AppendCombiner_MultipleTimes()
		{
			var cell = new Cell('A', Color.White, Color.Black);
			cell.AppendCombiner(new Rune(0xFE0F));
			cell.AppendCombiner(new Rune(0x200D));
			Assert.Equal("\uFE0F\u200D", cell.Combiners);
		}

		[Fact]
		public void Cell_VisuallyEquals_CombinersMatter()
		{
			var a = new Cell('A', Color.White, Color.Black);
			var b = new Cell('A', Color.White, Color.Black);
			b.AppendCombiner(new Rune(0xFE0F));
			Assert.False(a.VisuallyEquals(b));
		}

		[Fact]
		public void Cell_Equals_CombinersMatter()
		{
			var a = new Cell('A', Color.White, Color.Black);
			a.Dirty = false;
			var b = new Cell('A', Color.White, Color.Black);
			b.Dirty = false;
			b.AppendCombiner(new Rune(0xFE0F));
			Assert.False(a.Equals(b));
		}

		[Fact]
		public void Cell_GetHashCode_CombinersIncluded()
		{
			var a = new Cell('A', Color.White, Color.Black);
			var b = new Cell('A', Color.White, Color.Black);
			b.AppendCombiner(new Rune(0xFE0F));
			Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
		}

		[Fact]
		public void Cell_ToString_ShowsCombiners()
		{
			var cell = new Cell('X', Color.White, Color.Black);
			cell.AppendCombiner(new Rune(0xFE0F));
			Assert.Contains("combiners", cell.ToString());
		}

		#endregion
	}
}
